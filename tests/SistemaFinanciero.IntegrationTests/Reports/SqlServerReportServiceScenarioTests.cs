using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
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
            Guid accountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            // Período lejano en el pasado para no coincidir con datos reales o de ejemplo de la base
            // de desarrollo: este reporte suma todos los ingresos vigentes del período, sin importar
            // su categoría ni su cuenta de efectivo.
            await PrepareFabricatedPeriodAsync(
                services, actorId, new DateOnly(2009, 12, 30), new DateOnly(2010, 1, 10), new DateOnly(2010, 1, 12));

            IncomeCreationResult insidePeriod = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    accountId, cashAccountId, 500m, new DateOnly(2010, 1, 10),
                    PaymentMethod.Cash, "REC-1", "Reintegro de prueba"),
                actorId);

            IncomeCreationResult outsidePeriod = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    accountId, cashAccountId, 900m, new DateOnly(2009, 12, 30),
                    PaymentMethod.Cash, "REC-2", "Fuera del período"),
                actorId);

            IncomeCreationResult toBeCancelled = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    accountId, cashAccountId, 300m, new DateOnly(2010, 1, 12),
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
            Assert.Equal("Ingresos recibidos por período", result.Header.ReportName);
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
            Guid accountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            // Período lejano en el pasado, además de limitar el reporte a la categoría recién creada,
            // para no coincidir con datos reales o de ejemplo de la base de desarrollo.
            await PrepareFabricatedPeriodAsync(services, actorId, new DateOnly(2010, 1, 5));

            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 5), 1000m, actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 20), 500m, actorId);

            Guid draftExpenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, new DateOnly(2010, 1, 8), 9999m, actorId);

            Guid cancelledExpenseId = await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 9), 7777m, actorId);
            ExpenseModel confirmed = (await expenses.GetAsync(cancelledExpenseId))!;
            await expenses.CancelAsync(cancelledExpenseId, "Error de captura", confirmed.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            ExpenseByCategoryReportResult result = await reports.GetExpenseByCategoryReportAsync(
                new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), accountId, "Usuario de prueba");

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
                new DateOnly(2010, 1, 1), new DateOnly(2010, 1, 31), accountId: null, "Usuario de prueba");

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
            Guid accountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(accountId, 2010, 1, 1000m), version: null, actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, cashAccountId, new DateOnly(2010, 1, 5), 600m, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            BudgetVsExecutedReportResult result = await reports.GetBudgetVsExecutedReportAsync(
                2010, 1, "Usuario de prueba");

            BudgetModel line = result.Lines.Single(l => l.AccountId == accountId);
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
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid expenseAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await PrepareFabricatedPeriodAsync(
                services, actorId, new DateOnly(2010, 1, 10), new DateOnly(2010, 3, 15));

            await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, cashAccountId, 400m, new DateOnly(2010, 1, 10),
                    PaymentMethod.Cash, "REC-1", "Ingreso de enero"),
                actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, expenseAccountId, taxTypeId, cashAccountId, new DateOnly(2010, 3, 15), 250m, actorId);

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
    public Task GetMonthlyComparisonReportAsync_SourcesFromTheAsientosSoACreditSaleCountsWhenInvoicedNotWhenCollectedAndExcludesVat()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType taxType = new(Guid.NewGuid(), $"IVA-{suffix}", "IVA 13% de pruebas", 13m, "08", null, DateTimeOffset.UtcNow, actorId);
            context.TaxTypes.Add(taxType);
            await context.SaveChangesAsync();

            DateOnly saleDate = new(2027, 2, 10);
            DateOnly collectionDate = new(2027, 2, 20);
            await PrepareFabricatedPeriodAsync(services, actorId, saleDate, collectionDate);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, saleDate, PaymentTerm.Credit30),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito con IVA");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, 1000m, 0m, taxType.Id),
                draft.Version,
                actorId);
            draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(invoiceId, new ConfirmInvoiceCommand(null, null, null), draft.Version, actorId);

            Guid receivableId = (await context.Receivables.AsNoTracking().SingleAsync(r => r.InvoiceId == invoiceId)).Id;
            await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-0001",
                    collectionDate,
                    [new ReceivablePaymentAllocationCommand(receivableId, 1130m, Withholding: null)]),
                actorId,
                actorIsManagement: false);

            IReportService reports = services.GetRequiredService<IReportService>();
            MonthlyComparisonReportResult result = await reports.GetMonthlyComparisonReportAsync(
                2027, 2, 2027, 2, "Usuario de prueba");

            // El ingreso se reconoce al vender (fecha de la factura), no al cobrar, y sin IVA: la
            // factura acredita la cuenta de ingreso por el neto (1000), no por el total con IVA
            // (1130). El abono solo mueve efectivo contra cuentas por cobrar (regla de contabilización
            // del abono, bloque 5); no genera ingreso adicional en el comparativo.
            Assert.Equal(1000m, result.Lines.Single(line => line.PeriodMonth == 2).TotalIncome);
            Assert.Equal(1000m, result.GrandTotalIncome);
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
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 1000m);

            await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "AB-ESTADO-1",
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
            ICreditNoteService creditNotes = services.GetRequiredService<ICreditNoteService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 500m);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            InvoiceModel invoiceModel = (await invoices.GetAsync(receivable.InvoiceId))!;
            CreditNoteInvoiceOption eligible = (await creditNotes.GetInvoiceAsync(receivable.InvoiceId))!;
            CreditNoteRegistrationResult registration = await creditNotes.RegisterAsync(
                new RegisterCreditNoteCommand(
                    receivable.InvoiceId,
                    invoiceModel.IssueDate,
                    "Factura registrada por error",
                    null,
                    null,
                    eligible.InvoiceVersion),
                actorId,
                actorIsManagement: true);
            Assert.True(registration.Operation.IsSuccess);

            IReportService reports = services.GetRequiredService<IReportService>();
            CustomerStatementReportResult result = await reports.GetCustomerStatementReportAsync(
                customerId, "Usuario de prueba");

            Assert.Equal(2, result.Lines.Count);
            CustomerStatementLine invoiceLine = Assert.Single(
                result.Lines,
                line => line.MovementType == "Factura a crédito");
            Assert.True(invoiceLine.IsCancelled);
            CustomerStatementLine creditNoteLine = Assert.Single(
                result.Lines,
                line => line.MovementType == "Nota de crédito");
            Assert.Equal(500m, creditNoteLine.Amount);
            Assert.False(creditNoteLine.IsCancelled);
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
            await PrepareFabricatedPeriodAsync(
                services, actorId, new DateOnly(2010, 1, 10), new DateOnly(2010, 2, 10), new DateOnly(2010, 3, 10));

            Guid earlierPayableId = await CreateConfirmedCreditPayableAsync(
                services, expenses, supplierId, actorId, new DateOnly(2010, 1, 10), amount: 300m);
            Guid laterPayableId = await CreateConfirmedCreditPayableAsync(
                services, expenses, supplierId, actorId, new DateOnly(2010, 2, 10), amount: 200m);
            Guid toBePaidPayableId = await CreateConfirmedCreditPayableAsync(
                services, expenses, supplierId, actorId, new DateOnly(2010, 3, 10), amount: 150m);

            // El pago se registra con la fecha real de hoy, fuera del período fiscal fabricado de
            // 2010: se restaura la cobertura de hoy antes de registrarlo.
            await PrepareFabricatedPeriodAsync(services, actorId, DateOnly.FromDateTime(DateTime.UtcNow));

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

            // Vencimientos de 2010: a la fecha de ejecución de la prueba, ambas caen en "más de 90 días".
            Assert.Equal(0m, line.Aging.Current);
            Assert.Equal(0m, line.Aging.Overdue1To30);
            Assert.Equal(0m, line.Aging.Overdue31To60);
            Assert.Equal(0m, line.Aging.Overdue61To90);
            Assert.Equal(500m, line.Aging.OverdueOver90);
            Assert.Equal(500m, result.GrandTotalAging.OverdueOver90);

            Assert.Equal(2, line.Details.Count);
            Assert.Equal(new DateOnly(2010, 2, 9), line.Details[0].DueDate); // La más antigua primero.
            Assert.Equal(300m, line.Details[0].Balance);
            Assert.Equal(new DateOnly(2010, 3, 12), line.Details[1].DueDate);
            Assert.Equal(200m, line.Details[1].Balance);
            Assert.All(line.Details, detail => Assert.NotEmpty(detail.ExpenseCode));

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

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetReceivableByCustomerReportAsync_GroupsVigentReceivablesByCustomerAndExcludesPaidOnes()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService receivablePayments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);
            await PrepareFabricatedPeriodAsync(
                services, actorId, new DateOnly(2010, 1, 10), new DateOnly(2010, 2, 10), new DateOnly(2010, 3, 10));

            Guid earlierReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, 300m, new DateOnly(2010, 1, 10));
            Guid laterReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, 200m, new DateOnly(2010, 2, 10));
            Guid toBePaidReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, 150m, new DateOnly(2010, 3, 10));

            // El abono se registra con la fecha real de hoy, fuera del período fiscal fabricado de
            // 2010: se restaura la cobertura de hoy antes de registrarlo.
            await PrepareFabricatedPeriodAsync(services, actorId, DateOnly.FromDateTime(DateTime.UtcNow));

            await receivablePayments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "ABONO-ESTADO-1", DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(toBePaidReceivableId, 150m, null)]),
                actorId,
                actorIsManagement: false);

            IReportService reports = services.GetRequiredService<IReportService>();
            ReceivableByCustomerReportResult result = await reports.GetReceivableByCustomerReportAsync("Usuario de prueba");

            ReceivableByCustomerReportLine line = Assert.Single(result.Lines);
            Assert.Equal(customerId, line.CustomerId);
            Assert.Equal(2, line.ReceivableCount);
            Assert.Equal(500m, line.TotalBalance);
            Assert.Equal(new DateOnly(2010, 2, 9), line.EarliestDueDate); // Vencimiento = emisión + 30 días (Crédito 30).
            Assert.Equal(500m, result.GrandTotalBalance);

            // Vencimientos de 2010: a la fecha de ejecución de la prueba, ambas caen en "más de 90 días".
            Assert.Equal(0m, line.Aging.Current);
            Assert.Equal(0m, line.Aging.Overdue1To30);
            Assert.Equal(0m, line.Aging.Overdue31To60);
            Assert.Equal(0m, line.Aging.Overdue61To90);
            Assert.Equal(500m, line.Aging.OverdueOver90);
            Assert.Equal(500m, result.GrandTotalAging.OverdueOver90);

            Assert.Equal(2, line.Details.Count);
            Assert.Equal(new DateOnly(2010, 2, 9), line.Details[0].DueDate); // La más antigua primero.
            Assert.Equal(300m, line.Details[0].Balance);
            Assert.Equal(new DateOnly(2010, 3, 12), line.Details[1].DueDate);
            Assert.Equal(200m, line.Details[1].Balance);
            Assert.All(line.Details, detail => Assert.NotNull(detail.InvoiceNumber));

            _ = earlierReceivableId;
            _ = laterReceivableId;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetReceivableByCustomerReportAsync_WithNoReceivables_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();
            ReceivableByCustomerReportResult result = await reports.GetReceivableByCustomerReportAsync("Usuario de prueba");

            Assert.Empty(result.Lines);
            Assert.Equal(0m, result.GrandTotalBalance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetAnnualBudgetReportAsync_AccumulatesUpToTheGivenMonthAndKeepsTheAnnualReference()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid accountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            const int year = 2011;
            await budgets.SetAmountAsync(new SetBudgetAmountCommand(accountId, year, 1, 500m), version: null, actorId);
            await budgets.SetAmountAsync(new SetBudgetAmountCommand(accountId, year, 2, 500m), version: null, actorId);
            // Marzo es posterior a upToMonth (2): solo debe contar para el presupuesto anual de referencia.
            await budgets.SetAmountAsync(new SetBudgetAmountCommand(accountId, year, 3, 1000m), version: null, actorId);

            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, cashAccountId, new DateOnly(year, 1, 5), 600m, actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, accountId, taxTypeId, cashAccountId, new DateOnly(year, 2, 5), 900m, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string accountCode = (await context.Set<LedgerAccount>().AsNoTracking().SingleAsync(c => c.Id == accountId)).Code;

            IReportService reports = services.GetRequiredService<IReportService>();
            AnnualBudgetReportResult result = await reports.GetAnnualBudgetReportAsync(year, 2, "Usuario de prueba");

            AnnualBudgetReportLine line = result.Lines.Single(l => l.AccountCode == accountCode);
            Assert.Equal(1000m, line.AccumulatedBudgeted); // enero + febrero.
            Assert.Equal(1500m, line.AccumulatedExecuted); // 600 + 900, IVA 0%.
            Assert.Equal(2000m, line.AnnualBudgeted); // enero + febrero + marzo.
            Assert.Equal(0m, line.AvailableAmount); // ejecutado superó lo presupuestado acumulado.
            Assert.Equal(500m, line.VarianceAmount);
            Assert.Equal(0.5m, line.VariancePercent);
            Assert.Equal(BudgetExecutionStatus.Exceeded, line.ExecutionStatus);
            Assert.Equal("Presupuesto anual a la fecha", result.Header.ReportName);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetAnnualBudgetReportAsync_UsesConfiguredFiscalMonths()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            DateOnly today = DateOnly.FromDateTime(
                services.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);
            DateOnly fiscalStart = new(today.Year, today.Month, 1);
            DateOnly fiscalEnd = fiscalStart.AddYears(1).AddDays(-1);
            DateOnly secondFiscalMonth = fiscalStart.AddMonths(1);

            ISystemParametersService settings = services.GetRequiredService<ISystemParametersService>();
            SystemParametersModel? existingSettings = await settings.GetAsync();
            Assert.True((existingSettings is null
                ? await settings.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId, actorIsManagement: true)
                : await settings.UpdateAsync(new SaveSystemParametersCommand(500_000m, 7), existingSettings.Version, actorId, actorIsManagement: true)).IsSuccess);
            SystemParametersModel configured = (await settings.GetAsync())!;
            Assert.True((await settings.UpdateFiscalPeriodAsync(
                fiscalStart, fiscalEnd, null, configured.Version, actorId)).IsSuccess);

            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid accountId = await CreateCategoryAsync(
                services, actorId, LedgerAccountType.Expense);
            Assert.True((await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(accountId, fiscalStart.Year, fiscalStart.Month, 1_000m),
                version: null,
                actorId)).IsSuccess);
            Assert.True((await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(accountId, secondFiscalMonth.Year, secondFiscalMonth.Month, 2_000m),
                version: null,
                actorId)).IsSuccess);

            IReportService reports = services.GetRequiredService<IReportService>();
            AnnualBudgetReportResult result = await reports.GetAnnualBudgetReportAsync(
                fiscalStart.Year, 1, "Usuario de prueba");

            AnnualBudgetReportLine line = Assert.Single(result.Lines);
            Assert.Equal(1_000m, line.AccumulatedBudgeted);
            Assert.Equal(3_000m, line.AnnualBudgeted);
            Assert.Contains(
                $"{fiscalStart:dd/MM/yyyy}–{fiscalEnd:dd/MM/yyyy}",
                result.Header.ParametersDescription,
                StringComparison.Ordinal);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetCashFlowReportAsync_SumsInflowsAndOutflowsPerMonthAndFiltersByCashAccount()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid expenseAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid otherCashAccountId = await CreateCashAccountAsync(services, actorId);
            await PrepareFabricatedPeriodAsync(
                services, actorId, new DateOnly(2012, 1, 10), new DateOnly(2012, 1, 20));

            await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, cashAccountId, 500m, new DateOnly(2012, 1, 10),
                    PaymentMethod.Cash, "REC-CF-1", "Entrada de prueba de flujo de efectivo"),
                actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, expenseAccountId, taxTypeId, cashAccountId, new DateOnly(2012, 1, 20), 300m, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();

            CashFlowReportResult allAccounts = await reports.GetCashFlowReportAsync(
                new DateOnly(2012, 1, 1), new DateOnly(2012, 1, 31), cashAccountId: null, "Usuario de prueba");
            CashFlowReportLine monthLine = Assert.Single(allAccounts.Lines);
            Assert.Equal(500m, monthLine.TotalInflow);
            Assert.Equal(300m, monthLine.TotalOutflow); // IVA 0%.
            Assert.Equal(200m, monthLine.Net);

            CashFlowReportResult sameAccount = await reports.GetCashFlowReportAsync(
                new DateOnly(2012, 1, 1), new DateOnly(2012, 1, 31), cashAccountId, "Usuario de prueba");
            Assert.Equal(500m, sameAccount.GrandTotalInflow);
            Assert.Equal(300m, sameAccount.GrandTotalOutflow);

            CashFlowReportResult otherAccount = await reports.GetCashFlowReportAsync(
                new DateOnly(2012, 1, 1), new DateOnly(2012, 1, 31), otherCashAccountId, "Usuario de prueba");
            Assert.Equal(0m, otherAccount.GrandTotalInflow);
            Assert.Equal(0m, otherAccount.GrandTotalOutflow);
            Assert.Equal("Flujo de efectivo mensual", allAccounts.Header.ReportName);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetVatPeriodReportAsync_GroupsConfirmedInvoicesAndExpensesByTaxCodeAndExcludesDraftsAndCancelled()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid expenseAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType taxType = new(
                Guid.NewGuid(), $"IVA-{suffix}", "IVA 13% de pruebas de IVA del período", 13m, "08", null, DateTimeOffset.UtcNow, actorId);
            context.TaxTypes.Add(taxType);
            await context.SaveChangesAsync();

            DateOnly entryDate = new(2013, 5, 10);
            await PrepareFabricatedPeriodAsync(services, actorId, entryDate);

            // Factura confirmada de contado: neto 1000, IVA 130.
            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, entryDate, PaymentTerm.Cash), actorId);
            Guid invoiceId = createdInvoice.InvoiceId!.Value;
            InvoiceModel invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio con IVA de prueba de IVA del período");
            await invoices.AddLineAsync(
                invoiceId, new InvoiceLineInputCommand(catalogItemId, 1m, 1000m, 0m, taxType.Id), invoiceDraft.Version, actorId);
            invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-IVA-1"), invoiceDraft.Version, actorId);

            // Factura en borrador con la misma tarifa: no debe aparecer.
            InvoiceCreationResult draftOnlyInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, entryDate, PaymentTerm.Cash), actorId);
            InvoiceModel draftOnlyModel = (await invoices.GetAsync(draftOnlyInvoice.InvoiceId!.Value))!;
            await invoices.AddLineAsync(
                draftOnlyInvoice.InvoiceId!.Value,
                new InvoiceLineInputCommand(catalogItemId, 1m, 5000m, 0m, taxType.Id),
                draftOnlyModel.Version,
                actorId);

            // Gasto confirmado: neto 400, IVA 52.
            ExpenseCreationResult createdExpense = await expenses.CreateAsync(
                new SaveExpenseCommand(
                    supplierId, expenseAccountId, "Compra con IVA de prueba de IVA del período", "VC-IVA-1",
                    entryDate, PaymentTerm.Cash, 400m, taxType.Id, true, PaymentMethod.Cash),
                actorId);
            Guid expenseId = createdExpense.ExpenseId!.Value;
            ExpenseModel expenseDraft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REF-IVA-1"), expenseDraft.Version, actorId, actorIsManagement: true);

            // Gasto confirmado y luego anulado con la misma tarifa: no debe aparecer.
            ExpenseCreationResult cancelledExpense = await expenses.CreateAsync(
                new SaveExpenseCommand(
                    supplierId, expenseAccountId, "Compra anulada de prueba de IVA del período", "VC-IVA-2",
                    entryDate, PaymentTerm.Cash, 9999m, taxType.Id, true, PaymentMethod.Cash),
                actorId);
            ExpenseModel cancelledDraft = (await expenses.GetAsync(cancelledExpense.ExpenseId!.Value))!;
            await expenses.ConfirmAsync(
                cancelledExpense.ExpenseId!.Value, new ConfirmExpenseCommand(cashAccountId, "REF-IVA-2"),
                cancelledDraft.Version, actorId, actorIsManagement: true);
            ExpenseModel confirmedCancelled = (await expenses.GetAsync(cancelledExpense.ExpenseId!.Value))!;
            await expenses.CancelAsync(
                cancelledExpense.ExpenseId!.Value, "Error de captura", confirmedCancelled.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            VatPeriodReportResult result = await reports.GetVatPeriodReportAsync(entryDate, entryDate, "Usuario de prueba");

            VatPeriodReportLine line = result.Lines.Single(l => l.TaxCode == taxType.Code);
            Assert.Equal(1000m, line.SalesBase);
            Assert.Equal(130m, line.VatCollected);
            Assert.Equal(400m, line.PurchasesBase);
            Assert.Equal(52m, line.VatPaid);
            Assert.Equal(78m, line.Difference);
            Assert.Equal("IVA del período", result.Header.ReportName);

            _ = draftOnlyInvoice;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetCashFlowReportAsync_IncludesTransfersOnlyWhenFilteredByAccountAndVatPaymentsAlwaysAsOutflow()
    {
        // Año fabricado (ver PrepareFabricatedPeriodAsync): no hay movimientos reales antes de él, así
        // que los montos esperados son absolutos, no relativos a una base medida.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            Guid mainAccountId = await CreateCashAccountAsync(services, actorId);
            Guid otherAccountId = await CreateCashAccountAsync(services, actorId);
            DateOnly transferDate = new(2014, 6, 15);
            DateOnly settlementDate = new(2014, 7, 5);
            await PrepareFabricatedPeriodAsync(services, actorId, transferDate, settlementDate);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            // Entrada manual de 1000 a mainAccount: a diferencia del ingreso que genera una factura de
            // contado (cuya fecha es la de la confirmación, no la de la factura), el ingreso manual
            // permite fijar la fecha, necesaria para que caiga dentro del rango fabricado de la prueba.
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, mainAccountId, 1000m, transferDate, PaymentMethod.Cash, "REC-CF-1", "Entrada de prueba de flujo de efectivo"),
                actorId);

            // Factura de contado con IVA, solo para generar el IVA por pagar de 130 que liquida la
            // prueba; su propio ingreso (fechado a la confirmación) no es lo que esta prueba mide.
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio con IVA de prueba de flujo de efectivo");

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType vatRate = new(
                Guid.NewGuid(), $"IVA-{suffix}", "IVA 13% de prueba de flujo de efectivo", 13m, "08", null, DateTimeOffset.UtcNow, actorId);
            context.TaxTypes.Add(vatRate);
            await context.SaveChangesAsync();

            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, transferDate, PaymentTerm.Cash), actorId);
            Guid invoiceId = createdInvoice.InvoiceId!.Value;
            InvoiceModel invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.AddLineAsync(
                invoiceId, new InvoiceLineInputCommand(catalogItemId, 1m, 1000m, 0m, vatRate.Id), invoiceDraft.Version, actorId);
            invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            CatalogOperationResult confirmed = await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(mainAccountId, PaymentMethod.Cash, "F-CF-1"), invoiceDraft.Version, actorId);
            Assert.True(confirmed.IsSuccess);

            // Traspaso de 300 de mainAccount a otherAccount.
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            RegisterCashTransferResult transfer = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(mainAccountId, otherAccountId, 300m, "TRA-CF-1", "Traspaso de prueba", transferDate),
                actorId,
                actorIsManagement: false);
            Assert.True(transfer.Operation.IsSuccess);

            // Liquidación de IVA del mes: paga los 130 desde mainAccount, con fecha del mes siguiente.
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            RegisterVatSettlementResult settlement = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    transferDate.Year, transferDate.Month, settlementDate, mainAccountId, "PAGO-CF-1"),
                actorId,
                actorIsManagement: true);
            Assert.True(settlement.Operation.IsSuccess);

            IReportService reports = services.GetRequiredService<IReportService>();
            DateOnly rangeFrom = new(2014, 6, 1);
            DateOnly rangeTo = new(2014, 7, 31);

            CashFlowReportResult noFilter = await reports.GetCashFlowReportAsync(rangeFrom, rangeTo, null, "Usuario de prueba");
            Assert.Equal(1000m, noFilter.GrandTotalInflow);
            // Sin filtro, el traspaso se compensa (no aparece); el pago de IVA sí resta, siempre.
            Assert.Equal(130m, noFilter.GrandTotalOutflow);
            Assert.All(noFilter.Lines, line => Assert.Equal(0m, line.TransfersReceived + line.TransfersSent));

            CashFlowReportResult mainFiltered = await reports.GetCashFlowReportAsync(rangeFrom, rangeTo, mainAccountId, "Usuario de prueba");
            Assert.Equal(1000m, mainFiltered.GrandTotalInflow);
            Assert.Equal(300m + 130m, mainFiltered.GrandTotalOutflow);
            CashFlowReportLine mainJune = mainFiltered.Lines.Single(line => line.PeriodMonth == 6);
            CashFlowReportLine mainJuly = mainFiltered.Lines.Single(line => line.PeriodMonth == 7);
            Assert.Equal(300m, mainJune.TransfersSent);
            Assert.Equal(0m, mainJune.TransfersReceived);
            Assert.Equal(130m, mainJuly.VatPayments);

            CashFlowReportResult otherFiltered = await reports.GetCashFlowReportAsync(rangeFrom, rangeTo, otherAccountId, "Usuario de prueba");
            Assert.Equal(300m, otherFiltered.GrandTotalInflow);
            Assert.Equal(0m, otherFiltered.GrandTotalOutflow);
            CashFlowReportLine otherJune = otherFiltered.Lines.Single(line => line.PeriodMonth == 6);
            Assert.Equal(300m, otherJune.TransfersReceived);
            Assert.Equal(0m, otherJune.TransfersSent);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetVatPeriodReportAsync_ForAMonthWithAVigentSettlement_IncludesItsCodeAndResultWithoutAlteringTotals()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio con IVA de prueba de liquidación en reporte");

            DateOnly entryDate = new(2015, 3, 10);
            DateOnly settlementDate = new(2015, 4, 5);
            await PrepareFabricatedPeriodAsync(services, actorId, entryDate, settlementDate);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType vatRate = new(
                Guid.NewGuid(), $"IVA-{suffix}", "IVA 13% de prueba de liquidación en reporte", 13m, "08", null, DateTimeOffset.UtcNow, actorId);
            context.TaxTypes.Add(vatRate);
            await context.SaveChangesAsync();

            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, entryDate, PaymentTerm.Cash), actorId);
            Guid invoiceId = createdInvoice.InvoiceId!.Value;
            InvoiceModel invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.AddLineAsync(
                invoiceId, new InvoiceLineInputCommand(catalogItemId, 1m, 1000m, 0m, vatRate.Id), invoiceDraft.Version, actorId);
            invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-VP-1"), invoiceDraft.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            DateOnly monthStart = new(entryDate.Year, entryDate.Month, 1);
            DateOnly monthEnd = monthStart.AddMonths(1).AddDays(-1);

            VatPeriodReportResult beforeSettlement = await reports.GetVatPeriodReportAsync(monthStart, monthEnd, "Usuario de prueba");
            Assert.Null(beforeSettlement.Settlement);
            Assert.Equal(130m, beforeSettlement.TotalVatCollected);

            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            RegisterVatSettlementResult registered = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(entryDate.Year, entryDate.Month, settlementDate, cashAccountId, "PAGO-VP-1"),
                actorId,
                actorIsManagement: true);
            Assert.True(registered.Operation.IsSuccess);

            VatPeriodReportResult afterSettlement = await reports.GetVatPeriodReportAsync(monthStart, monthEnd, "Usuario de prueba");
            Assert.NotNull(afterSettlement.Settlement);
            Assert.Matches("^LIQ-\\d{6}$", afterSettlement.Settlement!.Code);
            Assert.Equal(130m, afterSettlement.Settlement.PaymentAmount);
            Assert.Equal(0m, afterSettlement.Settlement.CreditCarriedForward);
            // Los totales del reporte nunca cambian por la liquidación: siguen viniendo solo de la factura.
            Assert.Equal(130m, afterSettlement.TotalVatCollected);
            Assert.Equal(0m, afterSettlement.TotalVatPaid);

            // Un rango que no es exactamente un mes calendario nunca trae liquidación, aunque la cubra.
            VatPeriodReportResult partialRange = await reports.GetVatPeriodReportAsync(
                monthStart, monthStart.AddDays(10), "Usuario de prueba");
            Assert.Null(partialRange.Settlement);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetWithholdingByCustomerReportAsync_ListsOnlyWithholdingsFromActivePayments()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid incomeAccountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableIdA = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 1000m);
            Guid receivableIdB = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 1000m);

            DateOnly paymentDateActive = new(2014, 4, 1);
            DateOnly paymentDateCancelled = new(2014, 4, 2);
            await PrepareFabricatedPeriodAsync(services, actorId, paymentDateActive, paymentDateCancelled);

            RegisterReceivablePaymentResult activeResult = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "AB-RET-1", paymentDateActive,
                    [new ReceivablePaymentAllocationCommand(receivableIdA, 980m, new WithholdingInputCommand(withholdingTypeId, 1000m))]),
                actorId,
                actorIsManagement: false);
            Assert.True(activeResult.Operation.IsSuccess);
            ReceivablePaymentModel activePayment = (await payments.GetAsync(activeResult.ReceivablePaymentIds[0]))!;

            RegisterReceivablePaymentResult cancelledResult = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "AB-RET-2", paymentDateCancelled,
                    [new ReceivablePaymentAllocationCommand(receivableIdB, 980m, new WithholdingInputCommand(withholdingTypeId, 1000m))]),
                actorId,
                actorIsManagement: false);
            Assert.True(cancelledResult.Operation.IsSuccess);
            ReceivablePaymentModel cancelledPayment = (await payments.GetAsync(cancelledResult.ReceivablePaymentIds[0]))!;
            await payments.CancelAsync(cancelledPayment.Id, "Error de captura", cancelledPayment.Version, actorId);

            IReportService reports = services.GetRequiredService<IReportService>();
            WithholdingByCustomerReportResult result = await reports.GetWithholdingByCustomerReportAsync(
                paymentDateActive, paymentDateCancelled, "Usuario de prueba");

            WithholdingByCustomerLine line = result.Lines.Single(l => l.ReceivablePaymentCode == activePayment.Code);
            Assert.Equal(1000m, line.Base);
            Assert.Equal(20m, line.Amount);
            Assert.DoesNotContain(result.Lines, l => l.ReceivablePaymentCode == cancelledPayment.Code);
            Assert.Equal("Retenciones aplicadas por clientes", result.Header.ReportName);
        });
    }

    private static async Task<Guid> CreateWithholdingTypeAsync(IServiceProvider services, Guid actorId, decimal rate)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        WithholdingType type = new(
            Guid.NewGuid(), $"RET-{suffix}", $"Renta {rate}% de pruebas de reportes", rate, null, null, DateTimeOffset.UtcNow, actorId);
        context.WithholdingTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeAccountId,
        Guid actorId,
        decimal amount,
        DateOnly? issueDate = null)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(
                customerId, incomeAccountId, issueDate ?? DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito de pruebas de reportes");
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, amount, 0m, taxTypeId),
            draft.Version,
            actorId);

        draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(
            invoiceId,
            new ConfirmInvoiceCommand(null, null, null),
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
        Guid accountId = await CreateCategoryAsync(services, actorId, LedgerAccountType.Expense);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, accountId, "Compra a crédito de pruebas de reportes", "VC-0001", issueDate, PaymentTerm.Credit30,
                amount, taxTypeId, true, PaymentMethod.Cash),
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

    private static async Task<Guid> CreateCustomerAsync(
        IServiceProvider services,
        Guid actorId,
        bool appliesStateWithholding = false)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de reportes",
            IdentificationType.ForeignNonResident,
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            appliesStateWithholding,
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
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            context.SystemParameters.Add(new SystemParameters(limit, 7, DateTimeOffset.UtcNow, actorId));
        }
        else
        {
            parameters.UpdateValues(limit, parameters.OverdueAlertDays, DateTimeOffset.UtcNow, actorId);
        }

        await context.SaveChangesAsync();
    }

    private static async Task<Guid> CreateAndConfirmCashExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid accountId,
        Guid taxTypeId,
        Guid cashAccountId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, accountId, "Gasto de prueba de reportes", "VC-0001", issueDate, PaymentTerm.Cash,
                netAmount, taxTypeId, true, PaymentMethod.Cash),
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
        Guid accountId,
        Guid taxTypeId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, accountId, "Borrador de pruebas de reportes", "VC-0001", issueDate, PaymentTerm.Cash,
                netAmount, taxTypeId, true, PaymentMethod.Cash),
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
            IdentificationType.ForeignNonResident,
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
            "01",
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateCatalogItemAsync(IServiceProvider services, Guid actorId, string name)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        CatalogItem item = new(
            Guid.NewGuid(),
            $"SRV-{suffix}",
            name,
            CatalogItemType.Service,
            "8311500000000",
            null,
            UnitOfMeasureCode.Unit,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
    }

    private static async Task<Guid> CreateCategoryAsync(
        IServiceProvider services,
        Guid actorId,
        LedgerAccountType kind)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"LA-{suffix}",
            "Cuenta de pruebas de reportes",
            kind,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(ledgerAccount);
        await context.SaveChangesAsync();

        return ledgerAccount.Id;
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

    /// <summary>
    /// Las fechas de negocio de este archivo son siempre de un año fabricado (pasado o futuro, nunca
    /// usado en datos reales) para que las aserciones de un período no dependan de qué otros datos
    /// existan ya en la base de desarrollo. La fecha de cierre automática (regla 16) y el período
    /// fiscal son más nuevos que estas pruebas: esto deja el año fabricado dentro de un período fiscal
    /// propio y reabre sus meses exactos, para que ninguno de los dos guardianes los bloquee.
    /// </summary>
    private static async Task PrepareFabricatedPeriodAsync(
        IServiceProvider services, Guid actorId, params DateOnly[] dates)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            parameters = new SystemParameters(500_000m, 7, DateTimeOffset.UtcNow, actorId);
            context.SystemParameters.Add(parameters);
        }

        DateOnly earliest = dates.Min();
        DateOnly fiscalStart = new(earliest.Year, earliest.Month, 1);
        parameters.UpdateFiscalPeriod(
            fiscalStart, fiscalStart.AddYears(1).AddDays(-1), DateTimeOffset.UtcNow, actorId);

        foreach (var yearMonth in dates.Select(d => (d.Year, d.Month)).Distinct())
        {
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                yearMonth.Year, yearMonth.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
        }

        await context.SaveChangesAsync();
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
