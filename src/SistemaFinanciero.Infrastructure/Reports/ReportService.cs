using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
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
            $"Abono {payment.Code} (ref. {payment.Reference})",
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

    public async Task<JournalBookReportResult> GetJournalBookReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        List<JournalEntry> entries = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate >= periodFrom && entry.EntryDate <= periodTo)
            .OrderBy(entry => entry.EntryDate)
            .ThenBy(entry => entry.Number)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, LedgerAccount> accounts = await GetLedgerAccountsAsync(
            entries.SelectMany(entry => entry.Lines).Select(line => line.AccountId),
            cancellationToken);

        JournalBookEntry[] bookEntries = entries
            .Select(entry => new JournalBookEntry(
                entry.Number,
                entry.EntryDate,
                entry.Description,
                entry.SourceType,
                entry.Lines
                    .OrderBy(line => line.LineNumber)
                    .Select(line =>
                    {
                        LedgerAccount? account = accounts.GetValueOrDefault(line.AccountId);
                        return new JournalBookLine(account?.Code ?? "—", account?.Name ?? "—", line.Debit, line.Credit);
                    })
                    .ToArray()))
            .ToArray();

        ReportHeader header = new(
            "Libro diario",
            FormatPeriod(periodFrom, periodTo),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new JournalBookReportResult(
            header,
            periodFrom,
            periodTo,
            bookEntries,
            bookEntries.Sum(entry => entry.Lines.Sum(line => line.Debit)),
            bookEntries.Sum(entry => entry.Lines.Sum(line => line.Credit)));
    }

    public async Task<LedgerBookReportResult> GetLedgerBookReportAsync(
        Guid accountId,
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        LedgerAccount account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == accountId, cancellationToken);

        bool isDebitNature = account.Type is LedgerAccountType.Asset or LedgerAccountType.Expense;

        List<JournalEntry> priorEntries = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate < periodFrom && entry.Lines.Any(line => line.AccountId == accountId))
            .ToListAsync(cancellationToken);

        decimal openingBalance = SignedTotal(
            priorEntries.SelectMany(entry => entry.Lines).Where(line => line.AccountId == accountId),
            isDebitNature);

        List<JournalEntry> periodEntries = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate >= periodFrom
                && entry.EntryDate <= periodTo
                && entry.Lines.Any(line => line.AccountId == accountId))
            .OrderBy(entry => entry.EntryDate)
            .ThenBy(entry => entry.Number)
            .ToListAsync(cancellationToken);

        List<LedgerBookMovement> movements = [];
        decimal runningBalance = openingBalance;

        foreach (JournalEntry entry in periodEntries)
        {
            foreach (JournalEntryLine line in entry.Lines
                .Where(line => line.AccountId == accountId)
                .OrderBy(line => line.LineNumber))
            {
                runningBalance += isDebitNature ? line.Debit - line.Credit : line.Credit - line.Debit;
                movements.Add(new LedgerBookMovement(
                    entry.EntryDate, entry.Number, entry.Description, line.Debit, line.Credit, runningBalance));
            }
        }

        ReportHeader header = new(
            "Libro mayor",
            $"Cuenta {account.Code} — {account.Name}; {FormatPeriod(periodFrom, periodTo)}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new LedgerBookReportResult(
            header, accountId, account.Code, account.Name, periodFrom, periodTo, openingBalance, movements, runningBalance);
    }

    public async Task<TrialBalanceReportResult> GetTrialBalanceReportAsync(
        DateOnly cutoffDate,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        List<JournalEntry> entries = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate <= cutoffDate)
            .ToListAsync(cancellationToken);

        var accountTotals = entries
            .SelectMany(entry => entry.Lines)
            .GroupBy(line => line.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                TotalDebit = group.Sum(line => line.Debit),
                TotalCredit = group.Sum(line => line.Credit),
            })
            .Where(totals => totals.TotalDebit != 0 || totals.TotalCredit != 0)
            .ToArray();

        ReportHeader header = new(
            "Balanza de comprobación",
            $"Fecha de corte: {cutoffDate:dd/MM/yyyy}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        if (accountTotals.Length == 0)
        {
            return new TrialBalanceReportResult(header, cutoffDate, [], 0m, 0m, 0m, 0m);
        }

        Dictionary<Guid, LedgerAccount> accounts = await GetLedgerAccountsAsync(
            accountTotals.Select(totals => totals.AccountId),
            cancellationToken);
        Dictionary<Guid, LedgerAccount> groupAccounts = await GetLedgerAccountsAsync(
            accounts.Values.Select(account => account.ParentId ?? Guid.Empty).Where(id => id != Guid.Empty),
            cancellationToken);

        TrialBalanceGroupLine[] groups = accountTotals
            .Select(totals => new
            {
                Account = accounts[totals.AccountId],
                totals.TotalDebit,
                totals.TotalCredit,
            })
            .GroupBy(x => x.Account.ParentId ?? Guid.Empty)
            .Select(group =>
            {
                LedgerAccount? groupAccount = group.Key == Guid.Empty ? null : groupAccounts.GetValueOrDefault(group.Key);
                TrialBalanceAccountLine[] accountLines = group
                    .OrderBy(x => x.Account.Code, StringComparer.Ordinal)
                    .Select(x =>
                    {
                        decimal net = x.TotalDebit - x.TotalCredit;
                        return new TrialBalanceAccountLine(
                            x.Account.Code,
                            x.Account.Name,
                            x.TotalDebit,
                            x.TotalCredit,
                            net >= 0 ? net : 0m,
                            net < 0 ? -net : 0m);
                    })
                    .ToArray();

                return new TrialBalanceGroupLine(
                    groupAccount?.Code ?? "—",
                    groupAccount?.Name ?? "—",
                    accountLines,
                    accountLines.Sum(line => line.TotalDebit),
                    accountLines.Sum(line => line.TotalCredit),
                    accountLines.Sum(line => line.DebitBalance),
                    accountLines.Sum(line => line.CreditBalance));
            })
            .OrderBy(group => group.GroupCode, StringComparer.Ordinal)
            .ToArray();

        return new TrialBalanceReportResult(
            header,
            cutoffDate,
            groups,
            groups.Sum(group => group.TotalDebit),
            groups.Sum(group => group.TotalCredit),
            groups.Sum(group => group.DebitBalance),
            groups.Sum(group => group.CreditBalance));
    }

    public async Task<IncomeStatementReportResult> GetIncomeStatementReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        List<JournalEntry> entries = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate >= periodFrom && entry.EntryDate <= periodTo)
            .ToListAsync(cancellationToken);

        List<JournalEntryLine> allLines = entries.SelectMany(entry => entry.Lines).ToList();
        Dictionary<Guid, LedgerAccount> accounts = await GetLedgerAccountsAsync(
            allLines.Select(line => line.AccountId),
            cancellationToken);

        (IncomeStatementGroupLine[] incomeGroups, decimal totalIncome) = await BuildIncomeStatementSideAsync(
            allLines, accounts, LedgerAccountType.Income, isCreditNature: true, cancellationToken);
        (IncomeStatementGroupLine[] expenseGroups, decimal totalExpense) = await BuildIncomeStatementSideAsync(
            allLines, accounts, LedgerAccountType.Expense, isCreditNature: false, cancellationToken);

        ReportHeader header = new(
            "Estado de resultados",
            FormatPeriod(periodFrom, periodTo),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new IncomeStatementReportResult(
            header, periodFrom, periodTo, incomeGroups, totalIncome, expenseGroups, totalExpense, totalIncome - totalExpense);
    }

    private async Task<(IncomeStatementGroupLine[] Groups, decimal Total)> BuildIncomeStatementSideAsync(
        IReadOnlyList<JournalEntryLine> allLines,
        Dictionary<Guid, LedgerAccount> accounts,
        LedgerAccountType type,
        bool isCreditNature,
        CancellationToken cancellationToken)
    {
        var accountTotals = allLines
            .Where(line => accounts.TryGetValue(line.AccountId, out LedgerAccount? account) && account.Type == type)
            .GroupBy(line => line.AccountId)
            .Select(group =>
            {
                LedgerAccount account = accounts[group.Key];
                decimal amount = isCreditNature
                    ? group.Sum(line => line.Credit - line.Debit)
                    : group.Sum(line => line.Debit - line.Credit);
                return (Account: account, Amount: amount);
            })
            .Where(totals => totals.Amount != 0)
            .ToArray();

        if (accountTotals.Length == 0)
        {
            return ([], 0m);
        }

        Dictionary<Guid, LedgerAccount> groupAccounts = await GetLedgerAccountsAsync(
            accountTotals.Select(totals => totals.Account.ParentId ?? Guid.Empty).Where(id => id != Guid.Empty),
            cancellationToken);

        IncomeStatementGroupLine[] groups = accountTotals
            .GroupBy(totals => totals.Account.ParentId ?? Guid.Empty)
            .Select(group =>
            {
                LedgerAccount? groupAccount = group.Key == Guid.Empty ? null : groupAccounts.GetValueOrDefault(group.Key);
                IncomeStatementAccountLine[] accountLines = group
                    .OrderBy(totals => totals.Account.Code, StringComparer.Ordinal)
                    .Select(totals => new IncomeStatementAccountLine(totals.Account.Code, totals.Account.Name, totals.Amount))
                    .ToArray();

                return new IncomeStatementGroupLine(
                    groupAccount?.Code ?? "—",
                    groupAccount?.Name ?? "—",
                    accountLines,
                    accountLines.Sum(line => line.Amount));
            })
            .OrderBy(group => group.GroupCode, StringComparer.Ordinal)
            .ToArray();

        return (groups, groups.Sum(group => group.Subtotal));
    }

    private static decimal SignedTotal(IEnumerable<JournalEntryLine> lines, bool isDebitNature) =>
        isDebitNature
            ? lines.Sum(line => line.Debit - line.Credit)
            : lines.Sum(line => line.Credit - line.Debit);

    private async Task<Dictionary<Guid, LedgerAccount>> GetLedgerAccountsAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = accountIds.Distinct().ToArray();

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);
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
