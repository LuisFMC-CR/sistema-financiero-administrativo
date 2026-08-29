using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.Suppliers;

/// <summary>
/// Ejecuta los casos de uso de proveedores sobre SQL Server.
/// </summary>
internal sealed class SupplierCatalogService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ISupplierCatalogService
{
    public async Task<PagedResult<SupplierModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Supplier> suppliers = dbContext.Set<Supplier>().AsNoTracking();

        suppliers = query.Status switch
        {
            CatalogStatusFilter.Active => suppliers.Where(supplier => supplier.IsActive),
            CatalogStatusFilter.Inactive => suppliers.Where(supplier => !supplier.IsActive),
            CatalogStatusFilter.All => suppliers,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            suppliers = suppliers.Where(supplier =>
                supplier.Code.Contains(search) ||
                supplier.Name.Contains(search) ||
                (supplier.Identification != null && supplier.Identification.Contains(search)) ||
                (supplier.Email != null && supplier.Email.Contains(search)));
        }

        PagedResult<Supplier> page = await CatalogPersistence.ToPageAsync(
            suppliers.OrderBy(supplier => supplier.Name).ThenBy(supplier => supplier.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        return MapPage(page);
    }

    public async Task<SupplierModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Supplier? supplier = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return supplier is null ? null : Map(supplier);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        SaveSupplierCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            Supplier supplier = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Identification,
                command.Email,
                command.Phone,
                command.Address,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(supplier);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un proveedor con el mismo código o identificación.",
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
        SaveSupplierCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Supplier? supplier = await dbContext.Set<Supplier>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (supplier is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, supplier, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            supplier.UpdateDetails(
                command.Code,
                command.Name,
                command.Identification,
                command.Email,
                command.Phone,
                command.Address,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un proveedor con el mismo código o identificación.",
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
        Supplier? supplier = await dbContext.Set<Supplier>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (supplier is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, supplier, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            if (isActive)
            {
                supplier.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                supplier.Deactivate(timeProvider.GetUtcNow(), actorId);
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible cambiar el estado porque el código o la identificación están en uso.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private static PagedResult<SupplierModel> MapPage(PagedResult<Supplier> page)
    {
        return new PagedResult<SupplierModel>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    private static SupplierModel Map(Supplier supplier)
    {
        return new SupplierModel(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.Identification,
            supplier.Email,
            supplier.Phone,
            supplier.Address,
            supplier.IsActive,
            supplier.CreatedAtUtc,
            supplier.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(supplier.RowVersion));
    }
}
