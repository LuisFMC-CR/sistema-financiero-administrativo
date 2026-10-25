using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Receivables;

/// <summary>Consultas de cuentas por cobrar.</summary>
/// <remarks>
/// Conexiones: implementa <c>IReceivableService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo consume <c>ReceivablesController</c> (Web, ruta
/// <c>/cuentas-por-cobrar</c>). El registro y la anulación de abonos los coordina
/// <c>ReceivablePaymentService</c>, que actualiza <c>FinancialDbContext.Receivables</c> directamente.
/// </remarks>
internal sealed class ReceivableService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IReceivableService
{
    public async Task<PagedResult<ReceivableModel>> SearchAsync(
        ReceivableQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Receivable> receivables = dbContext.Set<Receivable>().AsNoTracking();

        receivables = query.Status switch
        {
            ReceivableStatusFilter.Pending => receivables.Where(receivable => receivable.Status == ReceivableStatus.Pending),
            ReceivableStatusFilter.PartiallyPaid => receivables.Where(receivable => receivable.Status == ReceivableStatus.PartiallyPaid),
            ReceivableStatusFilter.Paid => receivables.Where(receivable => receivable.Status == ReceivableStatus.Paid),
            ReceivableStatusFilter.Cancelled => receivables.Where(receivable => receivable.Status == ReceivableStatus.Cancelled),
            ReceivableStatusFilter.All => receivables,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.InvoiceNumber is not null && long.TryParse(query.InvoiceNumber, out long invoiceNumber))
        {
            receivables = receivables.Where(receivable => dbContext.Set<Invoice>().Any(invoice =>
                invoice.Id == receivable.InvoiceId && invoice.Number == invoiceNumber));
        }

        if (query.CustomerName is not null)
        {
            string customerName = query.CustomerName;
            receivables = receivables.Where(receivable => dbContext.Set<Customer>().Any(customer =>
                customer.Id == receivable.CustomerId && customer.Name.Contains(customerName)));
        }

        if (query.BalanceMin is decimal balanceMin)
        {
            receivables = receivables.Where(receivable => receivable.Balance >= balanceMin);
        }

        if (query.BalanceMax is decimal balanceMax)
        {
            receivables = receivables.Where(receivable => receivable.Balance <= balanceMax);
        }

        if (query.DueDateFrom is DateOnly dueDateFrom)
        {
            receivables = receivables.Where(receivable => receivable.DueDate >= dueDateFrom);
        }

        if (query.DueDateTo is DateOnly dueDateTo)
        {
            receivables = receivables.Where(receivable => receivable.DueDate <= dueDateTo);
        }

        PagedResult<Receivable> page = await CatalogPersistence.ToPageAsync(
            receivables.OrderBy(receivable => receivable.DueDate),
            query.Page,
            query.PageSize,
            cancellationToken);

        IReadOnlyDictionary<Guid, InvoiceInfo> invoiceInfo = await GetInvoiceInfoAsync(
            page.Items.Select(receivable => receivable.InvoiceId),
            cancellationToken);
        IReadOnlyDictionary<Guid, string> customerNames = await GetCustomerNamesAsync(
            page.Items.Select(receivable => receivable.CustomerId),
            cancellationToken);

        return new PagedResult<ReceivableModel>(
            page.Items.Select(receivable => Map(
                receivable,
                invoiceInfo.GetValueOrDefault(receivable.InvoiceId),
                customerNames.GetValueOrDefault(receivable.CustomerId, "—"))).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<ReceivableModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Receivable? receivable = await dbContext.Set<Receivable>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (receivable is null)
        {
            return null;
        }

        IReadOnlyDictionary<Guid, InvoiceInfo> invoiceInfo = await GetInvoiceInfoAsync(
            [receivable.InvoiceId],
            cancellationToken);
        IReadOnlyDictionary<Guid, string> customerNames = await GetCustomerNamesAsync(
            [receivable.CustomerId],
            cancellationToken);

        return Map(
            receivable,
            invoiceInfo.GetValueOrDefault(receivable.InvoiceId),
            customerNames.GetValueOrDefault(receivable.CustomerId, "—"));
    }

    public async Task<IReadOnlyList<ReceivableModel>> GetPendingByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        List<Receivable> receivables = await dbContext.Set<Receivable>()
            .AsNoTracking()
            .Where(receivable => receivable.CustomerId == customerId
                && (receivable.Status == ReceivableStatus.Pending
                    || receivable.Status == ReceivableStatus.PartiallyPaid))
            .OrderBy(receivable => receivable.DueDate)
            .ToListAsync(cancellationToken);

        if (receivables.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<Guid, InvoiceInfo> invoiceInfo = await GetInvoiceInfoAsync(
            receivables.Select(receivable => receivable.InvoiceId),
            cancellationToken);
        IReadOnlyDictionary<Guid, string> customerNames = await GetCustomerNamesAsync(
            [customerId],
            cancellationToken);
        string customerName = customerNames.GetValueOrDefault(customerId, "—");

        return receivables
            .Select(receivable => Map(receivable, invoiceInfo.GetValueOrDefault(receivable.InvoiceId), customerName))
            .ToArray();
    }

    public async Task<OverdueSummary> GetOverdueSummaryAsync(
        int alertDays,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        DateOnly upcomingThreshold = today.AddDays(alertDays);

        IQueryable<Receivable> pending = dbContext.Set<Receivable>()
            .AsNoTracking()
            .Where(receivable => receivable.Status == ReceivableStatus.Pending
                || receivable.Status == ReceivableStatus.PartiallyPaid);

        var overdue = await pending
            .Where(receivable => receivable.DueDate < today)
            .GroupBy(receivable => 1)
            .Select(group => new { Count = group.Count(), Amount = group.Sum(receivable => receivable.Balance) })
            .SingleOrDefaultAsync(cancellationToken);

        var upcoming = await pending
            .Where(receivable => receivable.DueDate >= today && receivable.DueDate <= upcomingThreshold)
            .GroupBy(receivable => 1)
            .Select(group => new { Count = group.Count(), Amount = group.Sum(receivable => receivable.Balance) })
            .SingleOrDefaultAsync(cancellationToken);

        return new OverdueSummary(
            overdue?.Count ?? 0,
            overdue?.Amount ?? 0m,
            upcoming?.Count ?? 0,
            upcoming?.Amount ?? 0m);
    }

    private async Task<IReadOnlyDictionary<Guid, InvoiceInfo>> GetInvoiceInfoAsync(
        IEnumerable<Guid> invoiceIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = invoiceIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, InvoiceInfo>();
        }

        return await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => ids.Contains(invoice.Id))
            .ToDictionaryAsync(
                invoice => invoice.Id,
                invoice => new InvoiceInfo(invoice.Number, invoice.NetAmount),
                cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetCustomerNamesAsync(
        IEnumerable<Guid> customerIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = customerIds.Distinct().ToArray();

        if (ids.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => ids.Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id, customer => customer.Name, cancellationToken);
    }

    private static ReceivableModel Map(Receivable receivable, InvoiceInfo invoiceInfo, string customerName)
    {
        return new ReceivableModel(
            receivable.Id,
            receivable.InvoiceId,
            invoiceInfo.Number,
            invoiceInfo.NetAmount,
            receivable.CustomerId,
            customerName,
            receivable.OriginalAmount,
            receivable.Balance,
            receivable.DueDate,
            receivable.Status,
            receivable.CreatedAtUtc,
            CatalogPersistence.EncodeVersion(receivable.RowVersion));
    }

    private readonly record struct InvoiceInfo(long? Number, decimal NetAmount);
}
