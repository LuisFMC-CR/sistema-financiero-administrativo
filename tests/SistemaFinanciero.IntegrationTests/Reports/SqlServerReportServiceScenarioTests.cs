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
    public Task GetMonthlyComparisonReportAsync_SourcesFromTheAsientosSoACreditSaleCountsWhenInvoicedNotWhenCollectedAndExcludesVat()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType taxType = new(Guid.NewGuid(), $"IVA-{suffix}", "IVA 13% de pruebas", 13m, "08", null, DateTimeOffset.UtcNow, actorId);
            context.TaxTypes.Add(taxType);
            await context.SaveChangesAsync();

            DateOnly saleDate = new(2027, 2, 10);
            DateOnly collectionDate = new(2027, 2, 20);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeCategoryId, saleDate, PaymentTerm.Credit30),
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
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 1000m);

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
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 500m);

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

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetAnnualBudgetReportAsync_AccumulatesUpToTheGivenMonthAndKeepsTheAnnualReference()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid categoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            const int year = 2011;
            await budgets.SetAmountAsync(new SetBudgetAmountCommand(categoryId, year, 1, 500m), version: null, actorId);
            await budgets.SetAmountAsync(new SetBudgetAmountCommand(categoryId, year, 2, 500m), version: null, actorId);
            // Marzo es posterior a upToMonth (2): solo debe contar para el presupuesto anual de referencia.
            await budgets.SetAmountAsync(new SetBudgetAmountCommand(categoryId, year, 3, 1000m), version: null, actorId);

            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, cashAccountId, new DateOnly(year, 1, 5), 600m, actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, cashAccountId, new DateOnly(year, 2, 5), 900m, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string categoryCode = (await context.FinancialCategories.AsNoTracking().SingleAsync(c => c.Id == categoryId)).Code;

            IReportService reports = services.GetRequiredService<IReportService>();
            AnnualBudgetReportResult result = await reports.GetAnnualBudgetReportAsync(year, 2, "Usuario de prueba");

            AnnualBudgetReportLine line = result.Lines.Single(l => l.CategoryCode == categoryCode);
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
    public Task GetCashFlowReportAsync_SumsInflowsAndOutflowsPerMonthAndFiltersByCashAccount()
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
            Guid otherCashAccountId = await CreateCashAccountAsync(services, actorId);

            await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeCategoryId, cashAccountId, 500m, new DateOnly(2012, 1, 10),
                    PaymentMethod.Cash, "REC-CF-1", "Entrada de prueba de flujo de efectivo"),
                actorId);
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, expenseCategoryId, taxTypeId, cashAccountId, new DateOnly(2012, 1, 20), 300m, actorId);

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
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid expenseCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            TaxType taxType = new(
                Guid.NewGuid(), $"IVA-{suffix}", "IVA 13% de pruebas de IVA del período", 13m, "08", null, DateTimeOffset.UtcNow, actorId);
            context.TaxTypes.Add(taxType);
            await context.SaveChangesAsync();

            DateOnly entryDate = new(2013, 5, 10);

            // Factura confirmada de contado: neto 1000, IVA 130.
            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeCategoryId, entryDate, PaymentTerm.Cash), actorId);
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
                new CreateInvoiceCommand(customerId, incomeCategoryId, entryDate, PaymentTerm.Cash), actorId);
            InvoiceModel draftOnlyModel = (await invoices.GetAsync(draftOnlyInvoice.InvoiceId!.Value))!;
            await invoices.AddLineAsync(
                draftOnlyInvoice.InvoiceId!.Value,
                new InvoiceLineInputCommand(catalogItemId, 1m, 5000m, 0m, taxType.Id),
                draftOnlyModel.Version,
                actorId);

            // Gasto confirmado: neto 400, IVA 52.
            ExpenseCreationResult createdExpense = await expenses.CreateAsync(
                new SaveExpenseCommand(
                    supplierId, expenseCategoryId, "Compra con IVA de prueba de IVA del período", "VC-IVA-1",
                    entryDate, PaymentTerm.Cash, 400m, taxType.Id, true, PaymentMethod.Cash),
                actorId);
            Guid expenseId = createdExpense.ExpenseId!.Value;
            ExpenseModel expenseDraft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REF-IVA-1"), expenseDraft.Version, actorId, actorIsManagement: true);

            // Gasto confirmado y luego anulado con la misma tarifa: no debe aparecer.
            ExpenseCreationResult cancelledExpense = await expenses.CreateAsync(
                new SaveExpenseCommand(
                    supplierId, expenseCategoryId, "Compra anulada de prueba de IVA del período", "VC-IVA-2",
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
    public Task GetWithholdingByCustomerReportAsync_ListsOnlyWithholdingsFromActivePayments()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid incomeCategoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Income);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableIdA = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 1000m);
            Guid receivableIdB = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 1000m);

            DateOnly paymentDateActive = new(2014, 4, 1);
            DateOnly paymentDateCancelled = new(2014, 4, 2);

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
        Guid incomeCategoryId,
        Guid actorId,
        decimal amount)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeCategoryId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
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
        Guid categoryId = await CreateCategoryAsync(services, actorId, FinancialCategoryKind.Expense);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Compra a crédito de pruebas de reportes", "VC-0001", issueDate, PaymentTerm.Credit30,
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
                supplierId, categoryId, "Gasto de prueba de reportes", "VC-0001", issueDate, PaymentTerm.Cash,
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
        Guid categoryId,
        Guid taxTypeId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Borrador de pruebas de reportes", "VC-0001", issueDate, PaymentTerm.Cash,
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
