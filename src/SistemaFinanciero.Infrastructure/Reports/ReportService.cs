using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Domain.VatSettlements;
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

        Dictionary<Guid, string> accountNames = await GetAccountNamesAsync(
            incomes.Select(income => income.AccountId),
            cancellationToken);
        Dictionary<Guid, string> cashAccountNames = await GetLedgerAccountNamesAsync(
            incomes.Select(income => income.CashAccountId),
            cancellationToken);

        IncomeReportLine[] lines = incomes
            .Select(income => new IncomeReportLine(
                income.Code,
                income.IncomeDate,
                accountNames.GetValueOrDefault(income.AccountId, "—"),
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
        Guid? accountId,
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodFrom, periodTo);

        List<Expense> expenses = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodFrom
                && expense.IssueDate <= periodTo
                && (!accountId.HasValue || expense.AccountId == accountId.Value))
            .ToListAsync(cancellationToken);

        Dictionary<Guid, LedgerAccount> accounts = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => expenses.Select(expense => expense.AccountId).Distinct().Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);

        ExpenseByCategoryReportLine[] lines = expenses
            .GroupBy(expense => expense.AccountId)
            .Select(group =>
            {
                LedgerAccount? account = accounts.GetValueOrDefault(group.Key);
                return new ExpenseByCategoryReportLine(
                    account?.Code ?? "—",
                    account?.Name ?? "—",
                    group.Count(),
                    group.Sum(expense => expense.NetAmount),
                    group.Sum(expense => expense.TaxAmount),
                    group.Sum(expense => expense.TotalAmount));
            })
            .OrderBy(line => line.AccountCode, StringComparer.Ordinal)
            .ToArray();

        ReportHeader header = new(
            "Gastos por período y categoría",
            FormatPeriod(periodFrom, periodTo)
                + (accountId is null
                    ? "; todas las categorías"
                    : $"; categoría {accounts.GetValueOrDefault(accountId.Value)?.Name ?? "—"}"),
            timeProvider.GetUtcNow(),
            generatedByUserName);

        return new ExpenseByCategoryReportResult(
            header,
            periodFrom,
            periodTo,
            accountId,
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
        Dictionary<Guid, long?> invoiceNumbersById = invoicesByReceivableId.Values
            .ToDictionary(invoice => invoice.Id, invoice => invoice.Number);

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

        Guid[] invoiceIds = invoicesByReceivableId.Values.Select(invoice => invoice.Id).ToArray();
        List<CreditNote> creditNotes = await dbContext.CreditNotes
            .AsNoTracking()
            .Where(note => invoiceIds.Contains(note.InvoiceId) && note.Status == CreditNoteStatus.Active)
            .ToListAsync(cancellationToken);
        lines.AddRange(creditNotes.Select(note => new CustomerStatementLine(
            note.IssueDate,
            "Nota de crédito",
            $"Nota de crédito {note.Code} sobre factura N° {invoiceNumbersById.GetValueOrDefault(note.InvoiceId)}",
            note.TotalAmount,
            IsCancelled: false)));

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
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        List<Payable> payables = await dbContext.Set<Payable>()
            .AsNoTracking()
            .Where(payable => payable.Status == PayableStatus.Pending || payable.Status == PayableStatus.PartiallyPaid)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> supplierNames = await dbContext.Set<Supplier>()
            .AsNoTracking()
            .Where(supplier => payables.Select(payable => payable.SupplierId).Distinct().Contains(supplier.Id))
            .ToDictionaryAsync(supplier => supplier.Id, supplier => supplier.Name, cancellationToken);

        Dictionary<Guid, Expense> expensesByPayableId = await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => payables.Select(payable => payable.ExpenseId).Contains(expense.Id))
            .ToDictionaryAsync(expense => expense.Id, cancellationToken);

        PayableBySupplierReportLine[] lines = payables
            .GroupBy(payable => payable.SupplierId)
            .Select(group =>
            {
                PayableDetailLine[] details = group
                    .OrderBy(payable => payable.DueDate)
                    .Select(payable =>
                    {
                        Expense expense = expensesByPayableId[payable.ExpenseId];
                        int daysPastDue = today.DayNumber - payable.DueDate.DayNumber;
                        return new PayableDetailLine(
                            expense.Code, expense.IssueDate, payable.DueDate, payable.Balance,
                            AgingAmounts.Zero.Add(daysPastDue, payable.Balance));
                    })
                    .ToArray();

                return new PayableBySupplierReportLine(
                    group.Key,
                    supplierNames.GetValueOrDefault(group.Key, "—"),
                    group.Count(),
                    group.Sum(payable => payable.Balance),
                    group.Min(payable => payable.DueDate),
                    details.Aggregate(AgingAmounts.Zero, (total, detail) => total + detail.Aging),
                    details);
            })
            .OrderBy(line => line.SupplierName, StringComparer.Ordinal)
            .ToArray();

        ReportHeader header = new(
            "Cuentas por pagar por proveedor",
            "Cuentas por pagar vigentes, con antigüedad de saldos a la fecha de generación",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        AgingAmounts grandTotalAging = lines.Aggregate(AgingAmounts.Zero, (total, line) => total + line.Aging);

        return new PayableBySupplierReportResult(
            header, lines, lines.Sum(line => line.TotalBalance), grandTotalAging);
    }

    public async Task<ReceivableByCustomerReportResult> GetReceivableByCustomerReportAsync(
        string generatedByUserName,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

        List<Receivable> receivables = await dbContext.Set<Receivable>()
            .AsNoTracking()
            .Where(receivable => receivable.Status == ReceivableStatus.Pending
                || receivable.Status == ReceivableStatus.PartiallyPaid)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, string> customerNames = await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => receivables.Select(receivable => receivable.CustomerId).Distinct().Contains(customer.Id))
            .ToDictionaryAsync(customer => customer.Id, customer => customer.Name, cancellationToken);

        Dictionary<Guid, Invoice> invoicesByReceivableId = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => receivables.Select(receivable => receivable.InvoiceId).Contains(invoice.Id))
            .ToDictionaryAsync(invoice => invoice.Id, cancellationToken);

        ReceivableByCustomerReportLine[] lines = receivables
            .GroupBy(receivable => receivable.CustomerId)
            .Select(group =>
            {
                ReceivableDetailLine[] details = group
                    .OrderBy(receivable => receivable.DueDate)
                    .Select(receivable =>
                    {
                        Invoice invoice = invoicesByReceivableId[receivable.InvoiceId];
                        int daysPastDue = today.DayNumber - receivable.DueDate.DayNumber;
                        return new ReceivableDetailLine(
                            invoice.Number, invoice.IssueDate, receivable.DueDate, receivable.Balance,
                            AgingAmounts.Zero.Add(daysPastDue, receivable.Balance));
                    })
                    .ToArray();

                return new ReceivableByCustomerReportLine(
                    group.Key,
                    customerNames.GetValueOrDefault(group.Key, "—"),
                    group.Count(),
                    group.Sum(receivable => receivable.Balance),
                    group.Min(receivable => receivable.DueDate),
                    details.Aggregate(AgingAmounts.Zero, (total, detail) => total + detail.Aging),
                    details);
            })
            .OrderBy(line => line.CustomerName, StringComparer.Ordinal)
            .ToArray();

        ReportHeader header = new(
            "Cuentas por cobrar por cliente",
            "Cuentas por cobrar vigentes, con antigüedad de saldos a la fecha de generación",
            timeProvider.GetUtcNow(),
            generatedByUserName);

        AgingAmounts grandTotalAging = lines.Aggregate(AgingAmounts.Zero, (total, line) => total + line.Aging);

        return new ReceivableByCustomerReportResult(
            header, lines, lines.Sum(line => line.TotalBalance), grandTotalAging);
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

        SystemParameters? parameters = await dbContext.SystemParameters
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        DateOnly fiscalStart = parameters?.FiscalPeriodStartDate ?? new DateOnly(periodYear, 1, 1);
        DateOnly fiscalEnd = parameters?.FiscalPeriodEndDate ?? fiscalStart.AddYears(1).AddDays(-1);
        if (periodYear != fiscalStart.Year)
        {
            throw new ArgumentOutOfRangeException(
                nameof(periodYear),
                periodYear,
                $"El año debe coincidir con el inicio del período fiscal vigente ({fiscalStart.Year}).");
        }

        // El presupuesto se define por mes calendario; se suma cada mes que cae dentro del período
        // fiscal, en orden desde su inicio. Meses posteriores al acumulado completan el total anual.
        Dictionary<Guid, (string Code, string Name, decimal AccBudgeted, decimal AccExecuted, decimal AnnualBudgeted)> accumulator = [];

        for (int fiscalMonth = 1; fiscalMonth <= 12; fiscalMonth++)
        {
            DateOnly calendarMonth = fiscalStart.AddMonths(fiscalMonth - 1);
            if (calendarMonth > fiscalEnd)
            {
                break;
            }

            IReadOnlyList<BudgetModel> monthResults = await budgetService.SearchByPeriodAsync(
                calendarMonth.Year,
                calendarMonth.Month,
                cancellationToken);

            foreach (BudgetModel budget in monthResults)
            {
                (string Code, string Name, decimal AccBudgeted, decimal AccExecuted, decimal AnnualBudgeted) entry =
                    accumulator.TryGetValue(budget.AccountId, out var existing)
                        ? existing
                        : (budget.AccountCode, budget.AccountName, 0m, 0m, 0m);

                decimal budgeted = budget.BudgetedAmount ?? 0m;

                if (fiscalMonth <= upToMonth)
                {
                    entry.AccBudgeted += budgeted;
                    entry.AccExecuted += budget.ExecutedAmount;
                }

                entry.AnnualBudgeted += budgeted;
                accumulator[budget.AccountId] = entry;
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
            $"Período fiscal {fiscalStart:dd/MM/yyyy}–{fiscalEnd:dd/MM/yyyy}; acumulado hasta "
                + $"{fiscalStart.AddMonths(upToMonth - 1):MMMM yyyy}",
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

        // Sin filtro de cuenta, un traspaso se compensa a sí mismo (entra a una cuenta propia y sale
        // de otra propia): se excluye por completo. Con filtro, cuenta como entrada si la cuenta es el
        // destino y como salida si es el origen (regla de negocio, bloque 3 del paquete v8).
        List<(DateOnly Date, decimal Amount)> transfersReceived = [];
        List<(DateOnly Date, decimal Amount)> transfersSent = [];
        if (cashAccountId.HasValue)
        {
            transfersReceived = await dbContext.Set<CashTransfer>()
                .AsNoTracking()
                .Where(transfer => transfer.Status == CashTransferStatus.Active
                    && transfer.TransferDate >= periodFrom
                    && transfer.TransferDate <= periodTo
                    && transfer.DestinationCashAccountId == cashAccountId.Value)
                .Select(transfer => new ValueTuple<DateOnly, decimal>(transfer.TransferDate, transfer.Amount))
                .ToListAsync(cancellationToken);

            transfersSent = await dbContext.Set<CashTransfer>()
                .AsNoTracking()
                .Where(transfer => transfer.Status == CashTransferStatus.Active
                    && transfer.TransferDate >= periodFrom
                    && transfer.TransferDate <= periodTo
                    && transfer.SourceCashAccountId == cashAccountId.Value)
                .Select(transfer => new ValueTuple<DateOnly, decimal>(transfer.TransferDate, transfer.Amount))
                .ToListAsync(cancellationToken);
        }

        List<(DateOnly Date, decimal Amount)> vatPayments = await dbContext.Set<VatSettlement>()
            .AsNoTracking()
            .Where(settlement => settlement.Status == VatSettlementStatus.Active
                && settlement.PaymentAmount > 0
                && settlement.SettlementDate >= periodFrom
                && settlement.SettlementDate <= periodTo
                && (!cashAccountId.HasValue || settlement.CashAccountId == cashAccountId.Value))
            .Select(settlement => new ValueTuple<DateOnly, decimal>(settlement.SettlementDate, settlement.PaymentAmount))
            .ToListAsync(cancellationToken);

        List<CashFlowReportLine> lines = [];
        DateOnly month = new(periodFrom.Year, periodFrom.Month, 1);
        DateOnly lastMonth = new(periodTo.Year, periodTo.Month, 1);

        while (month <= lastMonth)
        {
            int year = month.Year;
            int monthNumber = month.Month;
            decimal monthInflow = SumForMonth(inflows, year, monthNumber);
            decimal monthOutflow = SumForMonth(outflows, year, monthNumber);
            decimal monthTransfersReceived = SumForMonth(transfersReceived, year, monthNumber);
            decimal monthTransfersSent = SumForMonth(transfersSent, year, monthNumber);
            decimal monthVatPayments = SumForMonth(vatPayments, year, monthNumber);

            decimal totalInflow = monthInflow + monthTransfersReceived;
            decimal totalOutflow = monthOutflow + monthTransfersSent + monthVatPayments;

            lines.Add(new CashFlowReportLine(
                year,
                monthNumber,
                totalInflow,
                totalOutflow,
                monthTransfersReceived,
                monthTransfersSent,
                monthVatPayments,
                totalInflow - totalOutflow));
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
            .Where(invoice => (invoice.Status == InvoiceStatus.Confirmed
                    || dbContext.CreditNotes.Any(note =>
                        note.InvoiceId == invoice.Id && note.Status == CreditNoteStatus.Active))
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

        var creditsByTax = await (
            from note in dbContext.CreditNotes.AsNoTracking()
            join creditLine in dbContext.Set<CreditNoteLine>().AsNoTracking()
                on note.Id equals creditLine.CreditNoteId
            join invoiceLine in dbContext.Set<InvoiceLine>().AsNoTracking()
                on creditLine.InvoiceLineId equals invoiceLine.Id
            where note.Status == CreditNoteStatus.Active
                && note.IssueDate >= periodFrom
                && note.IssueDate <= periodTo
            group creditLine by new { invoiceLine.Tax.Code, invoiceLine.Tax.Name }
            into taxGroup
            select new
            {
                taxGroup.Key.Code,
                taxGroup.Key.Name,
                Base = -taxGroup.Sum(line => line.NetAmount),
                Vat = -taxGroup.Sum(line => line.TaxAmount),
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

        var netSalesByTax = salesByTax
            .Select(entry => new { entry.Code, entry.Name, entry.Base, entry.Vat })
            .Concat(creditsByTax)
            .GroupBy(entry => new { entry.Code, entry.Name })
            .Select(group => new
            {
                group.Key.Code,
                group.Key.Name,
                Base = group.Sum(entry => entry.Base),
                Vat = group.Sum(entry => entry.Vat),
            })
            .ToArray();

        string[] codes = netSalesByTax.Select(entry => entry.Code)
            .Concat(purchasesByTax.Select(entry => entry.Code))
            .Distinct()
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        VatPeriodReportLine[] lines = codes
            .Select(code =>
            {
                var sale = netSalesByTax.SingleOrDefault(entry => entry.Code == code);
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

        VatPeriodSettlementSummary? settlement = await GetVatPeriodSettlementSummaryAsync(
            periodFrom, periodTo, cancellationToken);

        return new VatPeriodReportResult(
            header,
            periodFrom,
            periodTo,
            lines,
            lines.Sum(line => line.VatCollected),
            lines.Sum(line => line.VatPaid),
            settlement);
    }

    /// <summary>
    /// La liquidación de IVA vigente del mes, solo cuando el rango del reporte cubre exactamente ese
    /// mes calendario completo (bloque 3 del paquete v8, punto 2); nunca descuenta los totales del
    /// reporte, que siguen viniendo únicamente de facturas y gastos.
    /// </summary>
    private async Task<VatPeriodSettlementSummary?> GetVatPeriodSettlementSummaryAsync(
        DateOnly periodFrom, DateOnly periodTo, CancellationToken cancellationToken)
    {
        bool isSingleCalendarMonth = periodFrom == new DateOnly(periodFrom.Year, periodFrom.Month, 1)
            && periodTo == periodFrom.AddMonths(1).AddDays(-1);

        if (!isSingleCalendarMonth)
        {
            return null;
        }

        VatSettlement? settlement = await dbContext.Set<VatSettlement>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Status == VatSettlementStatus.Active
                    && candidate.PeriodYear == periodFrom.Year
                    && candidate.PeriodMonth == periodFrom.Month,
                cancellationToken);

        return settlement is null
            ? null
            : new VatPeriodSettlementSummary(settlement.Code, settlement.PaymentAmount, settlement.CreditCarriedForward);
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

    private static decimal SumForMonth(IReadOnlyList<(DateOnly Date, decimal Amount)> amounts, int year, int month) =>
        amounts.Where(entry => entry.Date.Year == year && entry.Date.Month == month).Sum(entry => entry.Amount);

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

    private async Task<Dictionary<Guid, string>> GetAccountNamesAsync(
        IEnumerable<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = accountIds.Distinct().ToArray();

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => ids.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);
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
