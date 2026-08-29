using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.Customers;

/// <summary>
/// Ejecuta los casos de uso de clientes sobre SQL Server.
/// </summary>
internal sealed class CustomerCatalogService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ICustomerCatalogService
{
    public async Task<PagedResult<CustomerModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Customer> customers = dbContext.Set<Customer>().AsNoTracking();

        customers = query.Status switch
        {
            CatalogStatusFilter.Active => customers.Where(customer => customer.IsActive),
            CatalogStatusFilter.Inactive => customers.Where(customer => !customer.IsActive),
            CatalogStatusFilter.All => customers,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            customers = customers.Where(customer =>
                customer.Code.Contains(search) ||
                customer.Name.Contains(search) ||
                (customer.Identification != null && customer.Identification.Contains(search)) ||
                (customer.Email != null && customer.Email.Contains(search)));
        }

        PagedResult<Customer> page = await CatalogPersistence.ToPageAsync(
            customers.OrderBy(customer => customer.Name).ThenBy(customer => customer.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        return MapPage(page);
    }

    public async Task<CustomerModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        Customer? customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return customer is null ? null : Map(customer);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        SaveCustomerCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            Customer customer = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Identification,
                command.Email,
                command.Phone,
                command.Address,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(customer);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un cliente con el mismo código o identificación.",
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
        SaveCustomerCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Customer? customer = await dbContext.Set<Customer>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (customer is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, customer, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            customer.UpdateDetails(
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
                "Ya existe un cliente con el mismo código o identificación.",
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
        Customer? customer = await dbContext.Set<Customer>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (customer is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, customer, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            if (isActive)
            {
                customer.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                customer.Deactivate(timeProvider.GetUtcNow(), actorId);
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

    private static PagedResult<CustomerModel> MapPage(PagedResult<Customer> page)
    {
        return new PagedResult<CustomerModel>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    private static CustomerModel Map(Customer customer)
    {
        return new CustomerModel(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.Identification,
            customer.Email,
            customer.Phone,
            customer.Address,
            customer.IsActive,
            customer.CreatedAtUtc,
            customer.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(customer.RowVersion));
    }
}
