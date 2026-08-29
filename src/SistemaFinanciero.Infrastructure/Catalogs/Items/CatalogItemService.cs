using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.Items;

/// <summary>
/// Ejecuta los casos de uso del catálogo unificado de productos y servicios.
/// </summary>
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

        return new PagedResult<CatalogItemModel>(
            page.Items.Select(item => Map(item, categoryNames)).ToArray(),
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

        return Map(item, categoryNames);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        SaveCatalogItemCommand command,
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

        try
        {
            CatalogItem item = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Type,
                command.Description,
                command.UnitOfMeasure,
                command.ReferencePriceCrc,
                command.ReferencePriceUsd,
                command.DefaultIncomeCategoryId,
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
        SaveCatalogItemCommand command,
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

        try
        {
            item.UpdateDetails(
                command.Code,
                command.Name,
                command.Type,
                command.Description,
                command.UnitOfMeasure,
                command.ReferencePriceCrc,
                command.ReferencePriceUsd,
                command.DefaultIncomeCategoryId,
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

    private static CatalogItemModel Map(
        CatalogItem item,
        IReadOnlyDictionary<Guid, string> categoryNames)
    {
        string? categoryName = item.DefaultIncomeCategoryId is Guid categoryId &&
            categoryNames.TryGetValue(categoryId, out string? foundName)
                ? foundName
                : null;

        return new CatalogItemModel(
            item.Id,
            item.Code,
            item.Name,
            item.Type,
            item.Description,
            item.UnitOfMeasure,
            item.ReferencePriceCrc,
            item.ReferencePriceUsd,
            item.DefaultIncomeCategoryId,
            categoryName,
            item.IsActive,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(item.RowVersion));
    }
}
