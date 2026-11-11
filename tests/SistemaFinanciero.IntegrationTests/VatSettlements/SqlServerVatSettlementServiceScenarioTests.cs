using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.VatSettlements;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.VatSettlements;

/// <summary>
/// Escenarios reales de <c>IVatSettlementService</c> (paquete v8, bloque 2) que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerVatSettlementServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerVatSettlementServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_ComputesThePayableAndCreditableFromJournalMovementsSinceTheBeginning()
    {
        // El cálculo de A y B es acumulado desde siempre (menos liquidaciones anteriores vigentes),
        // a propósito, para que lo que se registre tarde ruede solo a la siguiente liquidación (ver
        // RegisterAsync_LateEntryDatedInAnAlreadySettledMonth_RollsIntoTheNextSettlement). Por eso la
        // base de desarrollo, con movimientos reales de sesiones anteriores, nunca parte de cero: la
        // prueba mide esa base ANTES de crear sus propios documentos y calcula lo esperado sobre esa
        // base, en vez de asumir montos absolutos. Se usa Gerencia para no depender del límite de
        // autorización, que no es lo que esta prueba verifica.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            DateOnly periodDate = await ReopenAPastMonthAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            VatSettlementPreview baseline = await settlements.PreviewAsync(periodDate.Year, periodDate.Month);

            await CreateConfirmedCashInvoiceAsync(services, actorId, periodDate, cashAccountId, netAmount: 1000m, taxRate: "13");
            await CreateConfirmedCashExpenseAsync(
                services, actorId, periodDate, cashAccountId, netAmount: 300m, taxRate: "13", vatCreditable: true);

            decimal expectedPayable = baseline.VatPayableAmount + 130m;
            decimal expectedCreditAvailable = baseline.VatCreditAvailable + 39m;
            (decimal expectedCreditApplied, decimal expectedPayment, decimal expectedCarry) =
                ExpectedAllocation(expectedPayable, expectedCreditAvailable);

            VatSettlementPreview preview = await settlements.PreviewAsync(periodDate.Year, periodDate.Month);
            Assert.True(preview.CanRegister);
            Assert.Equal(expectedPayable, preview.VatPayableAmount);
            Assert.Equal(expectedCreditApplied, preview.VatCreditApplied);
            Assert.Equal(expectedPayment, preview.PaymentAmount);
            Assert.Equal(expectedCarry, preview.CreditCarriedForward);

            RegisterVatSettlementResult result = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    periodDate.Year,
                    periodDate.Month,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    expectedPayment > 0 ? cashAccountId : null,
                    expectedPayment > 0 ? "PAGO-0001" : null),
                actorId,
                actorIsManagement: true);

            Assert.True(result.Operation.IsSuccess, result.Operation.Message);
            Assert.NotNull(result.VatSettlementId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            VatSettlement settlement = await context.VatSettlements.SingleAsync(s => s.Id == result.VatSettlementId);
            Assert.Equal(expectedPayable, settlement.VatPayableAmount);
            Assert.Equal(expectedCreditApplied, settlement.VatCreditApplied);
            Assert.Equal(expectedPayment, settlement.PaymentAmount);
            Assert.Equal(expectedCarry, settlement.CreditCarriedForward);
            Assert.Matches("^LIQ-\\d{6}$", settlement.Code);

            JournalEntry entry = await context.JournalEntries
                .Include(e => e.Lines)
                .SingleAsync(e => e.SourceType == JournalEntrySourceType.VatSettlement && e.SourceId == settlement.Id);
            Assert.Equal(expectedPayment > 0 ? 3 : 2, entry.Lines.Count);
            Assert.Equal(expectedPayable, entry.Lines.Sum(l => l.Debit));
            Assert.Equal(expectedPayable, entry.Lines.Sum(l => l.Credit));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WhenThereIsNoVatPayable_IsRejectedWithoutRegistering()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();

            VatSettlementPreview preview = await settlements.PreviewAsync(1999, 1);
            Assert.False(preview.CanRegister);
            Assert.NotNull(preview.BlockedMessage);

            RegisterVatSettlementResult result = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(1999, 1, new DateOnly(1999, 2, 1), null, null),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_LateEntryDatedInAnAlreadySettledMonth_RollsIntoTheNextSettlement()
    {
        // La primera liquidación absorbe TODO lo acumulado hasta su corte (real, de sesiones
        // anteriores, o de esta prueba), pero entre el corte del primer período y el del segundo
        // también puede haber actividad real nueva (por ejemplo, de pruebas manuales hechas este
        // mismo mes). Por eso se mide la base de AMBOS períodos antes de crear nada: la diferencia
        // entre esas dos bases es exactamente el crecimiento real ajeno a esta prueba, y lo que debe
        // quedar pendiente en la segunda liquidación es esa diferencia más la entrada tardía. Se usa
        // Gerencia para no depender del límite de autorización, que tampoco es lo que esta prueba
        // verifica.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            DateOnly periodDate = await ReopenAPastMonthAsync(services, actorId);
            DateOnly nextPeriodDate = periodDate.AddMonths(1);

            VatSettlementPreview firstBaseline = await settlements.PreviewAsync(periodDate.Year, periodDate.Month);
            VatSettlementPreview secondBaseline = await settlements.PreviewAsync(nextPeriodDate.Year, nextPeriodDate.Month);
            decimal realGrowthBetweenPeriods = secondBaseline.VatPayableAmount - firstBaseline.VatPayableAmount;

            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);

            await CreateConfirmedCashInvoiceAsync(services, actorId, periodDate, cashAccountId, netAmount: 1000m, taxRate: "13");

            RegisterVatSettlementResult first = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    periodDate.Year, periodDate.Month, DateOnly.FromDateTime(DateTime.UtcNow), cashAccountId, "PAGO-OCT"),
                actorId,
                actorIsManagement: true);
            Assert.True(first.Operation.IsSuccess, first.Operation.Message);

            // Entra tarde, con fecha del mes ya liquidado: el mes sigue reabierto, así que se puede
            // registrar; su efecto debe pasar automáticamente a la siguiente liquidación, nunca a la
            // que ya se cerró.
            await CreateConfirmedCashInvoiceAsync(services, actorId, periodDate, cashAccountId, netAmount: 500m, taxRate: "13");

            DateOnly nextSettlementDate = new DateOnly(nextPeriodDate.Year, nextPeriodDate.Month, 1).AddMonths(1);

            decimal expectedSecondPayable = realGrowthBetweenPeriods + 65m;
            VatSettlementPreview secondPreview = await settlements.PreviewAsync(nextPeriodDate.Year, nextPeriodDate.Month);
            Assert.Equal(expectedSecondPayable, secondPreview.VatPayableAmount);

            RegisterVatSettlementResult second = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    nextPeriodDate.Year, nextPeriodDate.Month, nextSettlementDate, cashAccountId, "PAGO-NOV"),
                actorId,
                actorIsManagement: true);

            Assert.True(second.Operation.IsSuccess, second.Operation.Message);
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            VatSettlement secondSettlement = await context.VatSettlements.SingleAsync(s => s.Id == second.VatSettlementId);
            // Lo exacto a verificar es que la entrada tardía quedó incluida una sola vez, en la
            // segunda liquidación y no en la primera; cuánto de eso se paga en vez de aplicarse como
            // crédito depende del IVA acreditable real disponible, que esta prueba no controla.
            Assert.Equal(expectedSecondPayable, secondSettlement.VatPayableAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_ForAPeriodEqualOrEarlierThanTheLastActiveSettlement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();

            DateOnly periodDate = await ReopenAPastMonthAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await CreateConfirmedCashInvoiceAsync(services, actorId, periodDate, cashAccountId, netAmount: 100m, taxRate: "13");

            RegisterVatSettlementResult first = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    periodDate.Year, periodDate.Month, DateOnly.FromDateTime(DateTime.UtcNow), cashAccountId, "PAGO-0001"),
                actorId,
                actorIsManagement: true);
            Assert.True(first.Operation.IsSuccess, first.Operation.Message);

            RegisterVatSettlementResult repeated = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    periodDate.Year, periodDate.Month, DateOnly.FromDateTime(DateTime.UtcNow), cashAccountId, "PAGO-0002"),
                actorId,
                actorIsManagement: true);

            Assert.Equal(CatalogOperationStatus.Invalid, repeated.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_AboveTheAuthorizationLimitWithoutManagement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            await SetAuthorizationLimitAsync(services, actorId, 50m);

            DateOnly periodDate = await ReopenAPastMonthAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await CreateConfirmedCashInvoiceAsync(services, actorId, periodDate, cashAccountId, netAmount: 1000m, taxRate: "13");

            RegisterVatSettlementCommand command = new(
                periodDate.Year, periodDate.Month, DateOnly.FromDateTime(DateTime.UtcNow), cashAccountId, "PAGO-0001");

            RegisterVatSettlementResult blocked = await settlements.RegisterAsync(command, actorId, actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Operation.Status);

            RegisterVatSettlementResult allowed = await settlements.RegisterAsync(command, actorId, actorIsManagement: true);
            Assert.True(allowed.Operation.IsSuccess, allowed.Operation.Message);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WithASettlementDateInAClosedMonth_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            DateOnly periodDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-2).AddDays(14);
            await ReopenMonthAsync(services, actorId, periodDate);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await CreateConfirmedCashInvoiceAsync(services, actorId, periodDate, cashAccountId, netAmount: 100m, taxRate: "13");

            // La fecha de liquidación cae un mes después del período (pasa el corte), pero ese mes
            // también está cerrado por defecto respecto al reloj real.
            DateOnly cutoff = new DateOnly(periodDate.Year, periodDate.Month, 1).AddMonths(1).AddDays(-1);
            DateOnly closedSettlementDate = cutoff.AddDays(15);

            RegisterVatSettlementResult result = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(periodDate.Year, periodDate.Month, closedSettlementDate, cashAccountId, "PAGO-0001"),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_OnlyTheMostRecentActiveSettlement_CanBeCancelled()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();

            DateOnly firstPeriod = await ReopenAPastMonthAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await CreateConfirmedCashInvoiceAsync(services, actorId, firstPeriod, cashAccountId, netAmount: 100m, taxRate: "13");

            RegisterVatSettlementResult first = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    firstPeriod.Year, firstPeriod.Month, DateOnly.FromDateTime(DateTime.UtcNow), cashAccountId, "PAGO-0001"),
                actorId,
                actorIsManagement: true);
            Assert.True(first.Operation.IsSuccess, first.Operation.Message);

            DateOnly secondPeriod = firstPeriod.AddMonths(1);
            DateOnly secondSettlementDate = new DateOnly(secondPeriod.Year, secondPeriod.Month, 1).AddMonths(1);
            await CreateConfirmedCashInvoiceAsync(services, actorId, secondPeriod, cashAccountId, netAmount: 200m, taxRate: "13");

            RegisterVatSettlementResult second = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(
                    secondPeriod.Year, secondPeriod.Month, secondSettlementDate, cashAccountId, "PAGO-0002"),
                actorId,
                actorIsManagement: true);
            Assert.True(second.Operation.IsSuccess, second.Operation.Message);

            VatSettlementModel? firstModel = await settlements.GetAsync(first.VatSettlementId!.Value);
            CatalogOperationResult cancelFirst = await settlements.CancelAsync(
                first.VatSettlementId.Value, "Intento fuera de orden", firstModel!.Version, actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, cancelFirst.Status);

            VatSettlementModel? secondModel = await settlements.GetAsync(second.VatSettlementId!.Value);
            CatalogOperationResult cancelSecond = await settlements.CancelAsync(
                second.VatSettlementId.Value, "Liquidación registrada por error", secondModel!.Version, actorId);
            Assert.True(cancelSecond.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            bool hasReversal = await context.JournalEntries.AnyAsync(e =>
                e.SourceType == JournalEntrySourceType.VatSettlement
                && e.SourceId == second.VatSettlementId.Value
                && e.ReversesEntryId != null);
            Assert.True(hasReversal);
        });
    }

    /// <summary>
    /// Replica la misma fórmula de reparto que usa <c>VatSettlementService</c>, para calcular lo
    /// esperado a partir de una base medida en vez de un monto absoluto fabricado.
    /// </summary>
    private static (decimal CreditApplied, decimal PaymentAmount, decimal CarriedForward) ExpectedAllocation(
        decimal vatPayableAmount, decimal vatCreditAvailable)
    {
        return vatPayableAmount > vatCreditAvailable
            ? (vatCreditAvailable, vatPayableAmount - vatCreditAvailable, 0m)
            : (vatPayableAmount, 0m, vatCreditAvailable - vatPayableAmount);
    }

    /// <summary>Reabre un mes pasado único (no usado por otras pruebas) para poder crear documentos.</summary>
    private static async Task<DateOnly> ReopenAPastMonthAsync(IServiceProvider services, Guid actorId)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        DateOnly periodDate = new DateOnly(today.Year, today.Month, 1).AddMonths(-1).AddDays(14);
        await ReopenMonthAsync(services, actorId, periodDate);
        return periodDate;
    }

    private static async Task ReopenMonthAsync(IServiceProvider services, Guid actorId, DateOnly date)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        bool alreadyReopened = await context.AccountingPeriodOverrides
            .AnyAsync(period => period.Year == date.Year && period.Month == date.Month);

        if (!alreadyReopened)
        {
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                date.Year, date.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
            await context.SaveChangesAsync();
        }
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

    private static async Task<Guid> CreateConfirmedCashInvoiceAsync(
        IServiceProvider services,
        Guid actorId,
        DateOnly issueDate,
        Guid cashAccountId,
        decimal netAmount,
        string taxRate)
    {
        IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
        Guid customerId = await CreateCustomerAsync(services, actorId);
        Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, taxRate);
        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId);

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeAccountId, issueDate, PaymentTerm.Cash), actorId);
        Guid invoiceId = created.InvoiceId!.Value;
        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, netAmount, 0m, taxTypeId),
            draft.Version,
            actorId);
        draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(
            invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, $"F-{Guid.NewGuid():N}"[..10]), draft.Version, actorId);

        return invoiceId;
    }

    private static async Task<Guid> CreateConfirmedCashExpenseAsync(
        IServiceProvider services,
        Guid actorId,
        DateOnly issueDate,
        Guid cashAccountId,
        decimal netAmount,
        string taxRate,
        bool vatCreditable)
    {
        IExpenseService expenses = services.GetRequiredService<IExpenseService>();
        Guid supplierId = await CreateSupplierAsync(services, actorId);
        Guid expenseAccountId = await CreateExpenseCategoryAsync(services, actorId);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, taxRate);

        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, expenseAccountId, "Compra de prueba de liquidación de IVA", $"VC-{Guid.NewGuid():N}"[..8],
                issueDate, PaymentTerm.Cash, netAmount, taxTypeId, vatCreditable, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;
        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(cashAccountId, $"REF-{Guid.NewGuid():N}"[..8]),
            draft.Version,
            actorId,
            actorIsManagement: false);

        return expenseId;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de liquidación de IVA",
            IdentificationType.ForeignNonResident,
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de liquidación de IVA",
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

    private static async Task<Guid> CreateCatalogItemAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        CatalogItem item = new(
            Guid.NewGuid(),
            $"SRV-{suffix}",
            "Servicio de pruebas de liquidación de IVA",
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

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId, string ratePercent)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            $"IVA {ratePercent}% de pruebas de liquidación",
            decimal.Parse(ratePercent, System.Globalization.CultureInfo.InvariantCulture),
            "08",
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Caja de pruebas de liquidación de IVA",
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
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"ING-{suffix}",
            "Cuenta de ingreso de pruebas de liquidación de IVA",
            LedgerAccountType.Income,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> CreateExpenseCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de pruebas de liquidación de IVA",
            LedgerAccountType.Expense,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
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

        // Aislamiento (paquete v9, bloque 9): una liquidación vigente de la base de desarrollo impide
        // liquidar los meses de la prueba; se anula solo dentro de esta transacción.
        await SqlTestIsolation.CancelExistingVatSettlementsAsync(scope.ServiceProvider, actorId);

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
