using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Parameters;

/// <summary>
/// Paquete v9, bloque 8 (RF-55 y CU-06): fuera del período fiscal vigente no se registran operaciones,
/// salvo en los meses aún abiertos del período inmediatamente anterior. Se prueba con ingresos
/// manuales (que pasan por <c>ClosingDateGuard</c>) y un reloj fijo en hora de Costa Rica. Se habilitan
/// con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten los datos.
/// </summary>
public sealed class SqlServerPreviousFiscalPeriodScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private static readonly DateOnly FiscalStart2027 = new(2027, 1, 1);
    private static readonly DateOnly FiscalEnd2027 = new(2027, 12, 31);
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerPreviousFiscalPeriodScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task OnJanuaryFifth_ClosedDecemberOfThePreviousPeriodOnlyAcceptsOperationsOnceReopened()
    {
        return RunAtLocalDateAsync(new DateOnly(2027, 1, 5), async (services, actorId, accounts) =>
        {
            CreateManualIncomeCommand Income(DateOnly date) => new(
                accounts.IncomeAccountId, accounts.CashAccountId, 1_000m, date, PaymentMethod.Cash,
                $"PER-{Guid.NewGuid():N}"[..12], "Reintegro de prueba");
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();

            // 20/12/2026: período anterior, mes cerrado automáticamente.
            IncomeCreationResult closedDecember = await incomes.CreateManualAsync(Income(new DateOnly(2026, 12, 20)), actorId);
            Assert.False(closedDecember.Operation.IsSuccess);
            Assert.Equal(
                "No es posible operar con fecha 20/12/2026: el mes ya está cerrado.",
                closedDecember.Operation.Message);

            // Gerencia reabre diciembre de 2026: ahora se acepta.
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                2026, 12, "Corrección del cierre anual", DateTimeOffset.UtcNow, actorId));
            await context.SaveChangesAsync();
            IncomeCreationResult reopenedDecember = await incomes.CreateManualAsync(Income(new DateOnly(2026, 12, 20)), actorId);
            Assert.True(reopenedDecember.Operation.IsSuccess, reopenedDecember.Operation.Message);

            // 15/11/2025: anterior al período anterior, se rechaza con el mensaje nuevo.
            IncomeCreationResult tooOld = await incomes.CreateManualAsync(Income(new DateOnly(2025, 11, 15)), actorId);
            Assert.False(tooOld.Operation.IsSuccess);
            Assert.Equal(
                "No es posible operar con fecha 15/11/2025: está fuera del período fiscal vigente y del período anterior.",
                tooOld.Operation.Message);

            // 05/01/2027: dentro del período vigente y en el mes actual.
            IncomeCreationResult today = await incomes.CreateManualAsync(Income(new DateOnly(2027, 1, 5)), actorId);
            Assert.True(today.Operation.IsSuccess, today.Operation.Message);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task OnDecemberTwentyEighth_WithTheNextPeriodAlreadyConfigured_TheCurrentMonthStaysOpen()
    {
        return RunAtLocalDateAsync(new DateOnly(2026, 12, 28), async (services, actorId, accounts) =>
        {
            IncomeCreationResult result = await services.GetRequiredService<IIncomeService>().CreateManualAsync(
                new CreateManualIncomeCommand(
                    accounts.IncomeAccountId, accounts.CashAccountId, 1_000m, new DateOnly(2026, 12, 28),
                    PaymentMethod.Cash, $"PER-{Guid.NewGuid():N}"[..12], "Reintegro de prueba"),
                actorId);

            Assert.True(result.Operation.IsSuccess, result.Operation.Message);
        });
    }

    /// <summary>
    /// Ejecuta el escenario con el reloj fijo al mediodía (hora de Costa Rica) de la fecha indicada y el
    /// período fiscal 01/01/2027–31/12/2027, dentro de una transacción que siempre se revierte. Quita
    /// las reaperturas de diciembre de 2026 que pudiera tener la base, para partir de un mes cerrado.
    /// </summary>
    private async Task RunAtLocalDateAsync(
        DateOnly localDate,
        Func<IServiceProvider, Guid, (Guid IncomeAccountId, Guid CashAccountId), Task> scenario)
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(EnableVariable),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        FixedLocalTimeProvider clock = FixedLocalTimeProvider.AtCostaRicaLocalTime(localDate.ToDateTime(new TimeOnly(12, 0)));
        await using WebApplicationFactory<Program> clockedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));

        using IServiceScope scope = clockedFactory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();

        try
        {
            Guid actorId = await CreateUserAsync(context);

            SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();
            if (parameters is null)
            {
                parameters = new SystemParameters(500_000m, 7, DateTimeOffset.UtcNow, actorId);
                context.SystemParameters.Add(parameters);
            }

            parameters.UpdateFiscalPeriod(FiscalStart2027, FiscalEnd2027, DateTimeOffset.UtcNow, actorId);
            await context.SaveChangesAsync();
            await context.AccountingPeriodOverrides
                .Where(period => period.Year == 2026 && period.Month == 12)
                .ExecuteDeleteAsync();

            Guid incomeAccountId = await CreateLedgerAccountAsync(context, actorId, LedgerAccountType.Income, null);
            Guid cashAccountId = await CreateLedgerAccountAsync(context, actorId, LedgerAccountType.Asset, CashAccountKind.Cash);

            await scenario(scope.ServiceProvider, actorId, (incomeAccountId, cashAccountId));
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static async Task<Guid> CreateLedgerAccountAsync(
        FinancialDbContext context, Guid actorId, LedgerAccountType type, CashAccountKind? cashKind)
    {
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(), $"PF-{suffix}", "Cuenta de prueba de período fiscal", type, null, cashKind, null, null,
            DateTimeOffset.UtcNow, actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> CreateUserAsync(FinancialDbContext context)
    {
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
}
