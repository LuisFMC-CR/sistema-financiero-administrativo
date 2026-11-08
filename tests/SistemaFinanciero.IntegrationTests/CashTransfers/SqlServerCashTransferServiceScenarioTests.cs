using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.CashTransfers;

/// <summary>
/// Escenarios reales de <c>ICashTransferService</c> (paquete v8, bloque 1) que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Sigue el mismo patrón que
/// <c>SqlServerSupplierPaymentServiceScenarioTests</c>, sin agrupación.
/// </summary>
public sealed class SqlServerCashTransferServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerCashTransferServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_BetweenTwoCashAccounts_PostsDebitDestinationCreditSource()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            Guid sourceId = await CreateCashAccountAsync(services, actorId);
            Guid destinationId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            RegisterCashTransferResult result = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    sourceId,
                    destinationId,
                    300m,
                    "DEP-0001",
                    "Depósito de caja chica al banco",
                    DateOnly.FromDateTime(DateTime.UtcNow)),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.NotNull(result.CashTransferId);
            Assert.False(result.DuplicateReferenceWarning);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            CashTransfer transfer = await context.CashTransfers.SingleAsync(t => t.Id == result.CashTransferId);
            Assert.Equal(300m, transfer.Amount);
            Assert.Equal(CashTransferStatus.Active, transfer.Status);
            Assert.Matches("^TRA-\\d{6}$", transfer.Code);

            JournalEntry entry = await context.JournalEntries
                .Include(e => e.Lines)
                .SingleAsync(e => e.SourceType == JournalEntrySourceType.CashTransfer && e.SourceId == transfer.Id);
            JournalEntryLine debit = entry.Lines.Single(line => line.Debit > 0);
            JournalEntryLine credit = entry.Lines.Single(line => line.Credit > 0);
            Assert.Equal(destinationId, debit.AccountId);
            Assert.Equal(300m, debit.Debit);
            Assert.Equal(sourceId, credit.AccountId);
            Assert.Equal(300m, credit.Credit);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WithTheSameSourceAndDestinationAccount_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            RegisterCashTransferResult result = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    cashAccountId,
                    cashAccountId,
                    100m,
                    "DEP-0002",
                    "Traspaso inválido",
                    DateOnly.FromDateTime(DateTime.UtcNow)),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_AboveTheAuthorizationLimitWithoutManagement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            Guid sourceId = await CreateCashAccountAsync(services, actorId);
            Guid destinationId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 50m);

            RegisterCashTransferCommand command = new(
                sourceId,
                destinationId,
                100m,
                "DEP-0003",
                "Traspaso sobre el límite",
                DateOnly.FromDateTime(DateTime.UtcNow));

            RegisterCashTransferResult blocked = await transfers.RegisterAsync(
                command, actorId, actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Operation.Status);

            RegisterCashTransferResult allowed = await transfers.RegisterAsync(
                command, actorId, actorIsManagement: true);
            Assert.True(allowed.Operation.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WithAReferenceAlreadyUsedInEitherAccount_WarnsButSucceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            Guid sourceId = await CreateCashAccountAsync(services, actorId);
            Guid destinationId = await CreateCashAccountAsync(services, actorId);
            Guid thirdAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            RegisterCashTransferResult first = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    sourceId, destinationId, 100m, "DEP-REPETIDA", "Primer traspaso",
                    DateOnly.FromDateTime(DateTime.UtcNow)),
                actorId,
                actorIsManagement: false);
            Assert.True(first.Operation.IsSuccess);
            Assert.False(first.DuplicateReferenceWarning);

            // Repite la referencia en la cuenta de destino del primer traspaso, ahora como origen.
            RegisterCashTransferResult second = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    destinationId, thirdAccountId, 50m, "DEP-REPETIDA", "Segundo traspaso",
                    DateOnly.FromDateTime(DateTime.UtcNow)),
                actorId,
                actorIsManagement: false);

            Assert.True(second.Operation.IsSuccess);
            Assert.True(second.DuplicateReferenceWarning);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WithATransferDateOnOrBeforeTheClosingDate_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            Guid sourceId = await CreateCashAccountAsync(services, actorId);
            Guid destinationId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            // Un mes anterior al actual se cierra automáticamente (regla 16); no hace falta ningún
            // AccountingPeriodOverride, que solo sirve para REABRIR un mes ya cerrado, nunca para
            // cerrar el mes actual.
            DateOnly lastMonth = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(-1);

            RegisterCashTransferResult result = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    sourceId, destinationId, 100m, "DEP-CIERRE-1", "Traspaso en mes cerrado", lastMonth),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_ReversesTheJournalEntryWithTheOriginalDate()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            Guid sourceId = await CreateCashAccountAsync(services, actorId);
            Guid destinationId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            RegisterCashTransferResult result = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    sourceId, destinationId, 200m, "DEP-0004", "Traspaso a anular",
                    DateOnly.FromDateTime(DateTime.UtcNow)),
                actorId,
                actorIsManagement: false);
            Assert.True(result.Operation.IsSuccess);

            CashTransferModel? registered = await transfers.GetAsync(result.CashTransferId!.Value);
            CatalogOperationResult cancel = await transfers.CancelAsync(
                result.CashTransferId.Value, "Traspaso registrado por error", registered!.Version, actorId);

            Assert.True(cancel.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            CashTransfer cancelled = await context.CashTransfers.SingleAsync(t => t.Id == result.CashTransferId);
            Assert.Equal(CashTransferStatus.Cancelled, cancelled.Status);

            bool hasReversal = await context.JournalEntries.AnyAsync(e =>
                e.SourceType == JournalEntrySourceType.CashTransfer
                && e.SourceId == cancelled.Id
                && e.ReversesEntryId != null);
            Assert.True(hasReversal);
        });
    }

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Caja de pruebas de traspasos",
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
