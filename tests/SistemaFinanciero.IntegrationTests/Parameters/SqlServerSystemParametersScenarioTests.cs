using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Parameters;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerSystemParametersScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerSystemParametersScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Scenario_CreatesUpdatesAndKeepsAHistoryOfEachChange()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();

            // Sin configurar todavía: no hay fila y GetAsync devuelve nulo.
            Assert.Null(await parameters.GetAsync());

            Assert.True((await parameters.CreateAsync(
                new SaveSystemParametersCommand(500_000m, 7), actorId)).IsSuccess);

            // Crear una segunda vez se rechaza: ya existe.
            CatalogOperationResult duplicate = await parameters.CreateAsync(
                new SaveSystemParametersCommand(1m, 1), actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);
            context.ChangeTracker.Clear();

            SystemParametersModel created = (await parameters.GetAsync())!;
            Assert.Equal(500_000m, created.AuthorizationLimitCrc);
            Assert.Equal(7, created.OverdueAlertDays);

            Assert.True((await parameters.UpdateAsync(
                new SaveSystemParametersCommand(750_000m, 10), created.Version, actorId)).IsSuccess);

            // Un token de concurrencia obsoleto se rechaza.
            CatalogOperationResult stale = await parameters.UpdateAsync(
                new SaveSystemParametersCommand(900_000m, 15), created.Version, actorId);
            Assert.Equal(CatalogOperationStatus.ConcurrencyConflict, stale.Status);

            SystemParametersModel current = (await parameters.GetAsync())!;
            Assert.Equal(750_000m, current.AuthorizationLimitCrc);
            Assert.Equal(10, current.OverdueAlertDays);

            // El historial conserva ambos cambios: la creación (sin valor anterior) y la edición.
            List<SystemParameterChange> history = await context.Set<SystemParameterChange>()
                .Where(change => change.ChangedByUserId == actorId)
                .OrderBy(change => change.ChangedAtUtc)
                .ToListAsync();

            Assert.Equal(2, history.Count);
            Assert.Null(history[0].PreviousAuthorizationLimitCrc);
            Assert.Equal(500_000m, history[0].NewAuthorizationLimitCrc);
            Assert.Equal(500_000m, history[1].PreviousAuthorizationLimitCrc);
            Assert.Equal(750_000m, history[1].NewAuthorizationLimitCrc);
            Assert.Equal(7, history[1].PreviousOverdueAlertDays);
            Assert.Equal(10, history[1].NewOverdueAlertDays);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Database_RejectsParametersThatBreakItsConstraints()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            Assert.True((await parameters.CreateAsync(
                new SaveSystemParametersCommand(500_000m, 7), actorId)).IsSuccess);
            context.ChangeTracker.Clear();

            Exception negativeLimit = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE finanzas.ParametrosSistema SET AuthorizationLimitCrc = -1 WHERE Id = {SystemParameters.SingletonId}"));
            Assert.Contains("CK_ParametrosSistema_AuthorizationLimit", negativeLimit.ToString());

            Exception zeroDays = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE finanzas.ParametrosSistema SET OverdueAlertDays = 0 WHERE Id = {SystemParameters.SingletonId}"));
            Assert.Contains("CK_ParametrosSistema_OverdueAlertDays", zeroDays.ToString());
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ParameterHistory_CannotBeModifiedOrDeletedOnceWritten()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            Assert.True((await parameters.CreateAsync(
                new SaveSystemParametersCommand(500_000m, 7), actorId)).IsSuccess);

            SystemParameterChange entry = await context.Set<SystemParameterChange>()
                .SingleAsync(change => change.ChangedByUserId == actorId);

            context.Remove(entry);
            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => context.SaveChangesAsync());
            Assert.Contains("historial de parámetros es inmutable", error.Message);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ChangeClosingDateAsync_AdvancingClosesThePeriodWithoutARequiredReason()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await parameters.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId);
            SystemParametersModel created = (await parameters.GetAsync())!;
            Assert.Equal(DateOnly.MinValue, created.ClosingDate);

            DateOnly newClosingDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1);
            CatalogOperationResult result = await parameters.ChangeClosingDateAsync(
                newClosingDate, reason: null, created.Version, actorId);

            Assert.True(result.IsSuccess);
            SystemParametersModel current = (await parameters.GetAsync())!;
            Assert.Equal(newClosingDate, current.ClosingDate);

            ClosingDateChange logEntry = await context.Set<ClosingDateChange>()
                .SingleAsync(change => change.ChangedByUserId == actorId);
            Assert.Equal(DateOnly.MinValue, logEntry.PreviousClosingDate);
            Assert.Equal(newClosingDate, logEntry.NewClosingDate);
            Assert.Null(logEntry.Reason);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ChangeClosingDateAsync_RejectsGoingBackwardsWithoutAReason()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await parameters.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId);
            SystemParametersModel created = (await parameters.GetAsync())!;

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            await parameters.ChangeClosingDateAsync(today.AddDays(-10), reason: null, created.Version, actorId);
            SystemParametersModel closed = (await parameters.GetAsync())!;

            CatalogOperationResult result = await parameters.ChangeClosingDateAsync(
                today.AddDays(-20), reason: null, closed.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
            SystemParametersModel unchanged = (await parameters.GetAsync())!;
            Assert.Equal(today.AddDays(-10), unchanged.ClosingDate);

            _ = context;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ChangeClosingDateAsync_GoingBackwardsWithAReasonReopensAndLogsIt()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await parameters.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId);
            SystemParametersModel created = (await parameters.GetAsync())!;

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            await parameters.ChangeClosingDateAsync(today.AddDays(-10), reason: null, created.Version, actorId);
            SystemParametersModel closed = (await parameters.GetAsync())!;

            CatalogOperationResult result = await parameters.ChangeClosingDateAsync(
                today.AddDays(-20), "Reapertura para corregir un gasto registrado por error", closed.Version, actorId);

            Assert.True(result.IsSuccess);
            SystemParametersModel reopened = (await parameters.GetAsync())!;
            Assert.Equal(today.AddDays(-20), reopened.ClosingDate);

            ClosingDateChange reopenEntry = await context.Set<ClosingDateChange>()
                .SingleAsync(change => change.NewClosingDate == today.AddDays(-20));
            Assert.Equal(today.AddDays(-10), reopenEntry.PreviousClosingDate);
            Assert.Equal("Reapertura para corregir un gasto registrado por error", reopenEntry.Reason);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ChangeClosingDateAsync_RejectsADateLaterThanToday()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await parameters.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId);
            SystemParametersModel created = (await parameters.GetAsync())!;

            CatalogOperationResult result = await parameters.ChangeClosingDateAsync(
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), reason: null, created.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
            _ = context;
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task UpdateCompanyAsync_SavesTheDataAndKeepsASnapshotHistory()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await parameters.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId);
            SystemParametersModel created = (await parameters.GetAsync())!;
            Assert.Null(created.CompanyName);

            CatalogOperationResult first = await parameters.UpdateCompanyAsync(
                new SaveCompanyDataCommand("Soporte Experto", "3-101-123456", "San José", "2222-2222", "info@soportexperto.cr"),
                created.Version,
                actorId);
            Assert.True(first.IsSuccess);

            SystemParametersModel current = (await parameters.GetAsync())!;
            Assert.Equal("Soporte Experto", current.CompanyName);
            Assert.Equal("3-101-123456", current.CompanyLegalId);
            Assert.Equal("San José", current.CompanyAddress);

            CatalogOperationResult second = await parameters.UpdateCompanyAsync(
                new SaveCompanyDataCommand("Soporte Experto S.A.", "3-101-123456", null, null, null),
                current.Version,
                actorId);
            Assert.True(second.IsSuccess);

            List<CompanyDataChange> history = await context.Set<CompanyDataChange>()
                .Where(change => change.ChangedByUserId == actorId)
                .OrderBy(change => change.ChangedAtUtc)
                .ToListAsync();

            Assert.Equal(2, history.Count);
            Assert.Equal("Soporte Experto", history[0].CompanyName);
            Assert.Equal("Soporte Experto S.A.", history[1].CompanyName);
            Assert.Null(history[1].CompanyAddress);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task UpdateCompanyAsync_RejectsAMissingLegalId()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ISystemParametersService parameters = services.GetRequiredService<ISystemParametersService>();
            await parameters.CreateAsync(new SaveSystemParametersCommand(500_000m, 7), actorId);
            SystemParametersModel created = (await parameters.GetAsync())!;

            CatalogOperationResult result = await parameters.UpdateCompanyAsync(
                new SaveCompanyDataCommand("Soporte Experto", "", null, null, null),
                created.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
            _ = context;
        });
    }

    private async Task RunInRollbackTransactionAsync(
        Func<FinancialDbContext, IServiceProvider, Guid, Task> scenario)
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
        Guid actorId = Guid.NewGuid();
        string email = $"sql-test-{actorId:N}@example.test";

        context.Users.Add(new ApplicationUser
        {
            Id = actorId,
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

        // Estos escenarios asumen que los parámetros todavía no están configurados. Se limpia
        // cualquier fila real ya guardada por la aplicación: como todo ocurre dentro de esta
        // transacción sin confirmar, se revierte al final sin afectar los datos reales.
        await context.Database.ExecuteSqlRawAsync("DELETE FROM finanzas.ParametrosFechaCierreHistorial");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM finanzas.ParametrosEmpresaHistorial");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM finanzas.ParametrosSistemaHistorial");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM finanzas.ParametrosSistema");

        try
        {
            await scenario(context, scope.ServiceProvider, actorId);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
