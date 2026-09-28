using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
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
    public Task HierarchyScenario_EnforcesTypeCashRulesCyclesAndDependencies()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            Assert.True((await accounts.CreateAsync(
                Command($"A-{suffix}", "Activos", LedgerAccountType.Asset), actorId)).IsSuccess);
            LedgerAccountModel root = await GetByCodeAsync(accounts, $"A-{suffix}");

            // Una cuenta de efectivo puede colgar de una cuenta agrupadora del mismo tipo.
            Assert.True((await accounts.CreateAsync(
                Command(
                    $"B-{suffix}",
                    "Banco en dólares",
                    LedgerAccountType.Asset,
                    parentId: root.Id,
                    cashKind: CashAccountKind.Bank,
                    currency: CurrencyCode.USD,
                    reference: "Terminada en 4321"),
                actorId)).IsSuccess);
            LedgerAccountModel bank = await GetByCodeAsync(accounts, $"B-{suffix}");
            Assert.True(bank.IsCash);
            Assert.Equal(CashAccountKind.Bank, bank.CashKind);
            Assert.Equal(CurrencyCode.USD, bank.Currency);
            Assert.Equal("Activos", bank.ParentName);

            // Una cuenta de efectivo no puede tener hijas.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"H-{suffix}", "Hija de banco", LedgerAccountType.Asset, parentId: bank.Id), actorId),
                "efectivo");

            // La cuenta superior debe existir, estar activa y ser del mismo tipo.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"P-{suffix}", "Pasivo", LedgerAccountType.Liability, parentId: root.Id), actorId),
                "mismo tipo");
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"X-{suffix}", "Sin padre", LedgerAccountType.Asset, parentId: Guid.NewGuid()), actorId),
                "no existe");

            // Las reglas de efectivo y moneda las aplica el dominio.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"M-{suffix}", "Sin moneda", LedgerAccountType.Asset, cashKind: CashAccountKind.Cash),
                    actorId),
                "moneda");
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"N-{suffix}", "Moneda sobrante", LedgerAccountType.Asset, currency: CurrencyCode.CRC),
                    actorId),
                "efectivo");
            AssertInvalid(
                await accounts.CreateAsync(
                    Command(
                        $"G-{suffix}",
                        "Gasto de efectivo",
                        LedgerAccountType.Expense,
                        cashKind: CashAccountKind.Cash,
                        currency: CurrencyCode.CRC),
                    actorId),
                "Activo");

            CatalogOperationResult duplicate = await accounts.CreateAsync(
                Command($"A-{suffix}", "Repetida", LedgerAccountType.Asset), actorId);
            Assert.Equal(CatalogOperationStatus.Duplicate, duplicate.Status);
            context.ChangeTracker.Clear();

            // Actualización con control de concurrencia; tipo, subtipo y moneda no forman parte del comando.
            Assert.True((await accounts.CreateAsync(
                Command($"C-{suffix}", "Cuentas por cobrar", LedgerAccountType.Asset, parentId: root.Id), actorId))
                .IsSuccess);
            LedgerAccountModel receivable = await GetByCodeAsync(accounts, $"C-{suffix}");
            UpdateLedgerAccountCommand update = new(
                receivable.Code,
                "Cuentas por cobrar a clientes",
                root.Id,
                null,
                "Saldos de clientes");
            Assert.True((await accounts.UpdateAsync(receivable.Id, update, receivable.Version, actorId)).IsSuccess);
            Assert.Equal(
                CatalogOperationStatus.ConcurrencyConflict,
                (await accounts.UpdateAsync(receivable.Id, update, receivable.Version, actorId)).Status);

            LedgerAccountModel updatedBank = (await accounts.GetAsync(bank.Id))!;
            Assert.True((await accounts.UpdateAsync(
                bank.Id,
                new UpdateLedgerAccountCommand(bank.Code, "Banco USD renombrado", root.Id, bank.Reference, null),
                updatedBank.Version,
                actorId)).IsSuccess);
            LedgerAccountModel renamedBank = (await accounts.GetAsync(bank.Id))!;
            Assert.Equal("Banco USD renombrado", renamedBank.Name);
            Assert.Equal(CashAccountKind.Bank, renamedBank.CashKind);
            Assert.Equal(CurrencyCode.USD, renamedBank.Currency);
            Assert.Equal(LedgerAccountType.Asset, renamedBank.Type);

            // Una cuenta no puede ser su propia superior ni producir un ciclo, ni colgar de una de efectivo.
            receivable = (await accounts.GetAsync(receivable.Id))!;
            AssertInvalid(
                await accounts.UpdateAsync(
                    receivable.Id,
                    update with { ParentId = receivable.Id },
                    receivable.Version,
                    actorId),
                "propia");
            root = (await accounts.GetAsync(root.Id))!;
            AssertInvalid(
                await accounts.UpdateAsync(
                    root.Id,
                    new UpdateLedgerAccountCommand(root.Code, root.Name, receivable.Id, null, null),
                    root.Version,
                    actorId),
                "ciclo");
            AssertInvalid(
                await accounts.UpdateAsync(
                    receivable.Id,
                    update with { ParentId = bank.Id },
                    receivable.Version,
                    actorId),
                "efectivo");

            // No se desactiva una cuenta con hijas activas; se puede después de desactivarlas.
            root = (await accounts.GetAsync(root.Id))!;
            Assert.Equal(
                CatalogOperationStatus.DependencyConflict,
                (await accounts.SetActiveAsync(root.Id, false, root.Version, actorId)).Status);

            foreach (string childCode in new[] { $"B-{suffix}", $"C-{suffix}" })
            {
                LedgerAccountModel child = await GetByCodeAsync(accounts, childCode);
                Assert.True((await accounts.SetActiveAsync(child.Id, false, child.Version, actorId)).IsSuccess);
            }

            root = (await accounts.GetAsync(root.Id))!;
            Assert.True((await accounts.SetActiveAsync(root.Id, false, root.Version, actorId)).IsSuccess);

            // Reactivar una hija exige que su superior esté activa.
            LedgerAccountModel inactiveChild = await GetByCodeAsync(accounts, $"C-{suffix}");
            AssertInvalid(
                await accounts.SetActiveAsync(inactiveChild.Id, true, inactiveChild.Version, actorId),
                "inactiva");

            root = (await accounts.GetAsync(root.Id))!;
            Assert.True((await accounts.SetActiveAsync(root.Id, true, root.Version, actorId)).IsSuccess);
            inactiveChild = (await accounts.GetAsync(inactiveChild.Id))!;
            Assert.True((await accounts.SetActiveAsync(inactiveChild.Id, true, inactiveChild.Version, actorId)).IsSuccess);
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

            await CreateAsync(accounts, actorId, Command($"I-{suffix}", "Ingresos", LedgerAccountType.Income));
            LedgerAccountModel incomeRoot = await GetByCodeAsync(accounts, $"I-{suffix}");
            await CreateAsync(
                accounts,
                actorId,
                Command($"I1-{suffix}", "Ingresos por servicios", LedgerAccountType.Income, parentId: incomeRoot.Id));
            LedgerAccountModel incomeChild = await GetByCodeAsync(accounts, $"I1-{suffix}");
            await CreateAsync(
                accounts,
                actorId,
                Command($"I2-{suffix}", "Ingresos por ventas", LedgerAccountType.Income, parentId: incomeRoot.Id));
            LedgerAccountModel incomeSibling = await GetByCodeAsync(accounts, $"I2-{suffix}");
            await CreateAsync(accounts, actorId, Command($"E-{suffix}", "Gastos", LedgerAccountType.Expense));
            LedgerAccountModel expense = await GetByCodeAsync(accounts, $"E-{suffix}");
            await CreateAsync(
                accounts,
                actorId,
                Command(
                    $"CJ-{suffix}",
                    "Caja colones",
                    LedgerAccountType.Asset,
                    cashKind: CashAccountKind.Cash,
                    currency: CurrencyCode.CRC));
            await CreateAsync(
                accounts,
                actorId,
                Command(
                    $"BD-{suffix}",
                    "Banco dólares",
                    LedgerAccountType.Asset,
                    cashKind: CashAccountKind.Bank,
                    currency: CurrencyCode.USD));

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
            Assert.DoesNotContain(assetParents, option => option.Code == $"CJ-{suffix}");
            Assert.DoesNotContain(assetParents, option => option.Code == $"BD-{suffix}");

            // Las categorías eligen entre las cuentas activas de su tipo.
            IReadOnlyList<LedgerAccountOption> incomeOptions = await accounts.GetActiveOptionsAsync(LedgerAccountType.Income);
            Assert.Contains(incomeOptions, option => option.Id == incomeChild.Id);
            Assert.DoesNotContain(incomeOptions, option => option.Id == expense.Id);

            LedgerAccountModel deactivated = (await accounts.GetAsync(incomeSibling.Id))!;
            Assert.True((await accounts.SetActiveAsync(deactivated.Id, false, deactivated.Version, actorId)).IsSuccess);
            Assert.DoesNotContain(
                await accounts.GetActiveOptionsAsync(LedgerAccountType.Income),
                option => option.Id == incomeSibling.Id);

            // Las cajas y bancos se pueden listar por moneda.
            IReadOnlyList<CashAccountOption> colones = await accounts.GetActiveCashAccountsAsync(CurrencyCode.CRC);
            IReadOnlyList<CashAccountOption> dollars = await accounts.GetActiveCashAccountsAsync(CurrencyCode.USD);
            IReadOnlyList<CashAccountOption> all = await accounts.GetActiveCashAccountsAsync();
            Assert.Contains(colones, option => option.Code == $"CJ-{suffix}" && option.CashKind == CashAccountKind.Cash);
            Assert.DoesNotContain(colones, option => option.Code == $"BD-{suffix}");
            Assert.Contains(dollars, option => option.Code == $"BD-{suffix}" && option.Currency == CurrencyCode.USD);
            Assert.Contains(all, option => option.Code == $"CJ-{suffix}");
            Assert.Contains(all, option => option.Code == $"BD-{suffix}");
            Assert.All(colones, option => Assert.Equal(CurrencyCode.CRC, option.Currency));
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

            await CreateAsync(accounts, actorId, Command($"I1-{suffix}", "Ingresos uno", LedgerAccountType.Income));
            await CreateAsync(accounts, actorId, Command($"I2-{suffix}", "Ingresos dos", LedgerAccountType.Income));
            await CreateAsync(accounts, actorId, Command($"I3-{suffix}", "Ingresos inactiva", LedgerAccountType.Income));
            await CreateAsync(accounts, actorId, Command($"E1-{suffix}", "Gastos uno", LedgerAccountType.Expense));
            LedgerAccountModel income1 = await GetByCodeAsync(accounts, $"I1-{suffix}");
            LedgerAccountModel income2 = await GetByCodeAsync(accounts, $"I2-{suffix}");
            LedgerAccountModel inactiveIncome = await GetByCodeAsync(accounts, $"I3-{suffix}");
            LedgerAccountModel expense1 = await GetByCodeAsync(accounts, $"E1-{suffix}");
            Assert.True((await accounts.SetActiveAsync(
                inactiveIncome.Id, false, inactiveIncome.Version, actorId)).IsSuccess);

            // Una categoría de ingreso con una cuenta de Ingreso activa es válida y expone la cuenta.
            Assert.True((await categories.CreateAsync(
                CategoryCommand($"C1-{suffix}", FinancialCategoryKind.Income, income1.Id), actorId)).IsSuccess);
            FinancialCategoryModel category = await GetCategoryAsync(categories, $"C1-{suffix}");
            Assert.Equal(income1.Id, category.LedgerAccountId);
            Assert.Equal($"I1-{suffix}", category.LedgerAccountCode);
            Assert.Equal("Ingresos uno", category.LedgerAccountName);

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
                CategoryCommand(category.Code, FinancialCategoryKind.Income, income2.Id),
                category.Version,
                actorId)).IsSuccess);
            category = (await categories.GetAsync(category.Id))!;
            Assert.Equal(income2.Id, category.LedgerAccountId);
            AssertInvalid(
                await categories.UpdateAsync(
                    category.Id,
                    CategoryCommand(category.Code, FinancialCategoryKind.Income, expense1.Id),
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
            await CreateAsync(accounts, actorId, Command($"N-{suffix}", "Sin efectivo", LedgerAccountType.Expense));
            await CreateAsync(
                accounts,
                actorId,
                Command(
                    $"K-{suffix}",
                    "Con efectivo",
                    LedgerAccountType.Asset,
                    cashKind: CashAccountKind.Bank,
                    currency: CurrencyCode.CRC));
            Guid plainId = (await GetByCodeAsync(accounts, $"N-{suffix}")).Id;
            Guid cashId = (await GetByCodeAsync(accounts, $"K-{suffix}")).Id;
            context.ChangeTracker.Clear();

            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET Currency = 'CRC' WHERE Id = '{plainId}'",
                "CK_CuentasContables_Cash");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET CashKind = 1 WHERE Id = '{plainId}'",
                "CK_CuentasContables_Cash");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET [Type] = 2 WHERE Id = '{cashId}'",
                "CK_CuentasContables_Cash");
            await AssertRejectsAsync(
                context,
                $"UPDATE catalogos.CuentasContables SET Currency = NULL WHERE Id = '{cashId}'",
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
                $"UPDATE catalogos.CuentasContables SET Currency = 'EUR' WHERE Id = '{cashId}'",
                "CK_CuentasContables_Currency");
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
        string code,
        FinancialCategoryKind kind,
        Guid ledgerAccountId) =>
        new(code, "Categoría de prueba", kind, null, ledgerAccountId, null);

    private static async Task<FinancialCategoryModel> GetCategoryAsync(
        IFinancialCategoryService categories,
        string code)
    {
        PagedResult<FinancialCategoryModel> page = await categories.SearchAsync(
            new CatalogQuery(code, CatalogStatusFilter.All));
        return Assert.Single(page.Items, category => category.Code == code);
    }

    private static async Task<LedgerAccountModel> GetByCodeAsync(ILedgerAccountService accounts, string code)
    {
        PagedResult<LedgerAccountModel> page = await accounts.SearchAsync(
            new CatalogQuery(code, CatalogStatusFilter.All));
        return Assert.Single(page.Items, account => account.Code == code);
    }

    private static CreateLedgerAccountCommand Command(
        string code,
        string name,
        LedgerAccountType type,
        Guid? parentId = null,
        CashAccountKind? cashKind = null,
        CurrencyCode? currency = null,
        string? reference = null) =>
        new(code, name, type, parentId, cashKind, currency, reference, null);

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
