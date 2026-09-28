using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerWithholdingTypeScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerWithholdingTypeScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Scenario_ValidatesUniquenessConcurrencyStatusAndOptions()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            IWithholdingTypeService withholdingTypes = services.GetRequiredService<IWithholdingTypeService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            string code = $"R-{suffix}";
            CreateWithholdingTypeCommand original = new(
                code,
                "Retención de integración",
                2m,
                "Descripción original");

            Assert.True((await withholdingTypes.CreateAsync(original, actorId)).IsSuccess);

            CatalogOperationResult duplicate = await withholdingTypes.CreateAsync(original, actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);
            context.ChangeTracker.Clear();

            WithholdingTypeModel created = Assert.Single((await withholdingTypes.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Active))).Items);
            Assert.Equal(2m, created.Rate);

            // La búsqueda también encuentra por nombre y por descripción.
            Assert.Single((await withholdingTypes.SearchAsync(
                new CatalogQuery("integración", CatalogStatusFilter.Active))).Items, item => item.Id == created.Id);
            Assert.Single((await withholdingTypes.SearchAsync(
                new CatalogQuery("original", CatalogStatusFilter.Active))).Items, item => item.Id == created.Id);

            UpdateWithholdingTypeCommand update = new(code, "Retención actualizada", 4m, null);
            Assert.True((await withholdingTypes.UpdateAsync(created.Id, update, created.Version, actorId)).IsSuccess);

            CatalogOperationResult stale = await withholdingTypes.UpdateAsync(
                created.Id,
                update with { Name = "Actualización obsoleta" },
                created.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.ConcurrencyConflict, stale.Status);

            WithholdingTypeModel current = (await withholdingTypes.GetAsync(created.Id))!;
            Assert.Equal("Retención actualizada", current.Name);
            Assert.Equal(4m, current.Rate);
            Assert.Null(current.Description);

            // Una tarifa por encima de 100 se rechaza en el servicio, sin llegar a la base.
            CatalogOperationResult tooHigh = await withholdingTypes.UpdateAsync(
                created.Id,
                update with { Rate = 100.5m },
                current.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, tooHigh.Status);

            Assert.Contains(
                await withholdingTypes.GetActiveOptionsAsync(),
                option => option.Id == created.Id && option.Rate == 4m);

            Assert.True((await withholdingTypes.SetActiveAsync(created.Id, false, current.Version, actorId)).IsSuccess);
            Assert.DoesNotContain(await withholdingTypes.GetActiveOptionsAsync(), option => option.Id == created.Id);
            Assert.Empty((await withholdingTypes.SearchAsync(new CatalogQuery(code, CatalogStatusFilter.Active))).Items);
            Assert.Single((await withholdingTypes.SearchAsync(new CatalogQuery(code, CatalogStatusFilter.Inactive))).Items);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Database_RejectsWithholdingTypesThatBreakItsConstraints()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            IWithholdingTypeService withholdingTypes = services.GetRequiredService<IWithholdingTypeService>();
            string code = $"C-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            Assert.True((await withholdingTypes.CreateAsync(
                new CreateWithholdingTypeCommand(code, "Restricciones", 2m, null),
                actorId)).IsSuccess);
            Guid id = (await context.Set<SistemaFinanciero.Domain.Catalogs.WithholdingType>()
                .SingleAsync(candidate => candidate.Code == code)).Id;
            context.ChangeTracker.Clear();

            Exception overHundred = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposRetencion SET Rate = 100.5 WHERE Id = {id}"));
            Assert.Contains("CK_TiposRetencion_Rate", overHundred.ToString());

            Exception negative = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposRetencion SET Rate = -1 WHERE Id = {id}"));
            Assert.Contains("CK_TiposRetencion_Rate", negative.ToString());

            Exception blankCode = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposRetencion SET Code = '   ' WHERE Id = {id}"));
            Assert.Contains("CK_TiposRetencion_Codigo_NoVacio", blankCode.ToString());
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
