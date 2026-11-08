using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
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
                IdentificationType.ForeignNonResident,
                $"ID-{suffix}",
                "cliente@example.test",
                null,
                null,
                null,
                VatCondition.Normal,
                false,
                null);

            Assert.True((await customers.CreateAsync(original, actorId)).IsSuccess);

            PagedResult<CustomerModel> activePage = await customers.SearchAsync(
                new CatalogQuery(uniqueName, CatalogStatusFilter.Active));
            CustomerModel created = Assert.Single(activePage.Items);
            string code = created.Code;
            PagedResult<CustomerModel> matchingCustomerColumns = await customers.SearchAsync(
                new CatalogQuery(
                    status: CatalogStatusFilter.Active,
                    code: code,
                    name: uniqueName,
                    identification: $"ID-{suffix}",
                    contact: "cliente@example.test"));
            Assert.Collection(matchingCustomerColumns.Items, customer => Assert.Equal(created.Id, customer.Id));

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
                        IdentificationType.ForeignNonResident,
                        $"PG-ID-{suffix}-{index:D2}",
                        null,
                        null,
                        null,
                        null,
                        VatCondition.Normal,
                        false,
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
                    IdentificationType.ForeignNonResident,
                    $"PRV-ID-{suffix}",
                    null,
                    null,
                    null,
                    "Comercio al detalle"),
                actorId)).IsSuccess);
            Assert.Single((await suppliers.SearchAsync(
                new CatalogQuery(supplierName, CatalogStatusFilter.Active))).Items);
            Assert.Single((await suppliers.SearchAsync(new CatalogQuery(
                status: CatalogStatusFilter.Active,
                name: supplierName,
                identification: $"PRV-ID-{suffix}"))).Items);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AccountAndItemScenario_ValidatesMovementAccountsAndActiveDependencies()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            ICatalogItemService items = services.GetRequiredService<ICatalogItemService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            // Solo se crean cuentas de movimiento dentro de los grupos permitidos.
            LedgerAccountModel incomeGroup = Assert.Single(
                (await accounts.SearchAsync(new LedgerAccountQuery("4.1"))).Items,
                account => account.Code == "4.1");
            LedgerAccountModel expenseGroup = Assert.Single(
                (await accounts.SearchAsync(new LedgerAccountQuery("5.1"))).Items,
                account => account.Code == "5.1");
            LedgerAccountModel assetGroup = Assert.Single(
                (await accounts.SearchAsync(new LedgerAccountQuery("1.1"))).Items,
                account => account.Code == "1.1");
            LedgerAccountModel incomeRoot = Assert.Single(
                (await accounts.SearchAsync(new LedgerAccountQuery("4"))).Items,
                account => account.Code == "4");

            CatalogOperationResult underRoot = await accounts.CreateAsync(
                new CreateLedgerAccountCommand($"Cuenta inválida {suffix}", incomeRoot.Id, null, null, null),
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, underRoot.Status);

            CatalogOperationResult assetWithoutCash = await accounts.CreateAsync(
                new CreateLedgerAccountCommand($"Activo inválido {suffix}", assetGroup.Id, null, null, null),
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, assetWithoutCash.Status);

            Assert.True((await accounts.CreateAsync(
                new CreateLedgerAccountCommand(
                    $"Cuenta de ingresos {suffix}",
                    incomeGroup.Id,
                    null,
                    null,
                    null),
                actorId)).IsSuccess);
            Assert.True((await accounts.CreateAsync(
                new CreateLedgerAccountCommand(
                    $"Cuenta de gastos {suffix}",
                    expenseGroup.Id,
                    null,
                    null,
                    null),
                actorId)).IsSuccess);

            LedgerAccountModel incomeAccount = Assert.Single(
                (await accounts.SearchAsync(new LedgerAccountQuery(null, $"Cuenta de ingresos {suffix}"))).Items);
            LedgerAccountModel expenseAccount = Assert.Single(
                (await accounts.SearchAsync(new LedgerAccountQuery(null, $"Cuenta de gastos {suffix}"))).Items);

            // Las cuentas acumuladoras del catálogo base no se editan.
            CatalogOperationResult groupUpdate = await accounts.UpdateAsync(
                incomeGroup.Id,
                new UpdateLedgerAccountCommand("Grupo renombrado", null, null),
                incomeGroup.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, groupUpdate.Status);

            string itemName = $"Servicio de integración {suffix}";
            Assert.True((await items.CreateAsync(
                new CreateCatalogItemCommand(
                    itemName,
                    CatalogItemType.Service,
                    "8311500000000",
                    null,
                    UnitOfMeasureCode.Hour,
                    25.12345m,
                    incomeAccount.Id,
                    null),
                actorId)).IsSuccess);

            CatalogOperationResult deactivation = await accounts.SetActiveAsync(
                incomeAccount.Id,
                false,
                incomeAccount.Version,
                actorId);
            Assert.True(deactivation.IsSuccess);
            Assert.DoesNotContain(
                await accounts.GetActiveOptionsAsync(LedgerAccountType.Income),
                option => option.Id == incomeAccount.Id);

            CatalogItemModel item = Assert.Single((await items.SearchAsync(
                new CatalogQuery(itemName, CatalogStatusFilter.Active))).Items);
            Assert.True(item.IsActive);
            Assert.Equal(25.1235m, item.ReferencePriceCrc);
            Assert.Single((await items.SearchAsync(new CatalogQuery(
                status: CatalogStatusFilter.Active,
                code: item.Code,
                name: itemName,
                itemType: CatalogItemType.Service,
                cabysCode: "8311500000000",
                unitOfMeasure: UnitOfMeasureCode.Hour,
                referencePrice: 25.1235m))).Items, result => result.Id == item.Id);

            LedgerAccountModel firstInactiveIncome = (await accounts.GetAsync(incomeAccount.Id))!;
            Assert.True((await accounts.SetActiveAsync(
                firstInactiveIncome.Id,
                true,
                firstInactiveIncome.Version,
                actorId)).IsSuccess);

            Assert.True((await items.SetActiveAsync(
                item.Id,
                false,
                item.Version,
                actorId)).IsSuccess);

            LedgerAccountModel currentIncome = (await accounts.GetAsync(incomeAccount.Id))!;
            Assert.True((await accounts.SetActiveAsync(
                currentIncome.Id,
                false,
                currentIncome.Version,
                actorId)).IsSuccess);

            LedgerAccountModel inactiveIncome = (await accounts.GetAsync(incomeAccount.Id))!;
            Assert.True((await accounts.SetActiveAsync(
                inactiveIncome.Id,
                true,
                inactiveIncome.Version,
                actorId)).IsSuccess);

            LedgerAccountModel currentExpense = (await accounts.GetAsync(expenseAccount.Id))!;
            Assert.True((await accounts.SetActiveAsync(
                currentExpense.Id,
                false,
                currentExpense.Version,
                actorId)).IsSuccess);
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
