using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Payables;

/// <summary>
/// Escenarios reales de <c>IPayableService.GetOverdueSummaryAsync</c> (regla 9) que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerPayableServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerPayableServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetOverdueSummaryAsync_ClassifiesOverdueAndUpcomingSeparately()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IPayableService payables = services.GetRequiredService<IPayableService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

            // Crédito 30 días emitido hace 35 días: vence hace 5 días (vencida).
            await CreateConfirmedCreditExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, today.AddDays(-35), 100m, actorId);

            // Crédito 30 días emitido hace 25 días: vence en 5 días (próxima, dentro de 7 días).
            await CreateConfirmedCreditExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, today.AddDays(-25), 200m, actorId);

            // Crédito 30 días emitido hoy: vence en 30 días (ni vencida ni próxima con alerta de 7).
            await CreateConfirmedCreditExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, today, 300m, actorId);

            OverdueSummary summary = await payables.GetOverdueSummaryAsync(7);

            Assert.Equal(1, summary.OverdueCount);
            Assert.Equal(100m, summary.OverdueAmount);
            Assert.Equal(1, summary.UpcomingCount);
            Assert.Equal(200m, summary.UpcomingAmount);
        });
    }

    private static async Task<Guid> CreateConfirmedCreditExpenseAsync(
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
                supplierId, categoryId, "Compra a crédito de pruebas de vencidas", issueDate,
                PaymentTerm.Credit30, netAmount, taxTypeId, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(null, null),
            draft.Version,
            actorId,
            actorIsManagement: false);

        return expenseId;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de vencidas",
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

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            "IVA 0%",
            0m,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateExpenseCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de pruebas de vencidas",
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
            "Categoría de gasto de pruebas de vencidas",
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
