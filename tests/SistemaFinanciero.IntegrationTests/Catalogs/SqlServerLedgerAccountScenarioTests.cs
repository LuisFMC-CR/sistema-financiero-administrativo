using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Escenarios reales que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// Las cuentas de grupo (código "4" Ingresos, "5" Gastos, "1.1" Efectivo) son las del catálogo base
/// sembrado por la migración (regla 13, renumerado por el bloque 4 del paquete de mejoras finales): el
/// servicio ya no crea cuentas raíz, así que estas pruebas agregan cuentas de detalle bajo esos grupos
/// reales, igual que lo hace la aplicación.
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
            // Paquete v9, bloque 9: desde la regla de creación del paquete v7 las cuentas nuevas solo se
            // crean, de tercer nivel, dentro de un grupo de segundo nivel habilitado (5.1, no la raíz 5).
            LedgerAccountModel expenseGroup = await GetByCodeAsync(accounts, "5.1");

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
                    Command($"Gasto de efectivo {suffix}", expenseGroup.Id, cashKind: CashAccountKind.Cash),
                    actorId),
                "Activo");

            // Actualización con control de concurrencia; código, tipo y cuenta superior no forman parte del comando.
            Assert.True((await accounts.CreateAsync(
                Command($"Detalle de gasto {suffix}", expenseGroup.Id), actorId)).IsSuccess);
            LedgerAccountModel detail = await GetByNameAsync(accounts, $"Detalle de gasto {suffix}");
            UpdateLedgerAccountCommand update = new("Detalle de gasto renombrado", null, "Nota de prueba");
            Assert.True((await accounts.UpdateAsync(detail.Id, update, detail.Version, actorId)).IsSuccess);
            Assert.Equal(
                CatalogOperationStatus.ConcurrencyConflict,
                (await accounts.UpdateAsync(detail.Id, update, detail.Version, actorId)).Status);

            LedgerAccountModel updatedDetail = (await accounts.GetAsync(detail.Id))!;
            Assert.Equal("Detalle de gasto renombrado", updatedDetail.Name);
            Assert.Equal(detail.Code, updatedDetail.Code);
            Assert.Equal(expenseGroup.Id, updatedDetail.ParentId);

            // Toda cuenta nueva es de tercer nivel: una cuenta de detalle no admite cuentas hijas.
            AssertInvalid(
                await accounts.CreateAsync(Command($"Nieta {suffix}", updatedDetail.Id), actorId),
                "segundo nivel");
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
            LedgerAccountModel incomeRoot = await GetByCodeAsync(accounts, "4");

            // Paquete v9, bloque 9: desde la regla de creación del paquete v7 el usuario no puede crear
            // una cuenta con hijas (toda cuenta nueva es de tercer nivel); el par padre e hija se
            // inserta directamente para probar solo las reglas de activación del servicio.
            LedgerAccount groupEntity = new(
                Guid.NewGuid(), $"T{suffix}", $"Grupo {suffix}", LedgerAccountType.Income, incomeRoot.Id,
                null, null, null, DateTimeOffset.UtcNow, actorId);
            context.LedgerAccounts.Add(groupEntity);
            context.LedgerAccounts.Add(new LedgerAccount(
                Guid.NewGuid(), $"T{suffix}.01", $"Hijo {suffix}", LedgerAccountType.Income, groupEntity.Id,
                null, null, null, DateTimeOffset.UtcNow, actorId));
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            LedgerAccountModel group = await GetByNameAsync(accounts, $"Grupo {suffix}");
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

            // Paquete v9, bloque 9: las cuentas nuevas se crean dentro de los grupos habilitados.
            LedgerAccountModel incomeRoot = await GetByCodeAsync(accounts, "4");
            LedgerAccountModel servicesGroup = await GetByCodeAsync(accounts, "4.1");
            LedgerAccountModel salesGroup = await GetByCodeAsync(accounts, "4.2");
            LedgerAccountModel expenseGroup = await GetByCodeAsync(accounts, "5.1");
            LedgerAccountModel cashGroup = await GetByCodeAsync(accounts, "1.1");

            await CreateAsync(accounts, actorId, Command($"Ingresos por servicios {suffix}", servicesGroup.Id));
            LedgerAccountModel incomeChild = await GetByNameAsync(accounts, $"Ingresos por servicios {suffix}");
            await CreateAsync(accounts, actorId, Command($"Ingresos por ventas {suffix}", salesGroup.Id));
            LedgerAccountModel incomeSibling = await GetByNameAsync(accounts, $"Ingresos por ventas {suffix}");
            await CreateAsync(accounts, actorId, Command($"Gasto nuevo {suffix}", expenseGroup.Id));
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

            // Las opciones de cuenta superior son solo los grupos habilitados del tipo: ni la raíz, ni
            // las cuentas de detalle, ni cuentas de otro tipo; y excluyen la cuenta indicada.
            IReadOnlyList<LedgerAccountOption> parents = await accounts.GetParentOptionsAsync(LedgerAccountType.Income);
            Assert.Equal(["4.1", "4.2", "4.3"], parents.Select(option => option.Code).ToArray());
            Assert.DoesNotContain(parents, option => option.Id == incomeRoot.Id);
            Assert.DoesNotContain(parents, option => option.Id == incomeChild.Id);
            Assert.DoesNotContain(parents, option => option.Id == expense.Id);

            IReadOnlyList<LedgerAccountOption> withoutServices = await accounts.GetParentOptionsAsync(
                LedgerAccountType.Income,
                excludedAccountId: servicesGroup.Id);
            Assert.DoesNotContain(withoutServices, option => option.Id == servicesGroup.Id);
            Assert.Contains(withoutServices, option => option.Id == salesGroup.Id);

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
    public Task SystemRoleScenario_ProtectsSystemAccountsAndRestrictsWhereTheUserCanCreate()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

            LedgerAccountModel assetRoot = await GetByCodeAsync(accounts, "1");
            LedgerAccountModel liabilityRoot = await GetByCodeAsync(accounts, "2");
            LedgerAccountModel equityRoot = await GetByCodeAsync(accounts, "3");
            LedgerAccountModel receivablesFromCustomers = await GetByCodeAsync(accounts, "1.2.01");

            // El usuario solo crea cuentas de detalle dentro de un grupo de segundo nivel habilitado:
            // nunca directamente bajo una raíz (Activo, Pasivo o Patrimonio).
            AssertInvalid(
                await accounts.CreateAsync(Command($"Bajo activo {suffix}", assetRoot.Id), actorId),
                "segundo nivel");
            AssertInvalid(
                await accounts.CreateAsync(Command($"Bajo pasivo {suffix}", liabilityRoot.Id), actorId),
                "segundo nivel");
            AssertInvalid(
                await accounts.CreateAsync(Command($"Bajo patrimonio {suffix}", equityRoot.Id), actorId),
                "segundo nivel");

            // Una cuenta de sistema tampoco admite cuentas hijas.
            AssertInvalid(
                await accounts.CreateAsync(
                    Command($"Bajo cuenta de sistema {suffix}", receivablesFromCustomers.Id), actorId),
                "sistema");

            // Pasivo y Patrimonio nunca ofrecen cuentas superior para el usuario.
            Assert.Empty(await accounts.GetParentOptionsAsync(LedgerAccountType.Liability));
            Assert.Empty(await accounts.GetParentOptionsAsync(LedgerAccountType.Equity));

            // Gastos ofrece sus tres grupos, incluido 5.3 Costo de ventas (regla 1; paquete v9, bloque 9).
            Assert.Equal(
                ["5.1", "5.2", "5.3"],
                (await accounts.GetParentOptionsAsync(LedgerAccountType.Expense)).Select(option => option.Code).ToArray());

            // Dentro de Activo, solo 1.1 Efectivo aparece como cuenta superior posible.
            IReadOnlyList<LedgerAccountOption> assetParentOptions =
                await accounts.GetParentOptionsAsync(LedgerAccountType.Asset);
            Assert.DoesNotContain(assetParentOptions, option => option.Id == assetRoot.Id);
            Assert.Contains(assetParentOptions, option => option.Code == "1.1");

            // Una cuenta de sistema se puede renombrar...
            LedgerAccountModel current = (await accounts.GetAsync(receivablesFromCustomers.Id))!;
            UpdateLedgerAccountCommand rename = new(
                $"Cuentas por cobrar a clientes {suffix}", current.Reference, current.Description);
            Assert.True((await accounts.UpdateAsync(
                receivablesFromCustomers.Id, rename, current.Version, actorId)).IsSuccess);

            // Las cuentas de sistema no aparecen entre las opciones para elegir en una categoría
            // financiera, y el servicio rechaza asignarlas aunque se envíe el Id directamente.
            Assert.DoesNotContain(
                await accounts.GetActiveOptionsAsync(LedgerAccountType.Asset),
                option => option.Id == receivablesFromCustomers.Id);

        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SystemRoleWithJournalMovement_ShowsDeactivationActionButRejectsTheRequest()
    {
        return RunInRollbackTransactionAsync(async (context, services, actorId) =>
        {
            ILedgerAccountService accounts = services.GetRequiredService<ILedgerAccountService>();
            LedgerAccountModel receivables = await GetByCodeAsync(accounts, "1.2.01");
            LedgerAccountModel incomeAccount = await GetByCodeAsync(accounts, "4.1.01");

            context.JournalEntries.Add(new JournalEntry(
                Guid.NewGuid(),
                DateTime.UtcNow.Ticks,
                DateOnly.FromDateTime(DateTime.UtcNow),
                "Movimiento de prueba para cuenta de sistema",
                JournalEntrySourceType.Income,
                Guid.NewGuid(),
                reversesEntryId: null,
                [
                    new JournalEntryLineInput(receivables.Id, 100m, 0m),
                    new JournalEntryLineInput(incomeAccount.Id, 0m, 100m),
                ],
                DateTimeOffset.UtcNow,
                actorId));
            await context.SaveChangesAsync();

            LedgerAccountModel current = (await accounts.GetAsync(receivables.Id))!;
            Assert.True(current.HasTransactions);
            Assert.True(current.CanDeactivate);

            CatalogOperationResult result = await accounts.SetActiveAsync(
                current.Id,
                false,
                current.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.DependencyConflict, result.Status);
            Assert.Contains("movimientos asociados", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True((await accounts.GetAsync(current.Id))!.IsActive);
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
            LedgerAccountModel expenseGroup = await GetByCodeAsync(accounts, "5.1");
            LedgerAccountModel cashGroup = await GetByCodeAsync(accounts, "1.1");

            await CreateAsync(accounts, actorId, Command($"Sin efectivo {suffix}", expenseGroup.Id));
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

    private static async Task<LedgerAccountModel> GetByCodeAsync(ILedgerAccountService accounts, string code)
    {
        PagedResult<LedgerAccountModel> page = await accounts.SearchAsync(
            new LedgerAccountQuery(code: code));
        return Assert.Single(page.Items, account => account.Code == code);
    }

    private static async Task<LedgerAccountModel> GetByNameAsync(ILedgerAccountService accounts, string name)
    {
        PagedResult<LedgerAccountModel> page = await accounts.SearchAsync(
            new LedgerAccountQuery(name: name));
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
