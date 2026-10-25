using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Payables;

/// <summary>Consulta cuentas por pagar. Espejo de <c>ReceivableService</c>, del lado de ingresos.</summary>
/// <remarks>
/// Conexiones: implementa <c>IPayableService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>. Las crea y anula en cascada <c>ExpenseService</c>; las
/// actualiza <c>SupplierPaymentService</c> al registrar o anular un pago.
/// </remarks>
internal sealed class PayableService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IPayableService
{
    public async Task<PagedResult<PayableModel>> SearchAsync(
        PayableQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<Payable> payables = dbContext.Set<Payable>().AsNoTracking();

        payables = query.Status switch
        {
            PayableStatusFilter.Pending => payables.Where(payable => payable.Status == PayableStatus.Pending),
            PayableStatusFilter.PartiallyPaid => payables.Where(payable => payable.Status == PayableStatus.PartiallyPaid),
            PayableStatusFilter.Paid => payables.Where(payable => payable.Status == PayableStatus.Paid),
            PayableStatusFilter.Cancelled => payables.Where(payable => payable.Status == PayableStatus.Cancelled),
            PayableStatusFilter.All => payables,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.ExpenseCode is not null)
        {
            string expenseCode = query.ExpenseCode;
            payables = payables.Where(payable => dbContext.Set<Expense>().Any(expense =>
                expense.Id == payable.ExpenseId && expense.Code.Contains(expenseCode)));
        }

        if (query.SupplierName is not null)
        {
            string supplierName = query.SupplierName;
            payables = payables.Where(payable => dbContext.Set<Supplier>().Any(supplier =>
                supplier.Id == payable.SupplierId && supplier.Name.Contains(supplierName)));
        }

        if (query.BalanceMin is decimal balanceMin)
        {
            payables = payables.Where(payable => payable.Balance >= balanceMin);
        }

        if (query.BalanceMax is decimal balanceMax)
        {
            payables = payables.Where(payable => payable.Balance <= balanceMax);
        }

        if (query.DueDateFrom is DateOnly dueDateFrom)
        {
            payables = payables.Where(payable => payable.DueDate >= dueDateFrom);
        }

        if (query.DueDateTo is DateOnly dueDateTo)
        {
            payables = payables.Where(payable => payable.DueDate <= dueDateTo);
        }

        PagedResult<Payable> page = await CatalogPersistence.ToPageAsync(
            payables.OrderBy(payable => payable.DueDate),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<PayableModel>(
            await MapAllAsync(page.Items.ToList(), cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<PayableModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        Payable? payable = await dbContext.Set<Payable>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payable is null)
        {
            return null;
        }

        IReadOnlyList<PayableModel> mapped = await MapAllAsync([payable], cancellationToken);
        return mapped[0];
    }

    public async Task<IReadOnlyList<PayableModel>> GetPendingBySupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        List<Payable> payables = await dbContext.Set<Payable>()
            .AsNoTracking()
            .Where(payable => payable.SupplierId == supplierId
                && (payable.Status == PayableStatus.Pending || payable.Status == PayableStatus.PartiallyPaid))
            .OrderBy(payable => payable.DueDate)
            .ToListAsync(cancellationToken);

        return await MapAllAsync(payables, cancellationToken);
    }

    public async Task<OverdueSummary> GetOverdueSummaryAsync(
        int alertDays,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        DateOnly upcomingThreshold = today.AddDays(alertDays);

        IQueryable<Payable> pending = dbContext.Set<Payable>()
            .AsNoTracking()
            .Where(payable => payable.Status == PayableStatus.Pending
                || payable.Status == PayableStatus.PartiallyPaid);

        var overdue = await pending
            .Where(payable => payable.DueDate < today)
            .GroupBy(payable => 1)
            .Select(group => new { Count = group.Count(), Amount = group.Sum(payable => payable.Balance) })
            .SingleOrDefaultAsync(cancellationToken);

        var upcoming = await pending
            .Where(payable => payable.DueDate >= today && payable.DueDate <= upcomingThreshold)
            .GroupBy(payable => 1)
            .Select(group => new { Count = group.Count(), Amount = group.Sum(payable => payable.Balance) })
            .SingleOrDefaultAsync(cancellationToken);

        return new OverdueSummary(
            overdue?.Count ?? 0,
            overdue?.Amount ?? 0m,
            upcoming?.Count ?? 0,
            upcoming?.Amount ?? 0m);
    }

    private async Task<IReadOnlyList<PayableModel>> MapAllAsync(
        List<Payable> payables,
        CancellationToken cancellationToken)
    {
        if (payables.Count == 0)
        {
            return [];
        }

        Guid[] expenseIds = payables.Select(payable => payable.ExpenseId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> expenseCodes = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expenseIds.Contains(expense.Id))
            .ToDictionaryAsync(expense => expense.Id, expense => expense.Code, cancellationToken);

        Guid[] supplierIds = payables.Select(payable => payable.SupplierId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> supplierNames = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => supplierIds.Contains(supplier.Id))
            .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Name, cancellationToken);

        return payables
            .Select(payable => new PayableModel(
                payable.Id,
                payable.ExpenseId,
                expenseCodes.GetValueOrDefault(payable.ExpenseId, "—"),
                payable.SupplierId,
                supplierNames.GetValueOrDefault(payable.SupplierId, "—"),
                payable.OriginalAmount,
                payable.Balance,
                payable.DueDate,
                payable.Status,
                payable.CreatedAtUtc,
                CatalogPersistence.EncodeVersion(payable.RowVersion)))
            .ToArray();
    }
}
