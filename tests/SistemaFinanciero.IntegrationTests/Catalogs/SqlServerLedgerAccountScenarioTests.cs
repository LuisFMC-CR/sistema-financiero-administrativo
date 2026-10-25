using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// Las cuentas de grupo (código "2" Ingresos, "3" Gastos, "1.1" Efectivo) son las del catálogo base
/// sembrado por la migración (regla 13): el servicio ya no crea cuentas raíz, así que estas pruebas
/// agregan cuentas de detalle bajo esos grupos reales, igual que lo hace la aplicación.
/// </summary>
public sealed class SqlServerLedgerAccountScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerLedgerAccountScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task HierarchyScenario_EnforcesCashRulesAndDependencies()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            LedgerAccountModel cashGroup = await GetByCodeAsync(accounts, "1.1");
            LedgerAccountModel expenseRoot = await GetByCodeAsync(accounts, "3");

            // Una cuenta de efectivo cuelga del grupo de efectivo del catálogo base.
            Assert.True((await accounts.CreateAsync(
                Command($"Banco {suffix}", cashGroup.Id, cashKind: CashAccountKind.Bank, reference: "Terminada en 4321"),
                actorId)).IsSuccess);
            LedgerAccountModel bank = await GetByNameAsync(accounts, $"Banco {suffix}");
            Assert.True(bank.IsCash);
            Assert.Equal(CashAccountKind.Bank, bank.CashKind);
            Assert.Equal("Efectivo", bank.ParentName);
            Assert.StartsWith(cashGroup.Code + ".", bank.Code);

            // Dos cuentas bajo el mismo padre reciben códigos jerárquicos consecutivos (regla 12).
            Assert.True((await accounts.CreateAsync(
                Command($"Banco dos {suffix}", cashGroup.Id, cashKind: CashAccountKind.Bank), actorId)).IsSuccess);
            LedgerAccountModel bankTwo = await GetByNameAsync(accounts, $"Banco dos {suffix}");
            Assert.NotEqual(bank.Code, bankTwo.Code);

            // Una cuenta de efectivo no puede tener hijas.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"Hija de banco {suffix}", bank.Id), actorId),
                "efectivo");

            // La cuenta superior debe existir y estar activa.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"Sin padre {suffix}", Guid.NewGuid()), actorId),
                "no existe");

            // La regla de efectivo la aplica el dominio: un grupo de Gasto no admite cuentas de efectivo.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"Gasto de efectivo {suffix}", expenseRoot.Id, cashKind: CashAccountKind.Cash),
                    actorId),
                "Activo");

            // Actualización con control de concurrencia; código, tipo y cuenta superior no forman parte del comando.
            Assert.True((await accounts.CreateAsync(
                Command($"Detalle de gasto {suffix}", expenseRoot.Id), actorId)).IsSuccess);
            LedgerAccountModel detail = await GetByNameAsync(accounts, $"Detalle de gasto {suffix}");
            UpdateLedgerAccountCommand update = new("Detalle de gasto renombrado", null, "Nota de prueba");
            Assert.True((await accounts.UpdateAsync(detail.Id, update, detail.Version, actorId)).IsSuccess);
            Assert.Equal(
                CatalogOperationStatus.ConcurrencyConflict,
                (await accounts.UpdateAsync(detail.Id, update, detail.Version, actorId)).Status);

            LedgerAccountModel updatedDetail = (await accounts.GetAsync(detail.Id))!;
            Assert.Equal("Detalle de gasto renombrado", updatedDetail.Name);
            Assert.Equal(detail.Code, updatedDetail.Code);
            Assert.Equal(expenseRoot.Id, updatedDetail.ParentId);

            // Una cuenta de detalle también puede ser cuenta superior de otra (regla 13 solo limita lo
            // que agrega la interfaz; el dominio no restringe la profundidad).
            Assert.True((await accounts.CreateAsync(
                Command($"Nieta {suffix}", updatedDetail.Id), actorId)).IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task DeactivationScenario_BlocksDependenciesAndRequiresAnActiveParentToReactivate()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            LedgerAccountModel incomeRoot = await GetByCodeAsync(accounts, "2");

            await CreateAsync(accounts, actorId, Command($"Grupo {suffix}", incomeRoot.Id));
            LedgerAccountModel group = await GetByNameAsync(accounts, $"Grupo {suffix}");
            await CreateAsync(accounts, actorId, Command($"Hijo {suffix}", group.Id));
            LedgerAccountModel child = await GetByNameAsync(accounts, $"Hijo {suffix}");

            Assert.Equal(
                CatalogOperationStatus.DependencyConflict,
                (await accounts.SetActiveAsync(group.Id, false, group.Version, actorId)).Status);

            Assert.True((await accounts.SetActiveAsync(child.Id, false, child.Version, actorId)).IsSuccess);
            group = (await accounts.GetAsync(group.Id))!;
            Assert.True((await accounts.SetActiveAsync(group.Id, false, group.Version, actorId)).IsSuccess);

            child = (await accounts.GetAsync(child.Id))!;
            AssertInvalid(
                await accounts.SetActiveAsync(child.Id, true, child.Version, actorId),
                "inactiva");

            group = (await accounts.GetAsync(group.Id))!;
            Assert.True((await accounts.SetActiveAsync(group.Id, true, group.Version, actorId)).IsSuccess);
            child = (await accounts.GetAsync(child.Id))!;
            Assert.True((await accounts.SetActiveAsync(child.Id, true, child.Version, actorId)).IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task OptionsScenario_ListsOnlyTheAccountsEachSelectorNeeds()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            LedgerAccountModel incomeRoot = await GetByCodeAsync(accounts, "2");
            LedgerAccountModel expenseRoot = await GetByCodeAsync(accounts, "3");
            LedgerAccountModel cashGroup = await GetByCodeAsync(accounts, "1.1");

            await CreateAsync(accounts, actorId, Command($"Ingresos por servicios {suffix}", incomeRoot.Id));
            LedgerAccountModel incomeChild = await GetByNameAsync(accounts, $"Ingresos por servicios {suffix}");
            await CreateAsync(accounts, actorId, Command($"Ingresos por ventas {suffix}", incomeRoot.Id));
            LedgerAccountModel incomeSibling = await GetByNameAsync(accounts, $"Ingresos por ventas {suffix}");
            await CreateAsync(accounts, actorId, Command($"Gasto nuevo {suffix}", expenseRoot.Id));
            LedgerAccountModel expense = await GetByNameAsync(accounts, $"Gasto nuevo {suffix}");
            await CreateAsync(
                accounts,
                actorId,
                Command($"Caja {suffix}", cashGroup.Id, cashKind: CashAccountKind.Cash));
            LedgerAccountModel caja = await GetByNameAsync(accounts, $"Caja {suffix}");
            await CreateAsync(
                accounts,
                actorId,
                Command($"Banco {suffix}", cashGroup.Id, cashKind: CashAccountKind.Bank));
            LedgerAccountModel banco = await GetByNameAsync(accounts, $"Banco {suffix}");

            // Las opciones de cuenta superior excluyen otros tipos, la cuenta misma y sus descendientes.
            IReadOnlyList<LedgerAccountOption> parents = await accounts.GetParentOptionsAsync(
                LedgerAccountType.Income,
                excludedAccountId: incomeRoot.Id);
            Assert.DoesNotContain(parents, option => option.Id == incomeRoot.Id);
            Assert.DoesNotContain(parents, option => option.Id == incomeChild.Id);
            Assert.DoesNotContain(parents, option => option.Id == expense.Id);

            IReadOnlyList<LedgerAccountOption> siblingParents = await accounts.GetParentOptionsAsync(
                LedgerAccountType.Income,
                excludedAccountId: incomeSibling.Id);
            Assert.Contains(siblingParents, option => option.Id == incomeRoot.Id);
            Assert.Contains(siblingParents, option => option.Id == incomeChild.Id);
            Assert.DoesNotContain(siblingParents, option => option.Id == incomeSibling.Id);

            // Las cajas y los bancos nunca se ofrecen como cuenta superior.
            IReadOnlyList<LedgerAccountOption> assetParents = await accounts.GetParentOptionsAsync(LedgerAccountType.Asset);
            Assert.DoesNotContain(assetParents, option => option.Id == caja.Id);
            Assert.DoesNotContain(assetParents, option => option.Id == banco.Id);

            // Las categorías eligen entre las cuentas activas de su tipo.
            IReadOnlyList<LedgerAccountOption> incomeOptions = await accounts.GetActiveOptionsAsync(LedgerAccountType.Income);
            Assert.Contains(incomeOptions, option => option.Id == incomeChild.Id);
            Assert.DoesNotContain(incomeOptions, option => option.Id == expense.Id);

            LedgerAccountModel deactivated = (await accounts.GetAsync(incomeSibling.Id))!;
            Assert.True((await accounts.SetActiveAsync(deactivated.Id, false, deactivated.Version, actorId)).IsSuccess);
            Assert.DoesNotContain(
                await accounts.GetActiveOptionsAsync(LedgerAccountType.Income),
                option => option.Id == incomeSibling.Id);

            // Las cajas y los bancos activos se listan todos juntos.
            IReadOnlyList<CashAccountOption> all = await accounts.GetActiveCashAccountsAsync();
            Assert.Contains(all, option => option.Id == caja.Id && option.CashKind == CashAccountKind.Cash);
            Assert.Contains(all, option => option.Id == banco.Id && option.CashKind == CashAccountKind.Bank);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CategoryLinkScenario_RequiresAnActiveAccountOfTheSameNatureAndProtectsIt()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            IFinancialCategoryService categories = services.GetRequiredService<IFinancialCategoryService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            LedgerAccountModel incomeRoot = await GetByCodeAsync(accounts, "2");
            LedgerAccountModel expenseRoot = await GetByCodeAsync(accounts, "3");

            await CreateAsync(accounts, actorId, Command($"Ingresos uno {suffix}", incomeRoot.Id));
            await CreateAsync(accounts, actorId, Command($"Ingresos dos {suffix}", incomeRoot.Id));
            await CreateAsync(accounts, actorId, Command($"Ingresos inactiva {suffix}", incomeRoot.Id));
            await CreateAsync(accounts, actorId, Command($"Gastos uno {suffix}", expenseRoot.Id));
            LedgerAccountModel income1 = await GetByNameAsync(accounts, $"Ingresos uno {suffix}");
            LedgerAccountModel income2 = await GetByNameAsync(accounts, $"Ingresos dos {suffix}");
            LedgerAccountModel inactiveIncome = await GetByNameAsync(accounts, $"Ingresos inactiva {suffix}");
            LedgerAccountModel expense1 = await GetByNameAsync(accounts, $"Gastos uno {suffix}");
            Assert.True((await accounts.SetActiveAsync(
                inactiveIncome.Id, false, inactiveIncome.Version, actorId)).IsSuccess);

            // Una categoría de ingreso con una cuenta de Ingreso activa es válida y expone la cuenta.
            Assert.True((await categories.CreateAsync(
                CategoryCommand($"C1-{suffix}", FinancialCategoryKind.Income, income1.Id), actorId)).IsSuccess);
            FinancialCategoryModel category = await GetCategoryAsync(categories, $"C1-{suffix}");
            Assert.Equal(income1.Id, category.LedgerAccountId);
            Assert.Equal(income1.Code, category.LedgerAccountCode);
            Assert.Equal($"Ingresos uno {suffix}", category.LedgerAccountName);

            // La cuenta debe existir, estar activa y ser del tipo que corresponde a la naturaleza.
            AssertInvalid(
                await categories.CreateAsync(
                    CategoryCommand($"C2-{suffix}", FinancialCategoryKind.Expense, income1.Id), actorId),
                "Gasto");
            AssertInvalid(
                await categories.CreateAsync(
                    CategoryCommand($"C3-{suffix}", FinancialCategoryKind.Income, expense1.Id), actorId),
                "Ingreso");
            AssertInvalid(
                await categories.CreateAsync(
                    CategoryCommand($"C4-{suffix}", FinancialCategoryKind.Income, inactiveIncome.Id), actorId),
                "inactiva");
            AssertInvalid(
                await categories.CreateAsync(
                    CategoryCommand($"C5-{suffix}", FinancialCategoryKind.Income, Guid.NewGuid()), actorId),
                "no existe");
            AssertInvalid(
                await categories.CreateAsync(
                    CategoryCommand($"C6-{suffix}", FinancialCategoryKind.Income, Guid.Empty), actorId),
                "no existe");
            context.ChangeTracker.Clear();

            // Al editar se puede reasignar a otra cuenta del mismo tipo, pero no a una de otro tipo.
            Assert.True((await categories.UpdateAsync(
                category.Id,
                CategoryCommand(category.Name, FinancialCategoryKind.Income, income2.Id),
                category.Version,
                actorId)).IsSuccess);
            category = (await categories.GetAsync(category.Id))!;
            Assert.Equal(income2.Id, category.LedgerAccountId);
            AssertInvalid(
                await categories.UpdateAsync(
                    category.Id,
                    CategoryCommand(category.Name, FinancialCategoryKind.Income, expense1.Id),
                    category.Version,
                    actorId),
                "Ingreso");

            // Una cuenta usada por una categoría activa no se desactiva.
            income2 = (await accounts.GetAsync(income2.Id))!;
            CatalogOperationResult blocked = await accounts.SetActiveAsync(
                income2.Id, false, income2.Version, actorId);
            Assert.Equal(CatalogOperationStatus.DependencyConflict, blocked.Status);

            // Con la categoría inactiva sí se puede; y entonces la categoría no se reactiva sin cuenta activa.
            category = (await categories.GetAsync(category.Id))!;
            Assert.True((await categories.SetActiveAsync(category.Id, false, category.Version, actorId)).IsSuccess);
            income2 = (await accounts.GetAsync(income2.Id))!;
            Assert.True((await accounts.SetActiveAsync(income2.Id, false, income2.Version, actorId)).IsSuccess);

            category = (await categories.GetAsync(category.Id))!;
            AssertInvalid(
                await categories.SetActiveAsync(category.Id, true, category.Version, actorId),
                "inactiva");

            income2 = (await accounts.GetAsync(income2.Id))!;
            Assert.True((await accounts.SetActiveAsync(income2.Id, true, income2.Version, actorId)).IsSuccess);
            category = (await categories.GetAsync(category.Id))!;
            Assert.True((await categories.SetActiveAsync(category.Id, true, category.Version, actorId)).IsSuccess);

            // La base también protege el vínculo.
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CategoriasFinancieras SET LedgerAccountId = NULL WHERE Id = '{category.Id}'",
                "LedgerAccountId");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CategoriasFinancieras SET LedgerAccountId = NEWID() WHERE Id = '{category.Id}'",
                "FK_CategoriasFinancieras_CuentasContables");
            await AssertRejectsAsync(
                context,
                $"DELETE FROM catalogos.CuentasContables WHERE Id = '{income2.Id}'",
                "FK_CategoriasFinancieras_CuentasContables");
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Database_RejectsAccountsThatBreakItsConstraints()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            LedgerAccountModel expenseRoot = await GetByCodeAsync(accounts, "3");
            LedgerAccountModel cashGroup = await GetByCodeAsync(accounts, "1.1");

            await CreateAsync(accounts, actorId, Command($"Sin efectivo {suffix}", expenseRoot.Id));
            await CreateAsync(
                accounts,
                actorId,
                Command($"Con efectivo {suffix}", cashGroup.Id, cashKind: CashAccountKind.Bank));
            Guid plainId = (await GetByNameAsync(accounts, $"Sin efectivo {suffix}")).Id;
            Guid cashId = (await GetByNameAsync(accounts, $"Con efectivo {suffix}")).Id;
            context.ChangeTracker.Clear();

            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET CashKind = 1 WHERE Id = '{plainId}'",
                "CK_CuentasContables_Cash");
            await AssertRejectsAsync(
                context,
                // 4 (Ingreso) es un valor permitido de Type, pero no Activo: aísla CK_CuentasContables_Cash
                // de CK_CuentasContables_Type.
                $"UPDATE catalogos.CuentasContables SET [Type] = 4 WHERE Id = '{cashId}'",
                "CK_CuentasContables_Cash");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET [Type] = 9 WHERE Id = '{plainId}'",
                "CK_CuentasContables_Type");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET CashKind = 3 WHERE Id = '{cashId}'",
                "CK_CuentasContables_CashKind");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET ParentId = Id WHERE Id = '{plainId}'",
                "CK_CuentasContables_Parent");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET Code = '   ' WHERE Id = '{plainId}'",
                "CK_CuentasContables_Codigo_NoVacio");
        });
    }

    private static async Task AssertRejectsAsync(FinancialDbContext context, string sql, string constraint)
    {
        Exception error = await Assert.ThrowsAnyAsync<Exception>(() =>
            context.Database.ExecuteSqlRawAsync(sql));
        Assert.Contains(constraint, error.ToString());
    }

    private static void AssertInvalid(CatalogOperationResult result, string expectedFragment)
    {
        Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
        Assert.Contains(expectedFragment, result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task CreateAsync(
        ILedgerAccountService accounts,
        Guid actorId,
        CreateLedgerAccountCommand command)
    {
        Assert.True((await accounts.CreateAsync(command, actorId)).IsSuccess);
    }

    private static SaveFinancialCategoryCommand CategoryCommand(
        string name,
        FinancialCategoryKind kind,
        Guid ledgerAccountId) =>
        new(name, kind, null, ledgerAccountId, null);

    private static async Task<FinancialCategoryModel> GetCategoryAsync(
        IFinancialCategoryService categories,
        string name)
    {
        PagedResult<FinancialCategoryModel> page = await categories.SearchAsync(
            new CatalogQuery(name, CatalogStatusFilter.All));
        return Assert.Single(page.Items, category => category.Name == name);
    }

    private static async Task<LedgerAccountModel> GetByCodeAsync(ILedgerAccountService accounts, string code)
    {
        PagedResult<LedgerAccountModel> page = await accounts.SearchAsync(
            new CatalogQuery(code, CatalogStatusFilter.All));
        return Assert.Single(page.Items, account => account.Code == code);
    }

    private static async Task<LedgerAccountModel> GetByNameAsync(ILedgerAccountService accounts, string name)
    {
        PagedResult<LedgerAccountModel> page = await accounts.SearchAsync(
            new CatalogQuery(name, CatalogStatusFilter.All));
        return Assert.Single(page.Items, account => account.Name == name);
    }

    private static CreateLedgerAccountCommand Command(
        string name,
        Guid parentId,
        CashAccountKind? cashKind = null,
        string? reference = null) =>
        new(name, parentId, cashKind, reference, null);

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
