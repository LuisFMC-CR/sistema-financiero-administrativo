using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.Items;

/// <summary>
/// Ejecuta los casos de uso del catálogo unificado de productos y servicios.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>ICatalogItemService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyecta <c>CatalogItemsController</c> (Web). Lee y
/// escribe <c>FinancialDbContext.CatalogItems</c> (tabla <c>catalogos.ProductosServicios</c>); valida
/// que <c>DefaultIncomeCategoryId</c> apunte a una <c>FinancialCategory</c> activa de tipo Ingreso y que
/// <c>DefaultTaxTypeId</c> (RF-52) apunte a un <c>TaxType</c> activo. El código lo asigna esta clase al
/// crear, con una secuencia de SQL Server distinta para productos (<c>PRD-0001</c>) y servicios
/// (<c>SRV-0001</c>, regla 12).
/// </remarks>
internal sealed class CatalogItemService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ICatalogItemService
{
    public async Task<PagedResult<CatalogItemModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<CatalogItem> items = dbContext.Set<CatalogItem>().AsNoTracking();

        items = query.Status switch
        {
            CatalogStatusFilter.Active => items.Where(item => item.IsActive),
            CatalogStatusFilter.Inactive => items.Where(item => !item.IsActive),
            CatalogStatusFilter.All => items,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            items = items.Where(item =>
                item.Code.Contains(search) ||
                item.Name.Contains(search) ||
                (item.Description != null && item.Description.Contains(search)));
        }

