using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerTaxTypeScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerTaxTypeScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Scenario_ValidatesUniquenessConcurrencyStatusAndOptions()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ITaxTypeService taxTypes = services.GetRequiredService<ITaxTypeService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            string code = $"T-{suffix}";
            CreateTaxTypeCommand original = new(
                code,
                "Impuesto de integración",
                TaxCalculationType.Percentage,
                8.5m,
                "Descripción original");

            Assert.True((await taxTypes.CreateAsync(original, actorId)).IsSuccess);

            CatalogOperationResult duplicate = await taxTypes.CreateAsync(original, actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);
            context.ChangeTracker.Clear();

            TaxTypeModel created = Assert.Single((await taxTypes.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Active))).Items);
            Assert.Equal(8.5m, created.Rate);
            Assert.Equal(TaxCalculationType.Percentage, created.CalculationType);

            // La búsqueda también encuentra por nombre y por descripción.
            Assert.Single((await taxTypes.SearchAsync(
                new CatalogQuery("integración", CatalogStatusFilter.Active))).Items, item => item.Id == created.Id);
            Assert.Single((await taxTypes.SearchAsync(
                new CatalogQuery("original", CatalogStatusFilter.Active))).Items, item => item.Id == created.Id);

            UpdateTaxTypeCommand update = new(code, "Impuesto actualizado", 9m, null);
            Assert.True((await taxTypes.UpdateAsync(created.Id, update, created.Version, actorId)).IsSuccess);

            CatalogOperationResult stale = await taxTypes.UpdateAsync(
                created.Id,
                update with { Name = "Actualización obsoleta" },
                created.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.ConcurrencyConflict, stale.Status);

            TaxTypeModel current = (await taxTypes.GetAsync(created.Id))!;
            Assert.Equal("Impuesto actualizado", current.Name);
            Assert.Equal(9m, current.Rate);
            Assert.Null(current.Description);
            Assert.Equal(TaxCalculationType.Percentage, current.CalculationType);

            // Una tarifa porcentual por encima de 100 se rechaza en el servicio, sin llegar a la base.
            CatalogOperationResult tooHigh = await taxTypes.UpdateAsync(
                created.Id,
                update with { Rate = 100.5m },
                current.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, tooHigh.Status);

            Assert.Contains(
                await taxTypes.GetActiveOptionsAsync(),
                option => option.Id == created.Id && option.Rate == 9m);

            Assert.True((await taxTypes.SetActiveAsync(created.Id, false, current.Version, actorId)).IsSuccess);
            Assert.DoesNotContain(await taxTypes.GetActiveOptionsAsync(), option => option.Id == created.Id);
            Assert.Empty((await taxTypes.SearchAsync(new CatalogQuery(code, CatalogStatusFilter.Active))).Items);
            Assert.Single((await taxTypes.SearchAsync(new CatalogQuery(code, CatalogStatusFilter.Inactive))).Items);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task LoadReference_CreatesOnlyTheMissingRatesAndNeverTouchesExistingOnes()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ITaxTypeService taxTypes = services.GetRequiredService<ITaxTypeService>();
            int total = ReferenceTaxTypes.All.Count;

            // La base puede ya contener tarifas si alguien usó el botón: se calcula lo que falta.
            List<string> storedCodes = await context.Set<TaxType>().Select(taxType => taxType.Code).ToListAsync();
            int alreadyStored = ReferenceTaxTypes.All.Count(reference => storedCodes.Contains(reference.Code));

            // Un tipo con un código de referencia y datos propios, desactivado, no debe cambiar.
            const string customCode = "IVA-13";
            if (!storedCodes.Contains(customCode))
            {
                Assert.True((await taxTypes.CreateAsync(
                    new CreateTaxTypeCommand(customCode, "IVA personalizado", TaxCalculationType.Percentage, 12m, null),
                    actorId)).IsSuccess);
                TaxTypeModel custom = Assert.Single((await taxTypes.SearchAsync(
                    new CatalogQuery(customCode, CatalogStatusFilter.Active))).Items);
                Assert.True((await taxTypes.SetActiveAsync(custom.Id, false, custom.Version, actorId)).IsSuccess);
                alreadyStored++;
            }

            ReferenceTaxTypesLoadResult first = await taxTypes.LoadReferenceTaxTypesAsync(actorId);

            Assert.True(first.Operation.IsSuccess);
            Assert.Equal(total - alreadyStored, first.CreatedCount);
            Assert.Equal(alreadyStored, first.ExistingCount);

            foreach (CreateTaxTypeCommand reference in ReferenceTaxTypes.All)
            {
                Assert.Contains(await context.Set<TaxType>().ToListAsync(), taxType => taxType.Code == reference.Code);
            }

            // Un tipo existente conserva sus datos y su baja: la carga no lo reactiva ni lo modifica.
            TaxType kept = await context.Set<TaxType>().SingleAsync(taxType => taxType.Code == customCode);
            if (kept.Name == "IVA personalizado")
            {
                Assert.False(kept.IsActive);
                Assert.Equal(12m, kept.Rate);
            }

            // La segunda carga no encuentra nada que crear.
            ReferenceTaxTypesLoadResult second = await taxTypes.LoadReferenceTaxTypesAsync(actorId);
            Assert.True(second.Operation.IsSuccess);
            Assert.Equal(0, second.CreatedCount);
            Assert.Equal(total, second.ExistingCount);

            // Las tarifas de referencia quedan a nombre de quien las cargó.
            Assert.All(
                await context.Set<TaxType>()
                    .Where(taxType => taxType.Description != null && taxType.Description.Contains("asesoría contable"))
                    .ToListAsync(),
                taxType => Assert.Equal(actorId, taxType.CreatedByUserId));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Database_RejectsTaxTypesThatBreakItsConstraints()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ITaxTypeService taxTypes = services.GetRequiredService<ITaxTypeService>();
            string code = $"C-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            Assert.True((await taxTypes.CreateAsync(
                new CreateTaxTypeCommand(code, "Restricciones", TaxCalculationType.Percentage, 13m, null),
                actorId)).IsSuccess);
            Guid id = (await context.Set<TaxType>().SingleAsync(taxType => taxType.Code == code)).Id;
            context.ChangeTracker.Clear();

            Exception overHundred = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposImpuesto SET Rate = 100.5 WHERE Id = {id}"));
            Assert.Contains("CK_TiposImpuesto_Rate", overHundred.ToString());

            Exception negative = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposImpuesto SET Rate = -1 WHERE Id = {id}"));
            Assert.Contains("CK_TiposImpuesto_Rate", negative.ToString());

            Exception undefinedType = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposImpuesto SET CalculationType = 9 WHERE Id = {id}"));
            Assert.Contains("CK_TiposImpuesto_CalculationType", undefinedType.ToString());

            Exception blankCode = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE catalogos.TiposImpuesto SET Code = '   ' WHERE Id = {id}"));
            Assert.Contains("CK_TiposImpuesto_Codigo_NoVacio", blankCode.ToString());

            // Un monto fijo por unidad sí puede superar 100: el tope solo aplica a los porcentajes.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE catalogos.TiposImpuesto SET CalculationType = 2, Rate = 250 WHERE Id = {id}");
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
