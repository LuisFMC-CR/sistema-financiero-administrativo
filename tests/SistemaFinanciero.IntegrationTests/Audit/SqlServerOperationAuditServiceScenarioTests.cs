using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Audit;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Audit;

/// <summary>
/// Escenarios reales de <c>IOperationAuditService</c> que solo se habilitan explícitamente y siempre
/// revierten sus datos. La escritura de <c>OperationLogEntry</c> ya está probada en cada servicio de
/// documento (facturas, gastos, ingresos, abonos, pagos, ajustes); aquí se prueba únicamente la
/// consulta de solo lectura (orden, paginación de veinte filas, motivo y resolución del usuario). Como
/// la base de desarrollo ya tiene bitácora real de usos anteriores, las filas insertadas usan una
/// fecha muy futura: así quedan garantizadas como las más recientes sin asumir que la tabla esté vacía.
/// </summary>
public sealed class SqlServerOperationAuditServiceScenarioTests
    : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private const string ActorFullName = "Administrador de Prueba SQL";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerOperationAuditServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SearchAsync_OrdersByMostRecentAndPaginatesInPagesOfTwenty()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            IOperationAuditService service = services.GetRequiredService<IOperationAuditService>();

            int baselineCount = await context.OperationLogEntries.CountAsync();
            DateTimeOffset farFuture = DateTimeOffset.UtcNow.AddYears(10);

            for (int index = 1; index <= 21; index++)
            {
                context.Add(new OperationLogEntry(
                    Guid.NewGuid(),
                    OperationLogEntityType.Expense,
                    Guid.NewGuid(),
                    OperationLogAction.Confirmed,
                    null,
                    farFuture.AddMinutes(index),
                    actorId));
            }

            Guid cancelledInvoiceId = Guid.NewGuid();
            context.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Invoice,
                cancelledInvoiceId,
                OperationLogAction.Cancelled,
                "Error en el cliente seleccionado",
                farFuture.AddMinutes(22),
                actorId));

            await context.SaveChangesAsync();

            PagedResult<OperationAuditEntryModel> firstPage = await service.SearchAsync(
                new OperationAuditQuery(page: 1));

            Assert.Equal(baselineCount + 22, firstPage.TotalCount);
            Assert.Equal(20, firstPage.Items.Count);
            AssertNonIncreasingOrder(firstPage.Items.Select(item => item.PerformedAtUtc).ToArray());

            OperationAuditEntryModel mostRecent = firstPage.Items[0];
            Assert.Equal(OperationLogEntityType.Invoice, mostRecent.EntityType);
            Assert.Equal(OperationLogAction.Cancelled, mostRecent.Action);
            Assert.Equal(cancelledInvoiceId, mostRecent.EntityId);
            Assert.Equal("Error en el cliente seleccionado", mostRecent.Reason);
            Assert.Equal(ActorFullName, mostRecent.PerformedByFullName);

            PagedResult<OperationAuditEntryModel> secondPage = await service.SearchAsync(
                new OperationAuditQuery(page: 2));

            Assert.True(secondPage.Items.Count >= 2, "Deben quedar al menos dos filas propias en la segunda página.");
            for (int index = 0; index < 2; index++)
            {
                Assert.Equal(OperationLogEntityType.Expense, secondPage.Items[index].EntityType);
                Assert.Null(secondPage.Items[index].Reason);
                Assert.Equal(ActorFullName, secondPage.Items[index].PerformedByFullName);
                Assert.True(secondPage.Items[index].PerformedAtUtc >= farFuture);
            }
        });
    }

    private static void AssertNonIncreasingOrder(DateTimeOffset[] timestamps)
    {
        for (int index = 1; index < timestamps.Length; index++)
        {
            Assert.True(
                timestamps[index - 1] >= timestamps[index],
                "La bitácora debe listarse de la más reciente a la más antigua.");
        }
    }

    private static async Task<Guid> CreateUserAsync(IServiceProvider services)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        Guid userId = Guid.NewGuid();
        string email = $"sql-test-{userId:N}@example.test";

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            FullName = ActorFullName,
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
