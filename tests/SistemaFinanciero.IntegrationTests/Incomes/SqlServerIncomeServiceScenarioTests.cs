using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Incomes;

/// <summary>
/// Escenarios reales de <c>IIncomeService</c> que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1
/// y siempre revierten datos. Sigue el mismo patrón que
/// <c>SqlServerReceivablePaymentServiceScenarioTests</c>.
/// </summary>
public sealed class SqlServerIncomeServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerIncomeServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithValidData_CreatesTheIncomeAndCancelAsyncCancelsIt()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            IncomeCreationResult created = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId,
                    cashAccountId,
                    15_000m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentMethod.BankTransfer,
                    "DEP-0099",
                    "Reintegro de un proveedor por un pago duplicado"),
                actorId);

            Assert.True(created.Operation.IsSuccess);
            Assert.NotNull(created.IncomeId);

            IncomeModel? income = await incomes.GetAsync(created.IncomeId!.Value);
            Assert.NotNull(income);
            Assert.Equal(IncomeOrigin.Manual, income!.Origin);
            Assert.Equal(IncomeStatus.Active, income.Status);
            Assert.Equal(15_000m, income.Amount);
            Assert.Equal("Reintegro de un proveedor por un pago duplicado", income.Description);
            Assert.Matches("^REC-\\d{6}$", income.Code);

            CatalogOperationResult cancel = await incomes.CancelAsync(
                income.Id,
                "Registrado por error",
                income.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            IncomeModel? cancelled = await incomes.GetAsync(income.Id);
            Assert.Equal(IncomeStatus.Cancelled, cancelled!.Status);
            Assert.Equal("Registrado por error", cancelled.CancellationReason);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithAnInactiveCashAccount_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            IncomeCreationResult created = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId,
                    Guid.NewGuid(),
                    15_000m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentMethod.BankTransfer,
                    "DEP-0099",
                    "Reintegro"),
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, created.Operation.Status);
            Assert.Null(created.IncomeId);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_OnAnAutomaticIncome_IsRejectedAndKeepsItActive()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = createdInvoice.InvoiceId!.Value;

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio de contado");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"),
                draft.Version,
                actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Income automaticIncome = await context.Incomes.SingleAsync(income => income.InvoiceId == invoiceId);
            IncomeModel model = (await incomes.GetAsync(automaticIncome.Id))!;

            CatalogOperationResult cancel = await incomes.CancelAsync(
                model.Id,
                "Intento de anular directo",
                model.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, cancel.Status);

            IncomeModel? stillActive = await incomes.GetAsync(model.Id);
            Assert.Equal(IncomeStatus.Active, stillActive!.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithAnIncomeDateOnOrBeforeTheClosingDate_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            // Un mes anterior al actual se cierra automáticamente (regla 16); no hace falta ningún
            // AccountingPeriodOverride, que solo sirve para REABRIR un mes ya cerrado, nunca para
            // cerrar el mes actual.
            DateOnly lastMonth = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);

            IncomeCreationResult closed = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, cashAccountId, 15_000m, lastMonth,
                    PaymentMethod.BankTransfer, "DEP-CIERRE", "Reintegro"),
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, closed.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task FiscalPeriodAndMonthlyClosure_AllowOnlyReopenedMonthsInsideTheFiscalRange()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ISystemParametersService settings = services.GetRequiredService<ISystemParametersService>();
            SaveSystemParametersCommand parametersCommand = new(500_000m, 7);
            SystemParametersModel? existingSettings = await settings.GetAsync();
            CatalogOperationResult parametersResult = existingSettings is null
                ? await settings.CreateAsync(parametersCommand, actorId)
                : await settings.UpdateAsync(parametersCommand, existingSettings.Version, actorId);
            Assert.True(parametersResult.IsSuccess);

            DateOnly currentMonth = DateOnly.FromDateTime(
                services.GetRequiredService<TimeProvider>().GetLocalNow().DateTime);
            DateOnly previousMonthStart = new DateOnly(currentMonth.Year, currentMonth.Month, 1).AddMonths(-1);
            DateOnly fiscalEnd = previousMonthStart.AddYears(1).AddDays(-1);
            SystemParametersModel currentSettings = (await settings.GetAsync())!;
            Assert.True((await settings.UpdateFiscalPeriodAsync(
                previousMonthStart,
                fiscalEnd,
                reason: null,
                currentSettings.Version,
                actorId)).IsSuccess);

            Assert.True((await settings.ReopenAccountingPeriodAsync(
                previousMonthStart.Year,
                previousMonthStart.Month,
                "Corrección de cierre",
                actorId)).IsSuccess);

            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            CreateManualIncomeCommand CreateCommand(DateOnly date, string reference) => new(
                incomeAccountId,
                cashAccountId,
                100m,
                date,
                PaymentMethod.Cash,
                reference,
                "Ingreso de prueba");

            IncomeCreationResult reopenedMonth = await incomes.CreateManualAsync(
                CreateCommand(previousMonthStart, "PERIODO-ABIERTO"),
                actorId);
            Assert.True(reopenedMonth.Operation.IsSuccess);

            ReopenedAccountingPeriodModel reopened = Assert.Single(
                await settings.GetReopenedAccountingPeriodsAsync());
            Assert.True((await settings.CloseAccountingPeriodAsync(
                reopened.Year, reopened.Month, reopened.Version, actorId)).IsSuccess);

            IncomeCreationResult closedMonth = await incomes.CreateManualAsync(
                CreateCommand(previousMonthStart, "PERIODO-CERRADO"),
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, closedMonth.Operation.Status);

            IncomeCreationResult outsideFiscalPeriod = await incomes.CreateManualAsync(
                CreateCommand(fiscalEnd.AddDays(1), "FUERA-FISCAL"),
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, outsideFiscalPeriod.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_WhenTheIncomeDateIsNowClosed_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            // Un mes pasado está cerrado por defecto: se reabre para poder crear el ingreso, y se
            // vuelve a cerrar antes de intentar anularlo, para probar el rechazo con el reloj real.
            DateOnly pastDate = PastDateWithinAReopenableMonth();
            await ReopenMonthAsync(services, actorId, pastDate);

            IncomeCreationResult created = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, cashAccountId, 15_000m, pastDate,
                    PaymentMethod.BankTransfer, "DEP-0099", "Reintegro"),
                actorId);
            IncomeModel income = (await incomes.GetAsync(created.IncomeId!.Value))!;

            await CloseMonthAsync(services, actorId, pastDate);

            CatalogOperationResult cancel = await incomes.CancelAsync(
                income.Id, "Registrado por error", income.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, cancel.Status);
            IncomeModel stillActive = (await incomes.GetAsync(income.Id))!;
            Assert.Equal(IncomeStatus.Active, stillActive.Status);
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
            context.SystemParameters.Add(new SystemParameters(500_000m, 7, DateTimeOffset.UtcNow, actorId));
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

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            IncomeCreationResult first = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, cashAccountId, 5_000m, DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentMethod.BankTransfer, "DEP-REPETIDO", "Primer reintegro"),
                actorId);
            Assert.True(first.Operation.IsSuccess);
            Assert.False(first.DuplicateReferenceWarning);

            IncomeCreationResult second = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, cashAccountId, 3_000m, DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentMethod.BankTransfer, "DEP-REPETIDO", "Segundo reintegro"),
                actorId);

            Assert.True(second.Operation.IsSuccess);
            Assert.True(second.DuplicateReferenceWarning);
        });
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de ingresos",
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
            "Caja de pruebas de ingresos",
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
            "Cuenta de ingreso de pruebas de ingresos",
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
