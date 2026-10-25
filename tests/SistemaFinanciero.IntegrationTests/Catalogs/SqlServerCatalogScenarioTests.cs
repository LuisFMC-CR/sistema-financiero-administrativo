using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Domain.Catalogs;
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
            string uniqueName = $"Cliente de integración {suffix}";
            SaveCustomerCommand original = new(
                uniqueName,
                $"ID-{suffix}",
                "cliente@example.test",
                null,
                null);

            Assert.True((await customers.CreateAsync(original, actorId)).IsSuccess);

            PagedResult<CustomerModel> activePage = await customers.SearchAsync(
                new CatalogQuery(uniqueName, CatalogStatusFilter.Active));
            CustomerModel created = Assert.Single(activePage.Items);
            string code = created.Code;

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
            Assert.Equal(code, current.Code);
            Assert.True((await customers.SetActiveAsync(
                current.Id,
                false,
                current.Version,
                actorId)).IsSuccess);

            Assert.Empty((await customers.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Active))).Items);
            Assert.Single((await customers.SearchAsync(
                new CatalogQuery(code, CatalogStatusFilter.Inactive))).Items);

            // La identificación es obligatoria y única (RF-50): crear de nuevo con la misma
            // identificación es el duplicado ahora que el código lo asigna el sistema (regla 12).
            CatalogOperationResult duplicate = await customers.CreateAsync(original, actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);

            string pagedNamePrefix = $"Cliente paginado {suffix}";
            for (int index = 0; index < 21; index++)
            {
                Assert.True((await customers.CreateAsync(
                    new SaveCustomerCommand(
                        $"{pagedNamePrefix} {index:D2}",
                        $"PG-ID-{suffix}-{index:D2}",
                        null,
                        null,
                        null),
                    actorId)).IsSuccess);
            }

            PagedResult<CustomerModel> secondPage = await customers.SearchAsync(
                new CatalogQuery(pagedNamePrefix, CatalogStatusFilter.Active, page: 2, pageSize: 10));
            Assert.Equal(21, secondPage.TotalCount);
            Assert.Equal(10, secondPage.Items.Count);
            Assert.True(secondPage.HasPreviousPage);
            Assert.True(secondPage.HasNextPage);

            string supplierName = $"Proveedor de integración {suffix}";
            Assert.True((await suppliers.CreateAsync(
                new SaveSupplierCommand(
                    supplierName,
                    $"PRV-ID-{suffix}",
                    null,
                    null,
                    null,
                    "Comercio al detalle"),
                actorId)).IsSuccess);
            Assert.Single((await suppliers.SearchAsync(
                new CatalogQuery(supplierName, CatalogStatusFilter.Active))).Items);
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
            ILedgerAccountService ledgerAccounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            // Toda categoría necesita una cuenta contable activa de su mismo tipo. El servicio ya no
            // crea cuentas raíz (regla 13): se agregan como detalle bajo los grupos del catálogo base.
            Guid incomeRootId = Assert.Single((await ledgerAccounts.SearchAsync(
                new CatalogQuery("2", CatalogStatusFilter.Active))).Items, account => account.Code == "2").Id;
            Guid expenseRootId = Assert.Single((await ledgerAccounts.SearchAsync(
                new CatalogQuery("3", CatalogStatusFilter.Active))).Items, account => account.Code == "3").Id;

            Assert.True((await ledgerAccounts.CreateAsync(
                new CreateLedgerAccountCommand($"Cuenta de ingresos {suffix}", incomeRootId, null, null, null),
                actorId)).IsSuccess);
            Assert.True((await ledgerAccounts.CreateAsync(
                new CreateLedgerAccountCommand($"Cuenta de gastos {suffix}", expenseRootId, null, null, null),
                actorId)).IsSuccess);
            Guid incomeAccountId = (await ledgerAccounts.GetActiveOptionsAsync(LedgerAccountType.Income))
                .Single(option => option.Name == $"Cuenta de ingresos {suffix}").Id;
            Guid expenseAccountId = (await ledgerAccounts.GetActiveOptionsAsync(LedgerAccountType.Expense))
                .Single(option => option.Name == $"Cuenta de gastos {suffix}").Id;

            string incomeCategoryName = $"Ingresos de integración {suffix}";
            Assert.True((await categories.CreateAsync(
                new SaveFinancialCategoryCommand(
                    incomeCategoryName,
                    FinancialCategoryKind.Income,
                    null,
                    incomeAccountId,
                    null),
                actorId)).IsSuccess);

            FinancialCategoryModel parent = Assert.Single((await categories.SearchAsync(
                new CatalogQuery(incomeCategoryName, CatalogStatusFilter.Active))).Items);

            string subCategoryName = $"Subcategoría de integración {suffix}";
            Assert.True((await categories.CreateAsync(
                new SaveFinancialCategoryCommand(
                    subCategoryName,
                    FinancialCategoryKind.Income,
                    parent.Id,
                    incomeAccountId,
                    null),
                actorId)).IsSuccess);

            FinancialCategoryModel child = Assert.Single((await categories.SearchAsync(
                new CatalogQuery(subCategoryName, CatalogStatusFilter.Active))).Items);

            string expenseCategoryName = $"Gasto de integración {suffix}";
            Assert.True((await categories.CreateAsync(
                new SaveFinancialCategoryCommand(
                    expenseCategoryName,
                    FinancialCategoryKind.Expense,
                    null,
                    expenseAccountId,
                    null),
                actorId)).IsSuccess);
            FinancialCategoryModel expense = Assert.Single((await categories.SearchAsync(
                new CatalogQuery(expenseCategoryName, CatalogStatusFilter.Active))).Items);

            CatalogOperationResult wrongKind = await categories.UpdateAsync(
                child.Id,
                new SaveFinancialCategoryCommand(
                    child.Name,
                    child.Kind,
                    expense.Id,
                    child.LedgerAccountId,
                    child.Description),
                child.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, wrongKind.Status);

            CatalogOperationResult cycle = await categories.UpdateAsync(
                parent.Id,
                new SaveFinancialCategoryCommand(
                    parent.Name,
                    parent.Kind,
                    child.Id,
                    parent.LedgerAccountId,
                    parent.Description),
                parent.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, cycle.Status);

            string itemName = $"Servicio de integración {suffix}";
            Assert.True((await items.CreateAsync(
                new CreateCatalogItemCommand(
                    itemName,
                    CatalogItemType.Service,
                    null,
                    "hora",
                    25.12345m,
                    child.Id,
                    null),
                actorId)).IsSuccess);

            CatalogOperationResult dependency = await categories.SetActiveAsync(
                child.Id,
                false,
                child.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.DependencyConflict, dependency.Status);

            CatalogItemModel item = Assert.Single((await items.SearchAsync(
                new CatalogQuery(itemName, CatalogStatusFilter.Active))).Items);
            Assert.Equal(25.1235m, item.ReferencePriceCrc);
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
