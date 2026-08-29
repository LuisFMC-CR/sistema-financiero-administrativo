using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Accounts;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.ExchangeRates;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerCatalogScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerCatalogScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ContactScenario_ValidatesPagingStatusUniquenessAndConcurrency()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ICustomerCatalogService customers = services.GetRequiredService<ICustomerCatalogService>();
            ISupplierCatalogService suppliers = services.GetRequiredService<ISupplierCatalogService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            string code = $"CLI-{suffix}";
            SaveCustomerCommand original = new(
                code,
                "Cliente de integración",
                $"ID-{suffix}",
                "cliente@example.test",
                null,
                null);

            Assert.True((await customers.CreateAsync(original, actorId)).IsSuccess);

            PagedResult<CustomerModel> activePage = await customers.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Active));
            CustomerModel created = Assert.Single(activePage.Items);

            SaveCustomerCommand updated = original with { Name = "Cliente actualizado" };
            Assert.True((await customers.UpdateAsync(
                created.Id,
                updated,
                created.Version,
                actorId)).IsSuccess);

            CatalogOperationResult staleUpdate = await customers.UpdateAsync(
                created.Id,
                updated with { Name = "Actualización obsoleta" },
                created.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.ConcurrencyConflict, staleUpdate.Status);

            CustomerModel current = (await customers.GetAsync(created.Id))!;
            Assert.Equal("Cliente actualizado", current.Name);
            Assert.True((await customers.SetActiveAsync(
                current.Id,
                false,
                current.Version,
                actorId)).IsSuccess);

            Assert.Empty((await customers.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Active))).Items);
            Assert.Single((await customers.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Inactive))).Items);

            CatalogOperationResult duplicate = await customers.CreateAsync(original, actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);

            for (int index = 0; index < 21; index++)
            {
                Assert.True((await customers.CreateAsync(
                    new SaveCustomerCommand(
                        $"PG-{suffix}-{index:D2}",
                        $"Cliente paginado {index:D2}",
                        null,
                        null,
                        null,
                        null),
                    actorId)).IsSuccess);
            }

            PagedResult<CustomerModel> secondPage = await customers.SearchAsync(
                new CatalogQuery($"PG-{suffix}", CatalogStatusFilter.Active, page: 2, pageSize: 10));
            Assert.Equal(21, secondPage.TotalCount);
            Assert.Equal(10, secondPage.Items.Count);
            Assert.True(secondPage.HasPreviousPage);
            Assert.True(secondPage.HasNextPage);

            Assert.True((await suppliers.CreateAsync(
                new SaveSupplierCommand(
                    $"PRV-{suffix}",
                    "Proveedor de integración",
                    $"PRV-ID-{suffix}",
                    null,
                    null,
                    null),
                actorId)).IsSuccess);
            Assert.Single((await suppliers.SearchAsync(
                new CatalogQuery($"PRV-{suffix}", CatalogStatusFilter.Active))).Items);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CategoryAndItemScenario_ValidatesHierarchyAndActiveDependencies()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IFinancialCategoryService categories = services
                .GetRequiredService<IFinancialCategoryService>();
            ICatalogItemService items = services.GetRequiredService<ICatalogItemService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            Assert.True((await categories.CreateAsync(
                new SaveFinancialCategoryCommand(
                    $"ING-{suffix}",
                    "Ingresos de integración",
                    FinancialCategoryKind.Income,
                    null,
                    null),
                actorId)).IsSuccess);

            FinancialCategoryModel parent = Assert.Single((await categories.SearchAsync(
                new CatalogQuery($"ING-{suffix}", CatalogStatusFilter.Active))).Items);

            Assert.True((await categories.CreateAsync(
                new SaveFinancialCategoryCommand(
                    $"SUB-{suffix}",
                    "Subcategoría de integración",
                    FinancialCategoryKind.Income,
                    parent.Id,
                    null),
                actorId)).IsSuccess);

            FinancialCategoryModel child = Assert.Single((await categories.SearchAsync(
                new CatalogQuery($"SUB-{suffix}", CatalogStatusFilter.Active))).Items);

            Assert.True((await categories.CreateAsync(
                new SaveFinancialCategoryCommand(
                    $"GAS-{suffix}",
                    "Gasto de integración",
                    FinancialCategoryKind.Expense,
                    null,
                    null),
                actorId)).IsSuccess);
            FinancialCategoryModel expense = Assert.Single((await categories.SearchAsync(
                new CatalogQuery($"GAS-{suffix}", CatalogStatusFilter.Active))).Items);

            CatalogOperationResult wrongKind = await categories.UpdateAsync(
                child.Id,
                new SaveFinancialCategoryCommand(
                    child.Code,
                    child.Name,
                    child.Kind,
                    expense.Id,
                    child.Description),
                child.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, wrongKind.Status);

            CatalogOperationResult cycle = await categories.UpdateAsync(
                parent.Id,
                new SaveFinancialCategoryCommand(
                    parent.Code,
                    parent.Name,
                    parent.Kind,
                    child.Id,
                    parent.Description),
                parent.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, cycle.Status);

            Assert.True((await items.CreateAsync(
                new SaveCatalogItemCommand(
                    $"SRV-{suffix}",
                    "Servicio de integración",
                    CatalogItemType.Service,
                    null,
                    "hora",
                    null,
                    25.12345m,
                    child.Id),
                actorId)).IsSuccess);

            CatalogOperationResult dependency = await categories.SetActiveAsync(
                child.Id,
                false,
                child.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.DependencyConflict, dependency.Status);

            CatalogItemModel item = Assert.Single((await items.SearchAsync(
                new CatalogQuery($"SRV-{suffix}", CatalogStatusFilter.Active))).Items);
            Assert.Equal(25.1235m, item.ReferencePriceUsd);
            Assert.True((await items.SetActiveAsync(
                item.Id,
                false,
                item.Version,
                actorId)).IsSuccess);

            FinancialCategoryModel currentChild = (await categories.GetAsync(child.Id))!;
            Assert.True((await categories.SetActiveAsync(
                currentChild.Id,
                false,
                currentChild.Version,
                actorId)).IsSuccess);

            FinancialCategoryModel currentParent = (await categories.GetAsync(parent.Id))!;
            Assert.True((await categories.SetActiveAsync(
                currentParent.Id,
                false,
                currentParent.Version,
                actorId)).IsSuccess);

            FinancialCategoryModel inactiveChild = (await categories.GetAsync(child.Id))!;
            CatalogOperationResult inactiveParent = await categories.SetActiveAsync(
                inactiveChild.Id,
                true,
                inactiveChild.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, inactiveParent.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AccountAndRateScenario_PreservesFixedFieldsPrecisionAndUniqueDate()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IFinancialAccountService accounts = services.GetRequiredService<IFinancialAccountService>();
            IDailyExchangeRateService rates = services.GetRequiredService<IDailyExchangeRateService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            Assert.True((await accounts.CreateAsync(
                new CreateFinancialAccountCommand(
                    $"CTA-{suffix}",
                    "Cuenta de integración",
                    FinancialAccountType.Bank,
                    CurrencyCode.USD,
                    "Referencia ficticia"),
                actorId)).IsSuccess);

            FinancialAccountModel account = Assert.Single((await accounts.SearchAsync(
                new CatalogQuery($"CTA-{suffix}", CatalogStatusFilter.Active))).Items);
            Assert.True((await accounts.UpdateAsync(
                account.Id,
                new UpdateFinancialAccountCommand(
                    account.Code,
                    "Cuenta actualizada",
                    null),
                account.Version,
                actorId)).IsSuccess);

            FinancialAccountModel updatedAccount = (await accounts.GetAsync(account.Id))!;
            Assert.Equal(FinancialAccountType.Bank, updatedAccount.Type);
            Assert.Equal(CurrencyCode.USD, updatedAccount.Currency);

            DateOnly date = new(2099, 1, 1);
            SaveDailyExchangeRateCommand rateCommand = new(
                date,
                510.1234567m,
                "Fuente ficticia",
                null);
            Assert.True((await rates.CreateAsync(rateCommand, actorId)).IsSuccess);

            DailyExchangeRateModel savedRate = Assert.Single((await rates.SearchAsync(
                new PageQuery(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))).Items);
            Assert.Equal(510.123457m, savedRate.CrcPerUsd);

            CatalogOperationResult duplicate = await rates.CreateAsync(rateCommand, actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);
        });
    }

    private async Task RunInRollbackTransactionAsync(
        Func<IServiceProvider, Guid, Task> scenario)
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
            await scenario(scope.ServiceProvider, actorId);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
