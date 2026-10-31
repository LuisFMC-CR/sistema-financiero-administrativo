using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Audit;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Security;

/// <summary>
/// Escenarios reales de <c>ISecurityAuditService</c> que solo se habilitan explícitamente y siempre
/// revierten sus datos. La escritura de <c>SecurityAuditEvent</c> ya está probada por
/// <c>SqlServerUserAdministrationScenarioTests</c>; aquí se prueba únicamente la consulta de solo
/// lectura (orden, paginación de veinte filas y resolución de nombres). Como la base de desarrollo ya
/// tiene bitácora real de usos anteriores, las filas insertadas usan una fecha muy futura: así quedan
/// garantizadas como las más recientes sin asumir que la tabla esté vacía.
/// </summary>
public sealed class SqlServerSecurityAuditServiceScenarioTests
    : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private const string TargetFullName = "Cuenta Auditada de Prueba SQL";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerSecurityAuditServiceScenarioTests(FinancialWebApplicationFactory factory)
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
            ISecurityAuditService service = services.GetRequiredService<ISecurityAuditService>();
            Guid targetId = await CreateUserAsync(services, TargetFullName);

            int baselineCount = await context.SecurityAuditEvents.CountAsync();
            DateTimeOffset farFuture = DateTimeOffset.UtcNow.AddYears(10);

            for (int index = 1; index <= 24; index++)
            {
                context.SecurityAuditEvents.Add(new SecurityAuditEvent(
                    Guid.NewGuid(),
                    SecurityAuditAction.UserProfileUpdated,
                    actorId,
                    targetId,
                    farFuture.AddMinutes(index)));
            }

            context.SecurityAuditEvents.Add(new SecurityAuditEvent(
                Guid.NewGuid(),
                SecurityAuditAction.BootstrapAdministratorCreated,
                null,
                targetId,
                farFuture.AddMinutes(25),
                SystemRoles.Administrator));

            await context.SaveChangesAsync();

            PagedResult<SecurityAuditEntryModel> firstPage = await service.SearchAsync(
                new SecurityAuditQuery(page: 1));

            Assert.Equal(baselineCount + 25, firstPage.TotalCount);
            Assert.Equal(20, firstPage.Items.Count);
            AssertNonIncreasingOrder(firstPage.Items.Select(item => item.OccurredAtUtc).ToArray());

            SecurityAuditEntryModel mostRecent = firstPage.Items[0];
            Assert.Equal(TargetFullName, mostRecent.TargetFullName);
            Assert.Null(mostRecent.ActorFullName);
            Assert.Equal(SecurityAuditAction.BootstrapAdministratorCreated, mostRecent.Action);
            Assert.Equal(SystemRoles.Administrator, mostRecent.Role);

            PagedResult<SecurityAuditEntryModel> secondPage = await service.SearchAsync(
                new SecurityAuditQuery(page: 2));

            Assert.True(secondPage.Items.Count >= 5, "Deben quedar al menos cinco filas propias en la segunda página.");
            for (int index = 0; index < 5; index++)
            {
                Assert.Equal(TargetFullName, secondPage.Items[index].TargetFullName);
                Assert.True(secondPage.Items[index].OccurredAtUtc >= farFuture);
                Assert.NotNull(secondPage.Items[index].ActorFullName);
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

    private static async Task<Guid> CreateUserAsync(IServiceProvider services, string fullName)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        Guid userId = Guid.NewGuid();
        string email = $"sql-test-{userId:N}@example.test";

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            FullName = fullName,
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
        Guid actorId = await CreateUserAsync(scope.ServiceProvider, "Administrador de Prueba SQL");

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
