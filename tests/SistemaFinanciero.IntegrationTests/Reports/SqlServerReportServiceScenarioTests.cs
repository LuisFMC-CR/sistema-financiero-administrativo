using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Reports;

/// <summary>
/// Escenarios reales de <c>IReportService</c> que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerReportServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerReportServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetIncomeReportAsync_OnlyIncludesActiveIncomesWithinThePeriodAndTotalsThem()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid categoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            // Período lejano en el pasado para no coincidir con datos reales o de ejemplo de la base
            // de desarrollo: este reporte suma todos los ingresos vigentes del período, sin importar
            // su categoría ni su cuenta de efectivo.
            IncomeCreationResult insidePeriod = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    categoryId, cashAccountId, 500m, new DateOnly(2010, 1, 10),
                    PaymentMethod.Cash, "REC-1", "Reintegro de prueba"),
                actorId);

            IncomeCreationResult outsidePeriod = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    categoryId, cashAccountId, 900m, new DateOnly(2009, 12, 30),
                    PaymentMethod.Cash, "REC-2", "Fuera del período"),
                actorId);

            IncomeCreationResult toBeCancelled = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    categoryId, cashAccountId, 300m, new DateOnly(2010, 1, 12),
                    PaymentMethod.Cash, "REC-3", "Será anulado"),
                actorId);
            IncomeModel cancelledDraft = (await incomes.GetAsync(toBeCancelled.IncomeId!.Value))!;
            await incomes.CancelAsync(toBeCancelled.IncomeId!.Value, "Error de captura", cancelledDraft.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            IncomeReportResult result = await reports.GetIncomeReportAsync(
                new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), "Usuario de prueba");

            IncomeReportLine line = result.Lines.Single(l => l.Amount == 500m);
            Assert.Equal("REC-1", line.Reference);
            Assert.DoesNotContain(result.Lines, l => l.Amount == 900m);
            Assert.DoesNotContain(result.Lines, l => l.Amount == 300m);
            Assert.Equal(500m, result.TotalAmount);
            Assert.Equal("Ingresos por período", result.Header.ReportName);
            Assert.Equal("Usuario de prueba", result.Header.GeneratedByUserName);

            _ = insidePeriod;
            _ = outsidePeriod;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetIncomeReportAsync_WithNoIncomesInThePeriod_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();
            IncomeReportResult result = await reports.GetIncomeReportAsync(
                new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), "Usuario de prueba");

            Assert.Empty(result.Lines);
            Assert.Equal(0m, result.TotalAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetExpenseByCategoryReportAsync_OnlyIncludesConfirmedExpensesGroupedByCategory()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid categoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            // Período lejano en el pasado, además de limitar el reporte a la categoría recién creada,
            // para no coincidir con datos reales o de ejemplo de la base de desarrollo.
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 5), 1000m, actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 20), 500m, actorId);

            Guid draftExpenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, new DateOnly(2010, 1, 8), 9999m, actorId);

            Guid cancelledExpenseId = await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 9), 7777m, actorId);
            ExpenseModel confirmed = (await expenses.GetAsync(cancelledExpenseId))!;
            await expenses.CancelAsync(cancelledExpenseId, "Error de captura", confirmed.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            ExpenseByCategoryReportResult result = await reports.GetExpenseByCategoryReportAsync(
                new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), categoryId, "Usuario de prueba");

            ExpenseByCategoryReportLine line = Assert.Single(result.Lines);
            Assert.Equal(2, line.Count);
            Assert.Equal(1500m, line.NetAmount);
            Assert.Equal(1500m, result.GrandTotal);

            _ = draftExpenseId;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetExpenseByCategoryReportAsync_WithNoExpensesInThePeriod_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();
            ExpenseByCategoryReportResult result = await reports.GetExpenseByCategoryReportAsync(
                new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), categoryId: null, "Usuario de prueba");

            Assert.Empty(result.Lines);
            Assert.Equal(0m, result.GrandTotal);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetBudgetVsExecutedReportAsync_IncludesExecutedFromConfirmedExpensesForThatCategoryAndMonth()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid categoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(categoryId, 2010, 1, 1000m), version: null, actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 5), 600m, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            BudgetVsExecutedReportResult result = await reports.GetBudgetVsExecutedReportAsync(
                2010, 1, "Usuario de prueba");

            BudgetModel line = result.Lines.Single(l => l.CategoryId == categoryId);
            Assert.Equal(1000m, line.BudgetedAmount);
            Assert.Equal(600m, line.ExecutedAmount); // IVA 0% en CreateTaxTypeAsync: ejecutado = neto.
            Assert.Equal(400m, line.AvailableAmount);
            Assert.Equal("Presupuesto contra lo ejecutado", result.Header.ReportName);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetMonthlyComparisonReportAsync_SumsIncomesAndExpensesPerMonthAndFillsMonthsWithNoActivity()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid expenseCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeCategoryId, cashAccountId, 400m, new DateOnly(2010, 1, 10),
                    PaymentMethod.Cash, "REC-1", "Ingreso de enero"),
                actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, expenseCategoryId, taxTypeId, cashAccountId, new DateOnly(2010, 3, 15), 250m, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            MonthlyComparisonReportResult result = await reports.GetMonthlyComparisonReportAsync(
                2010, 1, 2010, 3, "Usuario de prueba");

            Assert.Equal(3, result.Lines.Count);
            Assert.Equal(400m, result.Lines.Single(l => l.PeriodMonth == 1).TotalIncome);
            Assert.Equal(0m, result.Lines.Single(l => l.PeriodMonth == 2).TotalIncome);
            Assert.Equal(0m, result.Lines.Single(l => l.PeriodMonth == 2).TotalExpense);
            Assert.Equal(250m, result.Lines.Single(l => l.PeriodMonth == 3).TotalExpense); // IVA 0%.
            Assert.Equal(400m, result.GrandTotalIncome);
            Assert.Equal(250m, result.GrandTotalExpense);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetMonthlyComparisonReportAsync_WithNoActivityInTheRange_ReturnsZeroTotalsButKeepsTheMonths()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();
            MonthlyComparisonReportResult result = await reports.GetMonthlyComparisonReportAsync(
                2010, 1, 2010, 2, "Usuario de prueba");

            Assert.Equal(2, result.Lines.Count);
            Assert.Equal(0m, result.GrandTotalIncome);
            Assert.Equal(0m, result.GrandTotalExpense);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetCustomerStatementReportAsync_BuildsChronologicalHistoryWithCurrentBalance()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 1000m);

            await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, incomeCategoryId, "AB-ESTADO-1",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(receivableId, 400m, null)]),
                actorId,
                actorIsManagement: false);

            IReportService reports = services.GetRequiredService<IReportService>();
            CustomerStatementReportResult result = await reports.GetCustomerStatementReportAsync(
                customerId, "Usuario de prueba");

            Assert.Equal(2, result.Lines.Count);
            CustomerStatementLine invoiceLine = result.Lines.Single(l => l.MovementType == "Factura a crédito");
            Assert.Equal(1000m, invoiceLine.Amount);
            Assert.False(invoiceLine.IsCancelled);
            CustomerStatementLine paymentLine = result.Lines.Single(l => l.MovementType == "Abono");
            Assert.Equal(400m, paymentLine.Amount);
            Assert.False(paymentLine.IsCancelled);
            Assert.Equal(600m, result.CurrentBalance);
            Assert.Equal("Estado de cuenta de cliente", result.Header.ReportName);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetCustomerStatementReportAsync_KeepsACancelledCreditInvoiceVisibleButExcludesItFromTheCurrentBalance()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 500m);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            InvoiceModel invoiceModel = (await invoices.GetAsync(receivable.InvoiceId))!;
            await invoices.CancelAsync(receivable.InvoiceId, "Factura registrada por error", invoiceModel.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            CustomerStatementReportResult result = await reports.GetCustomerStatementReportAsync(
                customerId, "Usuario de prueba");

            CustomerStatementLine line = Assert.Single(result.Lines);
            Assert.True(line.IsCancelled);
            Assert.Equal(0m, result.CurrentBalance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetCustomerStatementReportAsync_WithNoCreditInvoices_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            Guid customerId = await CreateCustomerAsync(services, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            CustomerStatementReportResult result = await reports.GetCustomerStatementReportAsync(
                customerId, "Usuario de prueba");

            Assert.Empty(result.Lines);
            Assert.Equal(0m, result.CurrentBalance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetPayableBySupplierReportAsync_GroupsVigentPayablesBySupplierAndExcludesPaidOnes()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService supplierPayments = services.GetRequiredService<ISupplierPaymentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid earlierPayableId = await CreateConfirmedCreditPayableAsync(
                services, expenses, supplierId, actorId, new DateOnly(2010, 1, 10), amount: 300m);
            Guid laterPayableId = await CreateConfirmedCreditPayableAsync(
                services, expenses, supplierId, actorId, new DateOnly(2010, 2, 10), amount: 200m);
            Guid toBePaidPayableId = await CreateConfirmedCreditPayableAsync(
                services, expenses, supplierId, actorId, new DateOnly(2010, 3, 10), amount: 150m);

            await supplierPayments.RegisterAsync(
                new RegisterSupplierPaymentCommand(
                    supplierId, cashAccountId, "PAGO-ESTADO-1", DateOnly.FromDateTime(DateTime.UtcNow),
                    [new SupplierPaymentAllocationCommand(toBePaidPayableId, 150m)]),
                actorId,
                actorIsManagement: false);

            IReportService reports = services.GetRequiredService<IReportService>();
            PayableBySupplierReportResult result = await reports.GetPayableBySupplierReportAsync("Usuario de prueba");

            PayableBySupplierReportLine line = Assert.Single(result.Lines);
            Assert.Equal(supplierId, line.SupplierId);
            Assert.Equal(2, line.PayableCount);
            Assert.Equal(500m, line.TotalBalance);
            Assert.Equal(new DateOnly(2010, 2, 9), line.EarliestDueDate); // Vencimiento = emisión + 30 días (Crédito 30).
            Assert.Equal(500m, result.GrandTotalBalance);

            _ = earlierPayableId;
            _ = laterPayableId;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetPayableBySupplierReportAsync_WithNoPayables_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();
            PayableBySupplierReportResult result = await reports.GetPayableBySupplierReportAsync("Usuario de prueba");

            Assert.Empty(result.Lines);
            Assert.Equal(0m, result.GrandTotalBalance);
        });
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid actorId,
        decimal amount)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(null, "Servicio a crédito de pruebas de reportes", "unidad", 1m, amount, 0m, taxTypeId),
            draft.Version,
            actorId);

        draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(
            invoiceId,
            new ConfirmInvoiceCommand(null, null, null, null),
            draft.Version,
            actorId);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Receivables.SingleAsync(r => r.InvoiceId == invoiceId)).Id;
    }

    private static async Task<Guid> CreateConfirmedCreditPayableAsync(
        IServiceProvider services,
        IExpenseService expenses,
        Guid supplierId,
        Guid actorId,
        DateOnly issueDate,
        decimal amount)
    {
        Guid categoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Compra a crédito de pruebas de reportes", issueDate, PaymentTerm.Credit30,
                amount, taxTypeId, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(null, null),
            draft.Version,
            actorId,
            actorIsManagement: false);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Payables.SingleAsync(p => p.ExpenseId == expenseId)).Id;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de reportes",
            $"ID-{suffix}",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private static async Task SetAuthorizationLimitAsync(IServiceProvider services, Guid actorId, decimal limit)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        context.SystemParameters.Add(new SystemParameters(limit, 7, DateTimeOffset.UtcNow, actorId));
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> CreateAndConfirmCashExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        Guid cashAccountId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Gasto de prueba de reportes", issueDate, PaymentTerm.Cash,
                netAmount, taxTypeId, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(cashAccountId, $"REF-{expenseId:N}"[..12]),
            draft.Version,
            actorId,
            actorIsManagement: true);

        return expenseId;
    }

    private static async Task<Guid> CreateDraftExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Borrador de pruebas de reportes", issueDate, PaymentTerm.Cash,
                netAmount, taxTypeId, PaymentMethod.Cash),
            actorId);
        return created.ExpenseId!.Value;
    }

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Caja de pruebas de reportes",
            LedgerAccountType.Asset,
            null,
            CashAccountKind.Cash,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de reportes",
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            "IVA 0% de pruebas de reportes",
            0m,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateCategoryAsync(
        IServiceProvider services,
        Guid actorId,
        FinancialCategoryKind kind)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"LA-{suffix}",
            "Cuenta de pruebas de reportes",
            kind == FinancialCategoryKind.Expense ? LedgerAccountType.Expense : LedgerAccountType.Income,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(ledgerAccount);
        await context.SaveChangesAsync();

        FinancialCategory category = new(
            Guid.NewGuid(),
            $"CAT-{suffix}",
            "Categoría de pruebas de reportes",
            kind,
            null,
            ledgerAccount.Id,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.FinancialCategories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Guid> CreateUserAsync(IServiceProvider services)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        Guid userId = Guid.NewGuid();
        string email = $"sql-test-{userId:N}@example.test";

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            FullName = "Usuario de integración SQL",
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N"),
        });
        await context.SaveChangesAsync();
        return userId;
    }

    private async Task RunInRollbackTransactionAsync(Func<IServiceProvider, Guid, Task> scenario)
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(EnableVariable),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Guid actorId = await CreateUserAsync(scope.ServiceProvider);

        try
        {
            await scenario(scope.ServiceProvider, actorId);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
