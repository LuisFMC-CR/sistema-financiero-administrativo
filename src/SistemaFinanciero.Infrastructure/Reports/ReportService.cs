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
            "Ingresos recibidos por período",
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

        // Bloque 7 del paquete de mejoras finales, punto 4: se calcula desde los asientos (cuentas
        // tipo Ingreso contra tipo Gasto, sin IVA), para que coincida con el estado de resultados en
        // vez de con el efectivo cobrado o pagado.
        List<JournalEntry> entries = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate >= periodFrom && entry.EntryDate <= periodTo)
            .ToListAsync(cancellationToken);

        List<JournalEntryLine> allLines = entries.SelectMany(entry => entry.Lines).ToList();
        Guid[] accountIds = allLines.Select(line => line.AccountId).Distinct().ToArray();
        Dictionary<Guid, LedgerAccountType> accountTypes = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Type, cancellationToken);

        List<(DateOnly Date, decimal Amount)> incomes = entries
            .SelectMany(entry => entry.Lines.Select(line => (entry.EntryDate, line)))
            .Where(pair => accountTypes.GetValueOrDefault(pair.line.AccountId) == LedgerAccountType.Income)
            .Select(pair => (pair.EntryDate, Amount: pair.line.Credit - pair.line.Debit))
            .ToList();

        List<(DateOnly Date, decimal Amount)> expenses = entries
            .SelectMany(entry => entry.Lines.Select(line => (entry.EntryDate, line)))
            .Where(pair => accountTypes.GetValueOrDefault(pair.line.AccountId) == LedgerAccountType.Expense)
            .Select(pair => (pair.EntryDate, Amount: pair.line.Debit - pair.line.Credit))
            .ToList();

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

    public async Task<AnnualBudgetReportResult> GetAnnualBudgetReportAsync(
        int periodYear,
        int upToMonth,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        if (upToMonth < 1 || upToMonth > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(upToMonth), upToMonth, "El mes debe estar entre 1 y 12.");
        }

        // Un llamado por mes de enero a diciembre, reutilizando IBudgetService: así el acumulado usa
        // exactamente las mismas cifras (presupuesto y ejecutado sin IVA) que la pantalla de
        // presupuesto, mes por mes. Los meses posteriores a upToMonth solo completan el presupuesto
        // anual de referencia.
        Dictionary<Guid, (string Code, string Name, decimal AccBudgeted, decimal AccExecuted, decimal AnnualBudgeted)> accumulator = [];

        for (int month = 1; month <= 12; month++)
        {
            IReadOnlyList<BudgetModel> monthResults = await budgetService.SearchByPeriodAsync(periodYear, month, cancellationToken);

            foreach (BudgetModel budget in monthResults)
            {
                (string Code, string Name, decimal AccBudgeted, decimal AccExecuted, decimal AnnualBudgeted) entry =
                    accumulator.TryGetValue(budget.CategoryId, out var existing)
                        ? existing
                        : (budget.CategoryCode, budget.CategoryName, 0m, 0m, 0m);

                decimal budgeted = budget.BudgetedAmount ?? 0m;

                if (month <= upToMonth)
                {
                    entry.AccBudgeted += budgeted;
                    entry.AccExecuted += budget.ExecutedAmount;
                }

                entry.AnnualBudgeted += budgeted;
                accumulator[budget.CategoryId] = entry;
            }
        }

        AnnualBudgetReportLine[] lines = accumulator.Values
            .OrderBy(entry => entry.Code, StringComparer.Ordinal)
            .Select(entry =>
            {
                decimal available = Math.Max(0m, entry.AccBudgeted - entry.AccExecuted);
                decimal variance = entry.AccExecuted - entry.AccBudgeted;
                decimal? variancePercent = entry.AccBudgeted != 0 ? variance / entry.AccBudgeted : null;

                return new AnnualBudgetReportLine(
                    entry.Code, entry.Name, entry.AccBudgeted, entry.AccExecuted, entry.AnnualBudgeted,
                    available, variance, variancePercent);
            })
            .ToArray();

        ReportHeader header = new(
            "Presupuesto anual a la fecha",
            $"Año {periodYear}, hasta {upToMonth:D2}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new AnnualBudgetReportResult(
            header,
            periodYear,
            upToMonth,
            lines,
            lines.Sum(line => line.AccumulatedBudgeted),
            lines.Sum(line => line.AccumulatedExecuted),
            lines.Sum(line => line.AnnualBudgeted));
    }

    public async Task<CashFlowReportResult> GetCashFlowReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? cashAccountId,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        List<(DateOnly Date, decimal Amount)> inflows = await dbContext.Set<Income>()
            .AsNoTracking()
            .Where(income => income.Status == IncomeStatus.Active
                && income.IncomeDate >= periodFrom
                && income.IncomeDate <= periodTo
                && (!cashAccountId.HasValue || income.CashAccountId == cashAccountId.Value))
            .Select(income => new ValueTuple<DateOnly, decimal>(income.IncomeDate, income.Amount))
            .ToListAsync(cancellationToken);

        List<(DateOnly Date, decimal Amount)> outflowPayments = await dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .Where(payment => payment.Status == SupplierPaymentStatus.Active
                && payment.PaymentDate >= periodFrom
                && payment.PaymentDate <= periodTo
                && (!cashAccountId.HasValue || payment.CashAccountId == cashAccountId.Value))
            .Select(payment => new ValueTuple<DateOnly, decimal>(payment.PaymentDate, payment.Amount))
            .ToListAsync(cancellationToken);

        List<(DateOnly Date, decimal Amount)> outflowExpenses = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.Status == ExpenseStatus.Confirmed
                && expense.PaymentTerm == PaymentTerm.Cash
                && expense.IssueDate >= periodFrom
                && expense.IssueDate <= periodTo
                && (!cashAccountId.HasValue || expense.CashAccountId == cashAccountId.Value))
            .Select(expense => new ValueTuple<DateOnly, decimal>(expense.IssueDate, expense.TotalAmount))
            .ToListAsync(cancellationToken);

        List<(DateOnly Date, decimal Amount)> outflows = [.. outflowPayments, .. outflowExpenses];

        List<CashFlowReportLine> lines = [];
        DateOnly month = new(periodFrom.Year, periodFrom.Month, 1);
        DateOnly lastMonth = new(periodTo.Year, periodTo.Month, 1);

        while (month <= lastMonth)
        {
            int year = month.Year;
            int monthNumber = month.Month;
            decimal totalInflow = inflows
                .Where(inflow => inflow.Date.Year == year && inflow.Date.Month == monthNumber)
                .Sum(inflow => inflow.Amount);
            decimal totalOutflow = outflows
                .Where(outflow => outflow.Date.Year == year && outflow.Date.Month == monthNumber)
                .Sum(outflow => outflow.Amount);

            lines.Add(new CashFlowReportLine(year, monthNumber, totalInflow, totalOutflow, totalInflow - totalOutflow));
            month = month.AddMonths(1);
        }

        string cashAccountDescription = cashAccountId is null
            ? "todas las cuentas de efectivo"
            : await GetLedgerAccountNameAsync(cashAccountId.Value, cancellationToken);

        ReportHeader header = new(
            "Flujo de efectivo mensual",
            $"{FormatPeriod(periodFrom, periodTo)}; {cashAccountDescription}",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new CashFlowReportResult(
            header,
            periodFrom,
            periodTo,
            cashAccountId,
            lines,
            lines.Sum(line => line.TotalInflow),
            lines.Sum(line => line.TotalOutflow),
            lines.Sum(line => line.Net));
    }

    public async Task<VatPeriodReportResult> GetVatPeriodReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        var salesByTax = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatus.Confirmed
                && invoice.IssueDate >= periodFrom
                && invoice.IssueDate <= periodTo)
            .SelectMany(invoice => invoice.Lines)
            .GroupBy(line => new { line.Tax.Code, line.Tax.Name })
            .Select(group => new
            {
                group.Key.Code,
                group.Key.Name,
                Base = group.Sum(line => line.Tax.TaxableAmount),
                Vat = group.Sum(line => line.Tax.Amount),
            })
            .ToListAsync(cancellationToken);

        var purchasesByTax = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodFrom
                && expense.IssueDate <= periodTo)
            .GroupBy(expense => new { expense.TaxCode, expense.TaxName })
            .Select(group => new
            {
                Code = group.Key.TaxCode,
                Name = group.Key.TaxName,
                Base = group.Sum(expense => expense.NetAmount),
                Vat = group.Sum(expense => expense.TaxAmount),
            })
            .ToListAsync(cancellationToken);

        string[] codes = salesByTax.Select(entry => entry.Code)
            .Concat(purchasesByTax.Select(entry => entry.Code))
            .Distinct()
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        VatPeriodReportLine[] lines = codes
            .Select(code =>
            {
                var sale = salesByTax.SingleOrDefault(entry => entry.Code == code);
                var purchase = purchasesByTax.SingleOrDefault(entry => entry.Code == code);

                return new VatPeriodReportLine(
                    code,
                    sale?.Name ?? purchase?.Name ?? "—",
                    sale?.Base ?? 0m,
                    sale?.Vat ?? 0m,
                    purchase?.Base ?? 0m,
                    purchase?.Vat ?? 0m);
            })
            .ToArray();

        ReportHeader header = new(
            "IVA del período",
            FormatPeriod(periodFrom, periodTo),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new VatPeriodReportResult(
            header,
            periodFrom,
            periodTo,
            lines,
            lines.Sum(line => line.VatCollected),
            lines.Sum(line => line.VatPaid));
    }

    public async Task<WithholdingByCustomerReportResult> GetWithholdingByCustomerReportAsync(
        DateOnly periodFrom,
        DateOnly periodTo,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        var rows = await dbContext.Set<Withholding>()
            .AsNoTracking()
            .Join(
                dbContext.Set<ReceivablePayment>().AsNoTracking(),
                withholding => withholding.ReceivablePaymentId,
                payment => payment.Id,
                (withholding, payment) => new { withholding, payment })
            .Where(pair => pair.payment.Status == ReceivablePaymentStatus.Active
                && pair.payment.PaymentDate >= periodFrom
                && pair.payment.PaymentDate <= periodTo)
            .Join(
                dbContext.Set<Receivable>().AsNoTracking(),
                pair => pair.payment.ReceivableId,
                receivable => receivable.Id,
                (pair, receivable) => new { pair.withholding, pair.payment, receivable })
            .Join(
                dbContext.Set<Invoice>().AsNoTracking(),
                pair => pair.receivable.InvoiceId,
                invoice => invoice.Id,
                (pair, invoice) => new { pair.withholding, pair.payment, pair.receivable, invoice })
            .Join(
                dbContext.Set<Customer>().AsNoTracking(),
                pair => pair.receivable.CustomerId,
                customer => customer.Id,
                (pair, customer) => new
                {
                    CustomerName = customer.Name,
                    PaymentCode = pair.payment.Code,
                    pair.payment.PaymentDate,
                    InvoiceNumber = pair.invoice.Number,
                    WithholdingTypeName = pair.withholding.Name,
                    pair.withholding.Base,
                    pair.withholding.Amount,
                })
            .ToListAsync(cancellationToken);

        WithholdingByCustomerLine[] lines = rows
            .OrderBy(row => row.CustomerName, StringComparer.Ordinal)
            .ThenBy(row => row.PaymentDate)
            .Select(row => new WithholdingByCustomerLine(
                row.CustomerName, row.PaymentCode, row.InvoiceNumber, row.PaymentDate, row.WithholdingTypeName, row.Base, row.Amount))
            .ToArray();

        ReportHeader header = new(
            "Retenciones aplicadas por clientes",
            FormatPeriod(periodFrom, periodTo),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new WithholdingByCustomerReportResult(
            header, periodFrom, periodTo, lines, lines.Sum(line => line.Amount));
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

    private async Task<string> GetLedgerAccountNameAsync(Guid ledgerAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Id == ledgerAccountId)
            .Select(account => account.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "—";
    }
}
