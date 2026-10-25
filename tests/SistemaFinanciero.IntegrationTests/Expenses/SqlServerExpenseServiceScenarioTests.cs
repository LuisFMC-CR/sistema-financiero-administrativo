using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Expenses;

/// <summary>
/// Escenarios reales de <c>IExpenseService</c> que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1
/// y siempre revierten datos. Sigue el mismo patrón que <c>SqlServerInvoiceScenarioTests</c> y
/// <c>SqlServerReceivablePaymentServiceScenarioTests</c>.
/// </summary>
public sealed class SqlServerExpenseServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerExpenseServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Confirm_CashWithinTheLimit_SavesTheCashAccountAndReferenceWithoutAPayable()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Cash, 1000m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;

            CatalogOperationResult result = await expenses.ConfirmAsync(
                expenseId,
                new ConfirmExpenseCommand(cashAccountId, "REC-0001"),
                draft.Version,
                actorId,
                actorIsManagement: false);

            Assert.True(result.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Expense confirmed = await context.Expenses.SingleAsync(e => e.Id == expenseId);
            Assert.Equal(ExpenseStatus.Confirmed, confirmed.Status);
            Assert.Equal(cashAccountId, confirmed.CashAccountId);
            Assert.Equal("REC-0001", confirmed.Reference);
            Assert.False(await context.Payables.AnyAsync(p => p.ExpenseId == expenseId));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Confirm_Credit_CreatesThePayableWithTheExpenseTotalAndDueDate()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Credit30, 1000m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;

            CatalogOperationResult result = await expenses.ConfirmAsync(
                expenseId,
                new ConfirmExpenseCommand(null, null),
                draft.Version,
                actorId,
                actorIsManagement: false);

            Assert.True(result.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Expense confirmed = await context.Expenses.SingleAsync(e => e.Id == expenseId);
            Payable payable = await context.Payables.SingleAsync(p => p.ExpenseId == expenseId);
            Assert.Equal(confirmed.TotalAmount, payable.OriginalAmount);
            Assert.Equal(confirmed.DueDate, payable.DueDate);
            Assert.Equal(PayableStatus.Pending, payable.Status);
            Assert.Equal(supplierId, payable.SupplierId);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Confirm_CashAboveTheLimitWithoutManagement_IsRejectedButManagementCanConfirmIt()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 50m);

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Cash, 100m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;

            ConfirmExpenseCommand command = new(cashAccountId, "REC-0002");

            CatalogOperationResult blocked = await expenses.ConfirmAsync(
                expenseId, command, draft.Version, actorId, actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Expense stillDraft = await context.Expenses.SingleAsync(e => e.Id == expenseId);
            Assert.Equal(ExpenseStatus.Draft, stillDraft.Status);

            CatalogOperationResult allowed = await expenses.ConfirmAsync(
                expenseId, command, draft.Version, actorId, actorIsManagement: true);
            Assert.True(allowed.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Cancel_OnACreditExpenseWithoutPayments_CancelsItsPayable()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Credit30, 1000m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(null, null), draft.Version, actorId, actorIsManagement: false);

            ExpenseModel confirmed = (await expenses.GetAsync(expenseId))!;
            CatalogOperationResult result = await expenses.CancelAsync(
                expenseId, "Compra registrada por error", confirmed.Version, actorId);

            Assert.True(result.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Expense cancelled = await context.Expenses.SingleAsync(e => e.Id == expenseId);
            Assert.Equal(ExpenseStatus.Cancelled, cancelled.Status);

            Payable payable = await context.Payables.SingleAsync(p => p.ExpenseId == expenseId);
            Assert.Equal(PayableStatus.Cancelled, payable.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Cancel_OnACreditExpenseWithAnActivePayment_IsBlockedAndKeepsEverythingUnchanged()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Credit30, 1000m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(null, null), draft.Version, actorId, actorIsManagement: false);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Payable payable = await context.Payables.SingleAsync(p => p.ExpenseId == expenseId);

            // Todavía no existe SupplierPaymentService (paso 3, bloque 2): se simula un pago vigente
            // directamente sobre el dominio para probar que la anulación lo bloquea.
            payable.ApplyPayment(400m, DateTimeOffset.UtcNow, actorId);
            await context.SaveChangesAsync();

            ExpenseModel confirmed = (await expenses.GetAsync(expenseId))!;
            CatalogOperationResult blocked = await expenses.CancelAsync(
                expenseId, "Intento de anulación", confirmed.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Status);

            Expense stillConfirmed = await context.Expenses.SingleAsync(e => e.Id == expenseId);
            Assert.Equal(ExpenseStatus.Confirmed, stillConfirmed.Status);

            Payable stillPartiallyPaid = await context.Payables.SingleAsync(p => p.ExpenseId == expenseId);
            Assert.Equal(PayableStatus.PartiallyPaid, stillPartiallyPaid.Status);
        });
    }

    private static async Task<Guid> CreateDraftExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        PaymentTerm paymentTerm,
        decimal netAmount,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId,
                categoryId,
                "Compra de prueba",
                DateOnly.FromDateTime(DateTime.UtcNow),
                paymentTerm,
                netAmount,
                taxTypeId,
                PaymentMethod.Cash),
            actorId);

        Assert.True(created.Operation.IsSuccess);
        return created.ExpenseId!.Value;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de gastos",
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

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Caja de pruebas de gastos",
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

    private static async Task<Guid> CreateExpenseCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de pruebas",
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
            "Categoría de gasto de pruebas",
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

    private static async Task SetAuthorizationLimitAsync(IServiceProvider services, Guid actorId, decimal limit)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        context.SystemParameters.Add(new SystemParameters(limit, 7, DateTimeOffset.UtcNow, actorId));
        await context.SaveChangesAsync();
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
