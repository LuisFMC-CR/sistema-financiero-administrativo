using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.TaxTypes;

/// <summary>
/// Ejecuta los casos de uso del catálogo de tipos de impuesto.
/// </summary>
internal sealed class TaxTypeService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ITaxTypeService
{
    private const string DuplicateCodeMessage = "Ya existe un tipo de impuesto con el mismo código.";

    public async Task<PagedResult<TaxTypeModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<TaxType> taxTypes = dbContext.Set<TaxType>().AsNoTracking();

        taxTypes = query.Status switch
        {
            CatalogStatusFilter.Active => taxTypes.Where(taxType => taxType.IsActive),
            CatalogStatusFilter.Inactive => taxTypes.Where(taxType => !taxType.IsActive),
            CatalogStatusFilter.All => taxTypes,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            taxTypes = taxTypes.Where(taxType =>
                taxType.Code.Contains(search) ||
                taxType.Name.Contains(search) ||
                (taxType.Description != null && taxType.Description.Contains(search)));
        }

        PagedResult<TaxType> page = await CatalogPersistence.ToPageAsync(
            taxTypes
                .OrderBy(taxType => taxType.Name)
                .ThenBy(taxType => taxType.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<TaxTypeModel>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<TaxTypeModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        TaxType? taxType = await dbContext.Set<TaxType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return taxType is null ? null : Map(taxType);
    }

    public async Task<IReadOnlyList<TaxTypeOption>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TaxType>()
            .AsNoTracking()
            .Where(taxType => taxType.IsActive)
            .OrderBy(taxType => taxType.Name)
            .ThenBy(taxType => taxType.Code)
            .Select(taxType => new TaxTypeOption(
                taxType.Id,
                taxType.Code,
                taxType.Name,
                taxType.CalculationType,
                taxType.Rate))
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        CreateTaxTypeCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            TaxType taxType = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.CalculationType,
                command.Rate,
                command.Description,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(taxType);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                DuplicateCodeMessage,
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
        UpdateTaxTypeCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        TaxType? taxType = await dbContext.Set<TaxType>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (taxType is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, taxType, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            taxType.UpdateDetails(
                command.Code,
                command.Name,
                command.Rate,
                command.Description,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                DuplicateCodeMessage,
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
        TaxType? taxType = await dbContext.Set<TaxType>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (taxType is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, taxType, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            if (isActive)
            {
                taxType.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                taxType.Deactivate(timeProvider.GetUtcNow(), actorId);
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible cambiar el estado del tipo de impuesto.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<ReferenceTaxTypesLoadResult> LoadReferenceTaxTypesAsync(
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        List<string> storedCodes = await dbContext.Set<TaxType>()
            .AsNoTracking()
            .Select(taxType => taxType.Code)
            .ToListAsync(cancellationToken);
        HashSet<string> existingCodes = new(storedCodes, StringComparer.OrdinalIgnoreCase);
        DateTimeOffset now = timeProvider.GetUtcNow();
        int createdCount = 0;

        try
        {
            foreach (CreateTaxTypeCommand reference in ReferenceTaxTypes.All)
            {
                if (existingCodes.Contains(reference.Code))
                {
                    continue;
                }

                dbContext.Add(new TaxType(
                    Guid.NewGuid(),
                    reference.Code,
                    reference.Name,
                    reference.CalculationType,
                    reference.Rate,
                    reference.Description,
                    now,
                    actorId));
                createdCount++;
            }
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return new ReferenceTaxTypesLoadResult(CatalogOperationResult.Invalid(exception.Message), 0, 0);
        }

        CatalogOperationResult operation = createdCount == 0
            ? CatalogOperationResult.Succeeded()
            : await CatalogPersistence.SaveChangesAsync(dbContext, DuplicateCodeMessage, cancellationToken);

        return operation.IsSuccess
            ? new ReferenceTaxTypesLoadResult(operation, createdCount, ReferenceTaxTypes.All.Count - createdCount)
            : new ReferenceTaxTypesLoadResult(operation, 0, 0);
    }

    private static TaxTypeModel Map(TaxType taxType)
    {
        return new TaxTypeModel(
            taxType.Id,
            taxType.Code,
            taxType.Name,
            taxType.CalculationType,
            taxType.Rate,
            taxType.Description,
            taxType.IsActive,
            taxType.CreatedAtUtc,
            taxType.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(taxType.RowVersion));
    }
}
