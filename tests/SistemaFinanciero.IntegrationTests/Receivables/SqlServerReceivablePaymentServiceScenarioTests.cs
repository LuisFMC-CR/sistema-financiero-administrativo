using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Receivables;

/// <summary>
/// Escenarios reales de <c>IReceivablePaymentService</c> que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Sigue el mismo patrón que
/// <c>SqlServerInvoiceServiceScenarioTests</c>.
/// </summary>
public sealed class SqlServerReceivablePaymentServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerReceivablePaymentServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_ToASingleInvoice_AppliesThePaymentAndCreatesItsIncome()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-0001",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(receivableId, 60m, null)]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.Single(result.ReceivablePaymentIds);
            Assert.False(result.DuplicateReferenceWarning);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(40m, receivable.Balance);
            Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);

            ReceivablePayment payment = await context.ReceivablePayments.SingleAsync(
                p => p.Id == result.ReceivablePaymentIds[0]);
            Assert.Equal(60m, payment.Amount);
            Assert.Equal(ReceivablePaymentStatus.Active, payment.Status);
            Assert.Matches("^ABO-\\d{6}$", payment.Code);

            Income income = await context.Incomes.SingleAsync(i => i.ReceivablePaymentId == payment.Id);
            Assert.Equal(IncomeOrigin.ReceivablePayment, income.Origin);
            Assert.Equal(60m, income.Amount);
            Assert.Null(income.PaymentMethod);
            Assert.Matches("^REC-\\d{6}$", income.Code);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAWithholding_ReducesTheBalanceByTheAmountPlusTheWithholding()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 1000m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-0002",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(
                            receivableId,
                            980m,
                            new WithholdingInputCommand(withholdingTypeId, 1000m)),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(0m, receivable.Balance);
            Assert.Equal(ReceivableStatus.Paid, receivable.Status);

            Withholding withholding = await context.Withholdings.SingleAsync(
                w => w.ReceivablePaymentId == result.ReceivablePaymentIds[0]);
            Assert.Equal(20m, withholding.Amount);

            // Paquete v9, bloque 2: el ingreso es el dinero recibido (980), sin la retención (20).
            Income income = await context.Incomes.SingleAsync(
                i => i.ReceivablePaymentId == result.ReceivablePaymentIds[0]);
            Assert.Equal(980m, income.Amount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithWithholding_IncomeIsTheReceivedAmountAndEntryBalances()
    {
        // Paquete v9, bloque 2 (CU-12, paso 7): abono de 98 000 con retención de 2 000 sobre una
        // cuenta de 100 000. El ingreso y el reporte de ingresos muestran 98 000; el saldo baja a 0;
        // el asiento debita efectivo 98 000 y 1.3.02 2 000, y acredita cuentas por cobrar 100 000.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            IReportService reports = services.GetRequiredService<IReportService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100_000m);
            DateOnly paymentDate = DateOnly.FromDateTime(
                services.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    $"AB-{Guid.NewGuid():N}"[..20],
                    paymentDate,
                    [
                        new ReceivablePaymentAllocationCommand(
                            receivableId,
                            98_000m,
                            new WithholdingInputCommand(withholdingTypeId, 100_000m)),
                    ]),
                actorId,
                actorIsManagement: false);
            Assert.True(result.Operation.IsSuccess, result.Operation.Message);
            Guid paymentId = result.ReceivablePaymentIds[0];

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Income income = await context.Incomes.SingleAsync(i => i.ReceivablePaymentId == paymentId);
            Assert.Equal(98_000m, income.Amount);

            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(0m, receivable.Balance);

            Guid withholdingAccountId = await context.LedgerAccounts
                .Where(account => account.SystemRole == LedgerAccountSystemRole.WithholdingsReceivable)
                .Select(account => account.Id)
                .SingleAsync();
            Guid receivablesAccountId = await context.LedgerAccounts
                .Where(account => account.SystemRole == LedgerAccountSystemRole.ReceivablesFromCustomers)
                .Select(account => account.Id)
                .SingleAsync();
            JournalEntryTraceModel trace = (await journal.GetTraceAsync(
                JournalEntrySourceType.ReceivablePayment, paymentId))!;
            IReadOnlyList<JournalEntryLineModel> lines = trace.Entry.Lines;
            Assert.Equal(98_000m, lines.Single(line => line.AccountId == cashAccountId).Debit);
            Assert.Equal(2_000m, lines.Single(line => line.AccountId == withholdingAccountId).Debit);
            Assert.Equal(100_000m, lines.Single(line => line.AccountId == receivablesAccountId).Credit);

            IncomeReportResult report = await reports.GetIncomeReportAsync(paymentDate, paymentDate, "Pruebas");
            Assert.Equal(98_000m, report.Lines.Single(line => line.Code == income.Code).Amount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAWithholdingForACustomerWithoutTheStateWithholdingMark_IsRejected()
    {
        // Bloque 1 del paquete v7, RF-95: el servidor rechaza la retención aunque se envíe, si el
        // cliente no tiene la marca de retención del sector público.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: false);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 1000m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-RET-REJ-1",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(
                            receivableId, 980m, new WithholdingInputCommand(withholdingTypeId, 1000m)),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(1000m, receivable.Balance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAWithholdingBelowTheMinimumBase_IsRejected()
    {
        // Bloque 1 del paquete v7, RF-95: el neto de la factura debe superar la base mínima del tipo.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(
                services, actorId, rate: 2m, minimumBase: 462_200m);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            // El neto de la factura (1000) no supera la base mínima (462 200).
            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 1000m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-RET-REJ-2",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(
                            receivableId, 980m, new WithholdingInputCommand(withholdingTypeId, 1000m)),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_ToSeveralInvoicesOfTheSameCustomer_SharesTheGroupIdAndCancelsIndividually()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid firstReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);
            Guid secondReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-0003",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(firstReceivableId, 100m, null),
                        new ReceivablePaymentAllocationCommand(secondReceivableId, 50m, null),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.Equal(2, result.ReceivablePaymentIds.Count);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            ReceivablePayment firstPayment = await context.ReceivablePayments.SingleAsync(
                p => p.ReceivableId == firstReceivableId);
            ReceivablePayment secondPayment = await context.ReceivablePayments.SingleAsync(
                p => p.ReceivableId == secondReceivableId);
            Assert.Equal(firstPayment.GroupId, secondPayment.GroupId);

            // Anular uno no debe afectar al otro (regla 2: "cada uno se anula de forma individual").
            ReceivablePaymentModel? firstModel = await payments.GetAsync(firstPayment.Id);
            CatalogOperationResult cancel = await payments.CancelAsync(
                firstPayment.Id,
                "Abono registrado por error",
                firstModel!.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            Receivable firstReceivable = await context.Receivables.SingleAsync(r => r.Id == firstReceivableId);
            Assert.Equal(100m, firstReceivable.Balance);
            Assert.Equal(ReceivableStatus.Pending, firstReceivable.Status);

            Income firstIncome = await context.Incomes.SingleAsync(i => i.ReceivablePaymentId == firstPayment.Id);
            Assert.Equal(IncomeStatus.Cancelled, firstIncome.Status);

            Receivable secondReceivable = await context.Receivables.SingleAsync(r => r.Id == secondReceivableId);
            Assert.Equal(50m, secondReceivable.Balance);
            Assert.Equal(ReceivableStatus.PartiallyPaid, secondReceivable.Status);

            ReceivablePayment unaffectedSecondPayment = await context.ReceivablePayments.SingleAsync(
                p => p.Id == secondPayment.Id);
            Assert.Equal(ReceivablePaymentStatus.Active, unaffectedSecondPayment.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_AboveTheAuthorizationLimitWithoutManagement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 50m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);

            RegisterReceivablePaymentCommand command = new(
                customerId,
                cashAccountId,
                "AB-0004",
                DateOnly.FromDateTime(DateTime.UtcNow),
                [new ReceivablePaymentAllocationCommand(receivableId, 60m, null)]);

            RegisterReceivablePaymentResult blocked = await payments.RegisterAsync(
                command,
                actorId,
                actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Operation.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable stillPending = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(100m, stillPending.Balance);

            RegisterReceivablePaymentResult allowed = await payments.RegisterAsync(
                command,
                actorId,
                actorIsManagement: true);
            Assert.True(allowed.Operation.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid firstReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);
            Guid secondReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);

            RegisterReceivablePaymentResult first = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-REPETIDA",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(firstReceivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);
            Assert.True(first.Operation.IsSuccess);
            Assert.False(first.DuplicateReferenceWarning);

            RegisterReceivablePaymentResult second = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-REPETIDA",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(secondReceivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);

            Assert.True(second.Operation.IsSuccess);
            Assert.True(second.DuplicateReferenceWarning);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WithAPaymentDateOnOrBeforeTheClosingDate_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);

            // Un mes anterior al actual se cierra automáticamente (regla 16); no hace falta ningún
            // AccountingPeriodOverride, que solo sirve para REABRIR un mes ya cerrado, nunca para
            // cerrar el mes actual.
            DateOnly lastMonth = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "AB-CIERRE-1", lastMonth,
                    [new ReceivablePaymentAllocationCommand(receivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable unchanged = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(100m, unchanged.Balance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_WhenThePaymentDateIsNowClosed_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeAccountId, actorId, amount: 100m);

            // Un mes pasado está cerrado por defecto: se reabre para poder registrar el abono, y se
            // vuelve a cerrar antes de intentar anularlo, para probar el rechazo con el reloj real.
            DateOnly paymentDate = PastDateWithinAReopenableMonth();
            await ReopenMonthAsync(services, actorId, paymentDate);

            RegisterReceivablePaymentResult registered = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "AB-CIERRE-2", paymentDate,
                    [new ReceivablePaymentAllocationCommand(receivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);
            Assert.True(registered.Operation.IsSuccess);
            ReceivablePaymentModel? payment = await payments.GetAsync(registered.ReceivablePaymentIds[0]);

            await CloseMonthAsync(services, actorId, paymentDate);

            CatalogOperationResult cancel = await payments.CancelAsync(
                payment!.Id, "Abono registrado por error", payment.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, cancel.Status);
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable unchanged = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(50m, unchanged.Balance);
        });
    }

    /// <summary>Un día de un mes genuinamente pasado respecto al reloj real, dentro del año fiscal.</summary>
    private static DateOnly PastDateWithinAReopenableMonth()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new DateOnly(today.Year, today.Month, 1).AddMonths(-1).AddDays(14);
    }

    private static async Task ReopenMonthAsync(IServiceProvider services, Guid actorId, DateOnly date)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            context.SystemParameters.Add(new SystemParameters(10_000m, 7, DateTimeOffset.UtcNow, actorId));
        }

        context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
            date.Year, date.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
        await context.SaveChangesAsync();
    }

    private static async Task CloseMonthAsync(IServiceProvider services, Guid actorId, DateOnly date)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        AccountingPeriodOverride override_ = await context.AccountingPeriodOverrides
            .SingleAsync(period => period.Year == date.Year && period.Month == date.Month);
        override_.Close(DateTimeOffset.UtcNow, actorId);
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeAccountId,
        Guid actorId,
        decimal amount)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
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
            "Cliente de pruebas de abonos",
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

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId, string ratePercent)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            $"IVA {ratePercent}%",
            decimal.Parse(ratePercent, System.Globalization.CultureInfo.InvariantCulture),
            "08",
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

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Caja de pruebas de abonos",
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

    private static async Task<Guid> CreateIncomeCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"ING-{suffix}",
            "Cuenta de ingreso de pruebas de abonos",
            LedgerAccountType.Income,
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

    private static async Task<Guid> CreateWithholdingTypeAsync(
        IServiceProvider services,
        Guid actorId,
        decimal rate,
        decimal? minimumBase = null)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        WithholdingType type = new(
            Guid.NewGuid(),
            $"RET-{suffix}",
            $"Renta {rate}%",
            rate,
            minimumBase,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.WithholdingTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
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
