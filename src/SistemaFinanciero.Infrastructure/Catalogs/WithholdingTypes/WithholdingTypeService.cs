using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.WithholdingTypes;

/// <summary>
/// Ejecuta los casos de uso del catálogo de tipos de retención.
/// </summary>
internal sealed class WithholdingTypeService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IWithholdingTypeService
{
    private const string DuplicateCodeMessage = "Ya existe un tipo de retención con el mismo código.";

    public async Task<PagedResult<WithholdingTypeModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<WithholdingType> withholdingTypes = dbContext.Set<WithholdingType>().AsNoTracking();

        withholdingTypes = query.Status switch
        {
            CatalogStatusFilter.Active => withholdingTypes.Where(withholding => withholding.IsActive),
            CatalogStatusFilter.Inactive => withholdingTypes.Where(withholding => !withholding.IsActive),
            CatalogStatusFilter.All => withholdingTypes,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            withholdingTypes = withholdingTypes.Where(withholding =>
                withholding.Code.Contains(search) ||
                withholding.Name.Contains(search) ||
                (withholding.Description != null && withholding.Description.Contains(search)));
        }

        PagedResult<WithholdingType> page = await CatalogPersistence.ToPageAsync(
            withholdingTypes
                .OrderBy(withholding => withholding.Name)
                .ThenBy(withholding => withholding.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<WithholdingTypeModel>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<WithholdingTypeModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        WithholdingType? withholding = await dbContext.Set<WithholdingType>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return withholding is null ? null : Map(withholding);
    }

    public async Task<IReadOnlyList<WithholdingTypeOption>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<WithholdingType>()
            .AsNoTracking()
            .Where(withholding => withholding.IsActive)
            .OrderBy(withholding => withholding.Name)
            .ThenBy(withholding => withholding.Code)
            .Select(withholding => new WithholdingTypeOption(
                withholding.Id,
                withholding.Code,
                withholding.Name,
                withholding.Rate))
            .ToListAsync(cancellationToken);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        CreateWithholdingTypeCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            WithholdingType withholding = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Rate,
                command.Description,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(withholding);

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
        UpdateWithholdingTypeCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        WithholdingType? withholding = await dbContext.Set<WithholdingType>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (withholding is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, withholding, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            withholding.UpdateDetails(
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
        WithholdingType? withholding = await dbContext.Set<WithholdingType>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (withholding is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, withholding, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            if (isActive)
            {
                withholding.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                withholding.Deactivate(timeProvider.GetUtcNow(), actorId);
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible cambiar el estado del tipo de retención.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private static WithholdingTypeModel Map(WithholdingType withholding)
    {
        return new WithholdingTypeModel(
            withholding.Id,
            withholding.Code,
            withholding.Name,
            withholding.Rate,
            withholding.Description,
            withholding.IsActive,
            withholding.CreatedAtUtc,
            withholding.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(withholding.RowVersion));
    }
}
