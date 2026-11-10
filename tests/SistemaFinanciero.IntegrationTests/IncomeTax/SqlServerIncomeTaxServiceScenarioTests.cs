using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.IncomeTax;

/// <summary>
/// Escenarios reales de <c>IIncomeTaxService</c> (RF-99) que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Mismo patrón que
/// <c>SqlServerCashTransferServiceScenarioTests</c>. La estimación usa el período fiscal 2031, que no
/// tiene datos reales, con un asiento fabricado de montos conocidos.
/// </summary>
public sealed class SqlServerIncomeTaxServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private static readonly DateOnly EstimatePeriodStart = new(2031, 1, 1);
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerIncomeTaxServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterPaymentAsync_PostsDebitPrepaidTaxCreditCashAndLogsIt()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);

            RegisterIncomeTaxPaymentResult result = await incomeTax.RegisterPaymentAsync(
                Command(cashAccountId, 150_000m, "REC-HAC-0001"),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.False(result.DuplicateReferenceWarning);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            IncomeTaxPayment payment = await context.IncomeTaxPayments.SingleAsync(p => p.Id == result.PaymentId);
            Assert.Matches("^PIR-\\d{6}$", payment.Code);
            Assert.Equal(TodayFiscalPeriodStart(), payment.FiscalPeriodStartDate);
            Assert.Equal(TodayFiscalPeriodStart().AddYears(1).AddDays(-1), payment.FiscalPeriodEndDate);

            Guid prepaidAccountId = await context.LedgerAccounts
                .Where(account => account.SystemRole == LedgerAccountSystemRole.IncomeTaxPrepaid)
                .Select(account => account.Id)
                .SingleAsync();
            JournalEntry entry = await context.JournalEntries
                .Include(e => e.Lines)
                .SingleAsync(e => e.SourceType == JournalEntrySourceType.IncomeTaxPayment && e.SourceId == payment.Id);
            Assert.Equal(prepaidAccountId, entry.Lines.Single(line => line.Debit > 0).AccountId);
            Assert.Equal(cashAccountId, entry.Lines.Single(line => line.Credit > 0).AccountId);
            Assert.All(entry.Lines, line => Assert.Equal(150_000m, line.Debit + line.Credit));

            Assert.True(await context.OperationLogEntries.AnyAsync(log =>
                log.EntityType == OperationLogEntityType.IncomeTaxPayment
                && log.EntityId == payment.Id
                && log.Action == OperationLogAction.Confirmed));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterPaymentAsync_AboveTheAuthorizationLimitWithoutManagement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 100_000m);
            RegisterIncomeTaxPaymentCommand command = Command(cashAccountId, 150_000m, "REC-HAC-0002");

            RegisterIncomeTaxPaymentResult blocked = await incomeTax.RegisterPaymentAsync(
                command, actorId, actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Operation.Status);

            RegisterIncomeTaxPaymentResult allowed = await incomeTax.RegisterPaymentAsync(
                command, actorId, actorIsManagement: true);
            Assert.True(allowed.Operation.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterPaymentAsync_WithAReferenceAlreadyUsedInTheCashAccount_WarnsButSucceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);

            await incomeTax.RegisterPaymentAsync(Command(cashAccountId, 10_000m, "REC-HAC-0003"), actorId, false);
            RegisterIncomeTaxPaymentResult second = await incomeTax.RegisterPaymentAsync(
                Command(cashAccountId, 20_000m, "REC-HAC-0003"), actorId, false);

            Assert.True(second.Operation.IsSuccess);
            Assert.True(second.DuplicateReferenceWarning);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelPaymentAsync_MarksItCancelledAndPostsTheReversal()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);
            RegisterIncomeTaxPaymentResult registered = await incomeTax.RegisterPaymentAsync(
                Command(cashAccountId, 50_000m, "REC-HAC-0004"), actorId, false);
            IncomeTaxPaymentModel? model = await incomeTax.GetPaymentAsync(registered.PaymentId!.Value);

            CatalogOperationResult cancelled = await incomeTax.CancelPaymentAsync(
                model!.Id, "Monto digitado por error", model.Version, actorId);

            Assert.True(cancelled.IsSuccess);
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            IncomeTaxPayment payment = await context.IncomeTaxPayments.AsNoTracking().SingleAsync(p => p.Id == model.Id);
            Assert.Equal(IncomeTaxPaymentStatus.Cancelled, payment.Status);

            List<JournalEntry> entries = await context.JournalEntries
                .Include(e => e.Lines)
                .Where(e => e.SourceType == JournalEntrySourceType.IncomeTaxPayment && e.SourceId == model.Id)
                .ToListAsync();
            JournalEntry reversal = Assert.Single(entries, e => e.ReversesEntryId != null);
            Assert.Equal(cashAccountId, reversal.Lines.Single(line => line.Debit > 0).AccountId);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetEstimateAsync_AppliesTheBracketsAndSubtractsWithholdingsAndPayments()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);
            await PostSaleWithWithholdingAsync(services, actorId, cashAccountId, new DateOnly(2031, 3, 15));

            // El servicio rechaza fechas fuera del período fiscal vigente (cierre mensual), así que el
            // pago de 2031 se inserta directamente: aquí solo se prueba la agregación de la estimación.
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            context.IncomeTaxPayments.Add(new IncomeTaxPayment(
                Guid.NewGuid(),
                $"PIR-T{Guid.NewGuid():N}"[..14],
                EstimatePeriodStart,
                EstimatePeriodStart.AddYears(1).AddDays(-1),
                cashAccountId,
                PaymentMethod.BankTransfer,
                300_000m,
                "REC-HAC-2031",
                "Pago parcial",
                new DateOnly(2031, 6, 30),
                DateTimeOffset.UtcNow,
                actorId));
            await context.SaveChangesAsync();

            IncomeTaxEstimateReportResult? estimate = await incomeTax.GetEstimateAsync(EstimatePeriodStart, "Prueba");

            Assert.NotNull(estimate);
            Assert.True(estimate.HasData);
            Assert.Equal(10_000_000m, estimate.GrossIncome);
            Assert.Equal(10_000_000m, estimate.NetIncome);
            Assert.Equal(IncomeTaxRegime.Reduced, estimate.Regime);
            Assert.Equal(797_300m, estimate.EstimatedTax);
            Assert.Equal(200_000m, estimate.Withholdings);
            Assert.Equal(300_000m, estimate.PaymentsTotal);
            Assert.Equal(297_300m, estimate.AmountDue);
            Assert.Equal("Estimación del impuesto sobre la renta", estimate.Header.ReportName);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetEstimateAsync_ForAPeriodWithoutMovements_HasNoData()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);

            IncomeTaxEstimateReportResult? estimate = await incomeTax.GetEstimateAsync(new DateOnly(2032, 2, 1), "Prueba");

            Assert.NotNull(estimate);
            Assert.False(estimate.HasData);
            Assert.Equal(0m, estimate.EstimatedTax);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task UpdateScaleAsync_ReplacesTheScaleAndKeepsItsHistory()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeTaxService incomeTax = services.GetRequiredService<IIncomeTaxService>();
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await SetAuthorizationLimitAsync(services, actorId, 1_000_000m);
            IncomeTaxScaleModel? scale = (await parameters.GetAsync())?.IncomeTaxScale;
            Assert.NotNull(scale);

            CatalogOperationResult invalid = await incomeTax.UpdateScaleAsync(
                new SaveIncomeTaxScaleCommand(9_000_000m, 8_433_000m, 11_243_000m, 5m, 10m, 15m, 20m, 119_174_000m, 30m),
                scale.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, invalid.Status);

            CatalogOperationResult updated = await incomeTax.UpdateScaleAsync(
                new SaveIncomeTaxScaleCommand(6_000_000m, 9_000_000m, 12_000_000m, 5m, 10m, 15m, 20m, 125_000_000m, 30m),
                scale.Version,
                actorId);
            Assert.True(updated.IsSuccess);

            IncomeTaxScaleModel? reloaded = (await parameters.GetAsync())?.IncomeTaxScale;
            Assert.Equal(6_000_000m, reloaded!.Bracket1UpperLimit);
            Assert.Equal(125_000_000m, reloaded.GrossIncomeThreshold);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Assert.True(await context.IncomeTaxScaleChanges.AnyAsync(change =>
                change.ChangedByUserId == actorId && change.Bracket1UpperLimit == 6_000_000m));
        });
    }

    private static RegisterIncomeTaxPaymentCommand Command(Guid cashAccountId, decimal amount, string reference) =>
        new(
            TodayFiscalPeriodStart(),
            cashAccountId,
            PaymentMethod.BankTransfer,
            amount,
            reference,
            null,
            DateOnly.FromDateTime(DateTime.Now));

    private static DateOnly TodayFiscalPeriodStart() => new(DateTime.Now.Year, 1, 1);

    /// <summary>
    /// Asiento fabricado de una venta de ₡10.000.000 cobrada con retención: débito efectivo ₡9.800.000,
    /// débito retenciones ₡200.000, crédito una cuenta de ingreso de detalle ₡10.000.000.
    /// </summary>
    private static async Task PostSaleWithWithholdingAsync(
        IServiceProvider services,
        Guid actorId,
        Guid cashAccountId,
        DateOnly entryDate)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        Guid incomeAccountId = await context.LedgerAccounts
            .Where(account => account.Type == LedgerAccountType.Income && account.IsMovement && account.ParentId != null)
            .OrderBy(account => account.Code)
            .Select(account => account.Id)
            .FirstAsync();
        Guid withholdingAccountId = await context.LedgerAccounts
            .Where(account => account.SystemRole == LedgerAccountSystemRole.WithholdingsReceivable)
            .Select(account => account.Id)
            .SingleAsync();
        // ToListAsync no compone la consulta: SQL Server no admite NEXT VALUE FOR dentro de una subconsulta.
        List<long> numbers = await context.Database
            .SqlQueryRaw<long>("SELECT NEXT VALUE FOR finanzas.Seq_AsientoNumero AS [Value]")
            .ToListAsync();
        long number = numbers.Single();

        context.JournalEntries.Add(new JournalEntry(
            Guid.NewGuid(),
            number,
            entryDate,
            "Venta de prueba con retención",
            JournalEntrySourceType.Income,
            Guid.NewGuid(),
            reversesEntryId: null,
            [
                new JournalEntryLineInput(cashAccountId, 9_800_000m, 0m),
                new JournalEntryLineInput(withholdingAccountId, 200_000m, 0m),
                new JournalEntryLineInput(incomeAccountId, 0m, 10_000_000m),
            ],
            DateTimeOffset.UtcNow,
            actorId));
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Banco de pruebas de renta",
            LedgerAccountType.Asset,
            null,
            CashAccountKind.Bank,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
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
