using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Domain.Budgets;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Budgets;

/// <summary>
/// Escenarios reales de <c>IBudgetService</c> y <c>IExpenseService.CheckBudgetAsync</c> que se
/// habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerBudgetServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerBudgetServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SetAmount_FirstConfiguration_HasNoPreviousAmount()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid categoryId = await CreateLeafExpenseCategoryAsync(services, actorId);

            CatalogOperationResult result = await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(categoryId, 2026, 10, 100_000m),
                version: null,
                actorId);

            Assert.True(result.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Budget budget = await context.Budgets.SingleAsync(b => b.CategoryId == categoryId);
            Assert.Equal(100_000m, budget.Amount);

            BudgetChange change = await context.BudgetChanges.SingleAsync(c => c.BudgetId == budget.Id);
            Assert.Null(change.PreviousAmount);
            Assert.Equal(100_000m, change.NewAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SetAmount_SubsequentCorrection_KeepsThePreviousAmount()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid categoryId = await CreateLeafExpenseCategoryAsync(services, actorId);

            await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(categoryId, 2026, 10, 100_000m), null, actorId);
            BudgetModel? first = await budgets.GetAsync(categoryId, 2026, 10);

            CatalogOperationResult result = await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(categoryId, 2026, 10, 150_000m),
                first!.Version,
                actorId);

            Assert.True(result.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Budget budget = await context.Budgets.SingleAsync(b => b.CategoryId == categoryId);
            Assert.Equal(150_000m, budget.Amount);

            BudgetChange latestChange = await context.BudgetChanges
                .Where(c => c.BudgetId == budget.Id)
                .OrderByDescending(c => c.ChangedAtUtc)
                .FirstAsync();
            Assert.Equal(100_000m, latestChange.PreviousAmount);
            Assert.Equal(150_000m, latestChange.NewAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SetAmount_RejectsACategoryThatHasChildren()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            Guid parentCategoryId = await CreateParentExpenseCategoryWithChildAsync(services, actorId);

            CatalogOperationResult result = await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(parentCategoryId, 2026, 10, 100_000m), null, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SearchByPeriod_OnlyConfirmedExpensesOfThatMonthCountAsExecuted()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid categoryId = await CreateLeafExpenseCategoryAsync(services, actorId);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");

            await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(categoryId, 2026, 10, 1000m), null, actorId);

            // Confirmado dentro del mes: cuenta.
            Guid confirmedExpenseId = await CreateAndConfirmCashExpenseAsync(
                services, expenses, supplierId, categoryId, taxTypeId, new DateOnly(2026, 10, 15), 400m, actorId);

            // Borrador: no cuenta.
            await CreateDraftExpenseAsync(expenses, supplierId, categoryId, taxTypeId, new DateOnly(2026, 10, 16), 9000m, actorId);

            IReadOnlyList<BudgetModel> results = await budgets.SearchByPeriodAsync(2026, 10);
            BudgetModel model = results.Single(m => m.CategoryId == categoryId);

            Assert.Equal(1000m, model.BudgetedAmount);
            Assert.Equal(400m, model.ExecutedAmount);
            Assert.Equal(600m, model.AvailableAmount);
            Assert.Equal(-600m, model.VarianceAmount);

            // Anular el gasto confirmado lo saca de lo ejecutado sin tocar el presupuesto.
            ExpenseModel confirmed = (await expenses.GetAsync(confirmedExpenseId))!;
            await expenses.CancelAsync(confirmedExpenseId, "Error de captura", confirmed.Version, actorId);

            IReadOnlyList<BudgetModel> afterCancel = await budgets.SearchByPeriodAsync(2026, 10);
            BudgetModel modelAfterCancel = afterCancel.Single(m => m.CategoryId == categoryId);
            Assert.Equal(0m, modelAfterCancel.ExecutedAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CheckBudgetAsync_WithoutABudget_ReportsNoBudgetAndNeverExceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid categoryId = await CreateLeafExpenseCategoryAsync(services, actorId);

            ExpenseBudgetCheck check = await expenses.CheckBudgetAsync(
                categoryId, new DateOnly(2026, 10, 15), 1_000_000m);

            Assert.False(check.HasBudget);
            Assert.False(check.ExceedsAvailable);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CheckBudgetAsync_WithABudget_ReportsWhetherItWouldExceedTheAvailableAmount()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IBudgetService budgets = services.GetRequiredService<IBudgetService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid categoryId = await CreateLeafExpenseCategoryAsync(services, actorId);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");

            await budgets.SetAmountAsync(
                new SetBudgetAmountCommand(categoryId, 2026, 10, 1000m), null, actorId);
            await CreateAndConfirmCashExpenseAsync(
                services, expenses, supplierId, categoryId, taxTypeId, new DateOnly(2026, 10, 1), 700m, actorId);

            ExpenseBudgetCheck withinBudget = await expenses.CheckBudgetAsync(
                categoryId, new DateOnly(2026, 10, 20), 300m);
            Assert.True(withinBudget.HasBudget);
            Assert.False(withinBudget.ExceedsAvailable);

            ExpenseBudgetCheck overBudget = await expenses.CheckBudgetAsync(
                categoryId, new DateOnly(2026, 10, 20), 301m);
            Assert.True(overBudget.HasBudget);
            Assert.True(overBudget.ExceedsAvailable);
        });
    }

    private static async Task<Guid> CreateAndConfirmCashExpenseAsync(
        IServiceProvider services,
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Gasto de prueba", issueDate, PaymentTerm.Cash, netAmount, taxTypeId, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
        await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(cashAccountId, "REC-BUDGET"),
            draft.Version,
            actorId,
            actorIsManagement: true);

        return expenseId;
    }

    private static async Task<Guid> CreateDraftExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        DateOnly issueDate,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Borrador de prueba", issueDate, PaymentTerm.Cash, netAmount, taxTypeId, PaymentMethod.Cash),
            actorId);
        return created.ExpenseId!.Value;
    }

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Caja de pruebas de presupuesto",
            LedgerAccountType.Asset,
            null,
            CashAccountKind.Cash,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de presupuesto",
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();
        return supplier.Id;
    }

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId, string ratePercent)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            $"IVA {ratePercent}%",
            decimal.Parse(ratePercent, System.Globalization.CultureInfo.InvariantCulture),
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateLeafExpenseCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de pruebas de presupuesto",
            LedgerAccountType.Expense,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(ledgerAccount);
        await context.SaveChangesAsync();

        FinancialCategory category = new(
            Guid.NewGuid(),
            $"CAT-{suffix}",
            "Categoría hoja de pruebas de presupuesto",
            FinancialCategoryKind.Expense,
            null,
            ledgerAccount.Id,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.FinancialCategories.Add(category);
        await context.SaveChangesAsync();
        return category.Id;
    }

    private static async Task<Guid> CreateParentExpenseCategoryWithChildAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GASP-{suffix}",
            "Cuenta de gasto superior de pruebas",
            LedgerAccountType.Expense,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(ledgerAccount);
        await context.SaveChangesAsync();

        FinancialCategory parent = new(
            Guid.NewGuid(),
            $"CATP-{suffix}",
            "Categoría superior de pruebas",
            FinancialCategoryKind.Expense,
            null,
            ledgerAccount.Id,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.FinancialCategories.Add(parent);
        await context.SaveChangesAsync();

        FinancialCategory child = new(
            Guid.NewGuid(),
            $"CATH-{suffix}",
            "Categoría hija de pruebas",
            FinancialCategoryKind.Expense,
            parent.Id,
            ledgerAccount.Id,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.FinancialCategories.Add(child);
        await context.SaveChangesAsync();

        return parent.Id;
    }

    private static async Task<Guid> CreateUserAsync(IServiceProvider services)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        Guid userId = Guid.NewGuid();
        string email = $"sql-test-{userId:N}@example.test";

        context.Users.Add(new ApplicationUser
        {
            Id = userId,
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
        Guid actorId = await CreateUserAsync(scope.ServiceProvider);

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