        PagedResult<CatalogItem> page = await CatalogPersistence.ToPageAsync(
            items.OrderBy(item => item.Name).ThenBy(item => item.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        IReadOnlyDictionary<Guid, string> categoryNames = await GetCategoryNamesAsync(
            page.Items,
            cancellationToken);
        IReadOnlyDictionary<Guid, string> taxTypeNames = await GetTaxTypeNamesAsync(
            page.Items,
            cancellationToken);

        return new PagedResult<CatalogItemModel>(
            page.Items.Select(item => Map(item, categoryNames, taxTypeNames)).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<CatalogItemModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        CatalogItem? item = await dbContext.Set<CatalogItem>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (item is null)
        {
            return null;
        }

        IReadOnlyDictionary<Guid, string> categoryNames = await GetCategoryNamesAsync(
            [item],
            cancellationToken);
        IReadOnlyDictionary<Guid, string> taxTypeNames = await GetTaxTypeNamesAsync(
            [item],
            cancellationToken);

        return Map(item, categoryNames, taxTypeNames);
    }

    public async Task<IReadOnlyList<CatalogItemOption>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<CatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new CatalogItemOption(
                item.Id,
                item.Code,
                item.Name,
                item.Type,
                item.DefaultTaxTypeId))
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        CreateCatalogItemCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogOperationResult? categoryValidation = await ValidateIncomeCategoryAsync(
            command.DefaultIncomeCategoryId,
            cancellationToken);

        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        CatalogOperationResult? taxTypeValidation = await ValidateTaxTypeAsync(
            command.DefaultTaxTypeId,
            cancellationToken);

        if (taxTypeValidation is not null)
        {
            return taxTypeValidation;
        }

        try
        {
            string code = command.Type == CatalogItemType.Product
                ? await CatalogPersistence.GenerateSequentialCodeAsync(
                    dbContext,
                    "catalogos.Seq_ProductoCodigo",
                    "PRD",
                    4,
                    cancellationToken)
                : await CatalogPersistence.GenerateSequentialCodeAsync(
                    dbContext,
                    "catalogos.Seq_ServicioCodigo",
                    "SRV",
                    4,
                    cancellationToken);

            CatalogItem item = new(
                Guid.NewGuid(),
                code,
                command.Name,
                command.Type,
                command.Description,
                command.UnitOfMeasure,
                command.ReferencePriceCrc,
                command.DefaultIncomeCategoryId,
                command.DefaultTaxTypeId,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(item);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un producto o servicio con el mismo código.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateCatalogItemCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogItem? item = await dbContext.Set<CatalogItem>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (item is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, item, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        CatalogOperationResult? categoryValidation = await ValidateIncomeCategoryAsync(
            command.DefaultIncomeCategoryId,
            cancellationToken);

        if (categoryValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return categoryValidation;
        }

        CatalogOperationResult? taxTypeValidation = await ValidateTaxTypeAsync(
            command.DefaultTaxTypeId,
            cancellationToken);

        if (taxTypeValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return taxTypeValidation;
        }

        try
        {
            item.UpdateDetails(
                command.Name,
                command.Description,
                command.UnitOfMeasure,
                command.ReferencePriceCrc,
                command.DefaultIncomeCategoryId,
                command.DefaultTaxTypeId,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un producto o servicio con el mismo código.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        CatalogItem? item = await dbContext.Set<CatalogItem>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (item is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, item, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        if (isActive)
        {
            CatalogOperationResult? categoryValidation = await ValidateIncomeCategoryAsync(
                item.DefaultIncomeCategoryId,
                cancellationToken);

            if (categoryValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return categoryValidation;
            }

            CatalogOperationResult? taxTypeValidation = await ValidateTaxTypeAsync(
                item.DefaultTaxTypeId,
                cancellationToken);

            if (taxTypeValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return taxTypeValidation;
            }
        }

        try
        {
            if (isActive)
            {
                item.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                item.Deactivate(timeProvider.GetUtcNow(), actorId);
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible cambiar el estado del producto o servicio.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private async Task<CatalogOperationResult?> ValidateIncomeCategoryAsync(
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        if (categoryId is null)
        {
            return null;
        }

        FinancialCategory? category = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId.Value, cancellationToken);

        if (category is null)
        {
            return CatalogOperationResult.Invalid("La categoría de ingreso seleccionada no existe.");
        }

        if (!category.IsActive || category.Kind != FinancialCategoryKind.Income)
        {
            return CatalogOperationResult.Invalid(
                "La categoría predeterminada debe ser una categoría de ingreso activa.");
        }

        return null;
    }

    private async Task<CatalogOperationResult?> ValidateTaxTypeAsync(
        Guid? taxTypeId,
        CancellationToken cancellationToken)
    {
        if (taxTypeId is null)
        {
            return null;
        }

        TaxType? taxType = await dbContext.Set<TaxType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == taxTypeId.Value, cancellationToken);

        if (taxType is null)
        {
            return CatalogOperationResult.Invalid("La tarifa de IVA predeterminada seleccionada no existe.");
        }

        if (!taxType.IsActive)
        {
            return CatalogOperationResult.Invalid("La tarifa de IVA predeterminada debe estar activa.");
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetCategoryNamesAsync(
        IReadOnlyList<CatalogItem> items,
        CancellationToken cancellationToken)
    {
        Guid[] categoryIds = items
            .Where(item => item.DefaultIncomeCategoryId.HasValue)
            .Select(item => item.DefaultIncomeCategoryId!.Value)
            .Distinct()
            .ToArray();

        if (categoryIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .ToDictionaryAsync(
                category => category.Id,
                category => category.Name,
                cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetTaxTypeNamesAsync(
        IReadOnlyList<CatalogItem> items,
        CancellationToken cancellationToken)
    {
        Guid[] taxTypeIds = items
            .Where(item => item.DefaultTaxTypeId.HasValue)
            .Select(item => item.DefaultTaxTypeId!.Value)
            .Distinct()
            .ToArray();

        if (taxTypeIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<TaxType>()
            .AsNoTracking()
            .Where(taxType => taxTypeIds.Contains(taxType.Id))
            .ToDictionaryAsync(
                taxType => taxType.Id,
                taxType => taxType.Name,
                cancellationToken);
    }

    private static CatalogItemModel Map(
        CatalogItem item,
        IReadOnlyDictionary<Guid, string> categoryNames,
        IReadOnlyDictionary<Guid, string> taxTypeNames)
    {
        string? categoryName = item.DefaultIncomeCategoryId is Guid categoryId &&
            categoryNames.TryGetValue(categoryId, out string? foundCategoryName)
                ? foundCategoryName
                : null;
        string? taxTypeName = item.DefaultTaxTypeId is Guid taxTypeId &&
            taxTypeNames.TryGetValue(taxTypeId, out string? foundTaxTypeName)
                ? foundTaxTypeName
                : null;

        return new CatalogItemModel(
            item.Id,
            item.Code,
            item.Name,
            item.Type,
            item.Description,
            item.UnitOfMeasure,
            item.ReferencePriceCrc,
            item.DefaultIncomeCategoryId,
            categoryName,
            item.DefaultTaxTypeId,
            taxTypeName,
            item.IsActive,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(item.RowVersion));
    }
}
