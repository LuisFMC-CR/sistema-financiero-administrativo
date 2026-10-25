using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Reports;

/// <summary>
/// Agrega los datos de cada reporte financiero (regla de negocio 8) directamente sobre
/// <see cref="FinancialDbContext"/>: no son operaciones de un módulo sino una vista de varios a la
/// vez, igual criterio que <c>BudgetService</c>. El reporte de presupuesto contra lo ejecutado es la
/// excepción: reutiliza <see cref="IBudgetService"/> en vez de consultar la base directamente, porque
/// ya calcula exactamente lo que el reporte necesita.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IReportService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyecta <c>ReportsController</c> (Web, ruta
/// <c>/reportes</c>).
/// </remarks>
internal sealed class ReportService(
    FinancialDbContext dbContext,
    IBudgetService budgetService,
    TimeProvider timeProvider) : IReportService
{
    public async Task<IncomeReportResult> GetIncomeReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        List<Income> incomes = await dbContext.Set<Income>()
            .AsNoTracking()
            .Where(income => income.Status == IncomeStatus.Active
                && income.IncomeDate >= periodFrom
                && income.IncomeDate <= periodTo)
            .OrderBy(income => income.IncomeDate)
            .ThenBy(income => income.Code)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> categoryNames = await GetCategoryNamesAsync(
            incomes.Select(income => income.CategoryId),
            cancellationToken);
        Dictionary<Guid, string> cashAccountNames = await GetLedgerAccountNamesAsync(
            incomes.Select(income => income.CashAccountId),
            cancellationToken);

        IncomeReportLine[] lines = incomes
            .Select(income => new IncomeReportLine(
                income.Code,
                income.IncomeDate,
                categoryNames.GetValueOrDefault(income.CategoryId, "—"),
                cashAccountNames.GetValueOrDefault(income.CashAccountId, "—"),
                income.Origin,
                income.Reference,
                income.Amount))
            .ToArray();

        ReportHeader header = new(
            "Ingresos por período",
            FormatPeriod(periodFrom, periodTo),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new IncomeReportResult(
            header,
            periodFrom,
            periodTo,
            lines,
            lines.Sum(line => line.Amount));
    }

    public async Task<ExpenseByCategoryReportResult> GetExpenseByCategoryReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? categoryId,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        List<Expense> expenses = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodFrom
                && expense.IssueDate <= periodTo
                && (!categoryId.HasValue || expense.CategoryId == categoryId.Value))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, FinancialCategory> categories = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => expenses.Select(expense => expense.CategoryId).Distinct().Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, cancellationToken);

        ExpenseByCategoryReportLine[] lines = expenses
            .GroupBy(expense => expense.CategoryId)
            .Select(group =>
            {
                FinancialCategory? category = categories.GetValueOrDefault(group.Key);
                return new ExpenseByCategoryReportLine(
                    category?.Code ?? "—",
                    category?.Name ?? "—",
                    group.Count(),
                    group.Sum(expense => expense.NetAmount),
                    group.Sum(expense => expense.TaxAmount),
                    group.Sum(expense => expense.TotalAmount));
            })
            .OrderBy(line => line.CategoryCode, StringComparer.Ordinal)
            .ToArray();

        ReportHeader header = new(
            "Gastos por período y categoría",
            FormatPeriod(periodFrom, periodTo)
                + (categoryId is null
                    ? "; todas las categorías"
                    : $"; categoría {categories.GetValueOrDefault(categoryId.Value)?.Name ?? "—"}"),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new ExpenseByCategoryReportResult(
            header,
            periodFrom,
            periodTo,
            categoryId,
            lines,
            lines.Sum(line => line.TotalAmount));
    }

    public async Task<BudgetVsExecutedReportResult> GetBudgetVsExecutedReportAsync(
        int periodYear,
        int periodMonth,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BudgetModel> lines = await budgetService.SearchByPeriodAsync(
            periodYear,
            periodMonth,
            cancellationToken);

        ReportHeader header = new(
            "Presupuesto contra lo ejecutado",
            $"Mes {periodMonth:D2}/{periodYear}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new BudgetVsExecutedReportResult(
            header,
            periodYear,
            periodMonth,
            lines,
            lines.Sum(line => line.BudgetedAmount ?? 0m),
            lines.Sum(line => line.ExecutedAmount));
    }

    public async Task<MonthlyComparisonReportResult> GetMonthlyComparisonReportAsync(
        int periodYearFrom,
        int periodMonthFrom,
        int periodYearTo,
        int periodMonthTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        DateOnly periodFrom = new(periodYearFrom, periodMonthFrom, 1);
        DateOnly periodTo = new DateOnly(periodYearTo, periodMonthTo, 1).AddMonths(1).AddDays(-1);
        ValidatePeriod(periodFrom, periodTo);

        List<(DateOnly Date, decimal Amount)> incomes = await dbContext.Set<Income>()
            .AsNoTracking()
            .Where(income => income.Status == IncomeStatus.Active
                && income.IncomeDate >= periodFrom
                && income.IncomeDate <= periodTo)
            .Select(income => new ValueTuple<DateOnly, decimal>(income.IncomeDate, income.Amount))
            .ToListAsync(cancellationToken);

        List<(DateOnly Date, decimal Amount)> expenses = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodFrom
                && expense.IssueDate <= periodTo)
            .Select(expense => new ValueTuple<DateOnly, decimal>(expense.IssueDate, expense.TotalAmount))
            .ToListAsync(cancellationToken);

        List<MonthlyComparisonLine> lines = [];
        DateOnly month = periodFrom;

        while (month <= periodTo)
        {
            int year = month.Year;
            int monthNumber = month.Month;

            lines.Add(new MonthlyComparisonLine(
                year,
                monthNumber,
                incomes
                    .Where(income => income.Date.Year == year && income.Date.Month == monthNumber)
                    .Sum(income => income.Amount),
                expenses
                    .Where(expense => expense.Date.Year == year && expense.Date.Month == monthNumber)
                    .Sum(expense => expense.Amount)));

            month = month.AddMonths(1);
        }

        ReportHeader header = new(
            "Comparativo mensual de ingresos y gastos",
            $"Del {periodMonthFrom:D2}/{periodYearFrom} al {periodMonthTo:D2}/{periodYearTo}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new MonthlyComparisonReportResult(
            header,
            periodYearFrom,
            periodMonthFrom,
            periodYearTo,
            periodMonthTo,
            lines,
            lines.Sum(line => line.TotalIncome),
            lines.Sum(line => line.TotalExpense));
    }

    public async Task<CustomerStatementReportResult> GetCustomerStatementReportAsync(
        Guid customerId,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        Customer customer = await dbContext.Set<Customer>()
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == customerId, cancellationToken);

        List<Receivable> receivables = await dbContext.Set<Receivable>()
            .AsNoTracking()
            .Where(receivable => receivable.CustomerId == customerId)
            .ToListAsync(cancellationToken);

        Guid[] receivableIds = receivables.Select(receivable => receivable.Id).ToArray();

        Dictionary<Guid, Invoice> invoicesByReceivableId = await dbContext.Set<Receivable>()
            .AsNoTracking()
            .Where(receivable => receivable.CustomerId == customerId)
            .Join(dbContext.Set<Invoice>(), receivable => receivable.InvoiceId, invoice => invoice.Id,
                (receivable, invoice) => new { receivable.Id, invoice })
            .ToDictionaryAsync(pair => pair.Id, pair => pair.invoice, cancellationToken);

        List<ReceivablePayment> payments = await dbContext.Set<ReceivablePayment>()
            .AsNoTracking()
            .Where(payment => receivableIds.Contains(payment.ReceivableId))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, decimal> withholdingAmountsByPaymentId = await dbContext.Set<Withholding>()
            .AsNoTracking()
            .Where(withholding => payments.Select(payment => payment.Id).Contains(withholding.ReceivablePaymentId))
            .ToDictionaryAsync(withholding => withholding.ReceivablePaymentId, withholding => withholding.Amount, cancellationToken);

        List<CustomerStatementLine> lines = [];

        lines.AddRange(receivables.Select(receivable =>
        {
            Invoice invoice = invoicesByReceivableId[receivable.Id];
            return new CustomerStatementLine(
                invoice.IssueDate,
                "Factura a crédito",
                $"Factura N° {invoice.Number}",
                receivable.OriginalAmount,
                receivable.Status == ReceivableStatus.Cancelled);
        }));

        lines.AddRange(payments.Select(payment => new CustomerStatementLine(
            payment.PaymentDate,
            "Abono",
            $"Abono {payment.Reference}",
            payment.Amount + withholdingAmountsByPaymentId.GetValueOrDefault(payment.Id),
            payment.Status == ReceivablePaymentStatus.Cancelled)));

        lines = lines.OrderBy(line => line.Date).ToList();

        decimal currentBalance = receivables
            .Where(receivable => receivable.Status != ReceivableStatus.Cancelled)
            .Sum(receivable => receivable.Balance);

        ReportHeader header = new(
            "Estado de cuenta de cliente",
            $"Cliente: {customer.Name}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new CustomerStatementReportResult(header, customerId, customer.Name, lines, currentBalance);
    }

    public async Task<PayableBySupplierReportResult> GetPayableBySupplierReportAsync(
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        List<Payable> payables = await dbContext.Set<Payable>()
            .AsNoTracking()
            .Where(payable => payable.Status == PayableStatus.Pending || payable.Status == PayableStatus.PartiallyPaid)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> supplierNames = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => payables.Select(payable => payable.SupplierId).Distinct().Contains(supplier.Id))
            .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Name, cancellationToken);

        PayableBySupplierReportLine[] lines = payables
            .GroupBy(payable => payable.SupplierId)
            .Select(group => new PayableBySupplierReportLine(
                group.Key,
                supplierNames.GetValueOrDefault(group.Key, "—"),
                group.Count(),
                group.Sum(payable => payable.Balance),
                group.Min(payable => payable.DueDate)))
            .OrderBy(line => line.SupplierName, StringComparer.Ordinal)
            .ToArray();

        ReportHeader header = new(
            "Cuentas por pagar por proveedor",
            "Cuentas por pagar vigentes a la fecha de generación",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new PayableBySupplierReportResult(header, lines, lines.Sum(line => line.TotalBalance));
    }

    private static void ValidatePeriod(DateOnly periodFrom, DateOnly periodTo)
    {
        if (periodTo < periodFrom)
        {
            throw new ArgumentException(
                "La fecha final del período no puede ser anterior a la inicial.",
                nameof(periodTo));
        }
    }

    private static string FormatPeriod(DateOnly periodFrom, DateOnly periodTo) =>
        $"Del {periodFrom:dd/MM/yyyy} al {periodTo:dd/MM/yyyy}";

    private async Task<Dictionary<Guid, string>> GetCategoryNamesAsync(
        IEnumerable<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = categoryIds.Distinct().ToArray();

        return await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => ids.Contains(category.Id))
            .ToDictionaryAsync(category => category.Id, category => category.Name, cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> GetLedgerAccountNamesAsync(
        IEnumerable<Guid> ledgerAccountIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = ledgerAccountIds.Distinct().ToArray();

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);
    }
}
