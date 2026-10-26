using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Adjustments;

/// <summary>
/// Escenarios reales de <c>IBalanceAdjustmentService</c> que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos (regla de negocio 4).
/// </summary>
public sealed class SqlServerBalanceAdjustmentServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerBalanceAdjustmentServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AdjustReceivableAsync_ACorrectionInTheOppositeDirectionRestoresTheOriginalBalance()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoiceService = services.GetRequiredService<IInvoiceService>();
            IReceivableService receivableService = services.GetRequiredService<IReceivableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoiceService, customerId, actorId, amount: 1000m);
            string version = (await receivableService.GetAsync(receivableId))!.Version;

            CatalogOperationResult decreaseResult = await adjustments.AdjustReceivableAsync(
                receivableId, 300m, BalanceAdjustmentDirection.Decrease, "Ajuste por error de digitación",
                version, actorId);
            Assert.True(decreaseResult.IsSuccess);

            version = (await receivableService.GetAsync(receivableId))!.Version;
            CatalogOperationResult increaseResult = await adjustments.AdjustReceivableAsync(
                receivableId, 300m, BalanceAdjustmentDirection.Increase, "Corrección del ajuste anterior",
                version, actorId);
            Assert.True(increaseResult.IsSuccess);

            ReceivableModel updated = (await receivableService.GetAsync(receivableId))!;
            Assert.Equal(1000m, updated.Balance);
            Assert.Equal(ReceivableStatus.Pending, updated.Status);

            IReadOnlyList<BalanceAdjustmentModel> history = await adjustments.SearchByReceivableAsync(receivableId);
            Assert.Equal(2, history.Count);
            Assert.Contains(history, item => item.Direction == BalanceAdjustmentDirection.Decrease && item.Amount == 300m);
            Assert.Contains(history, item => item.Direction == BalanceAdjustmentDirection.Increase && item.Amount == 300m);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            int logEntries = await context.Set<OperationLogEntry>().CountAsync(
                entry => entry.EntityId == receivableId
                    && entry.EntityType == OperationLogEntityType.Receivable
                    && entry.Action == OperationLogAction.Adjusted);
            Assert.Equal(2, logEntries);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AdjustReceivableAsync_RejectsAnAdjustmentThatWouldExceedTheOriginalAmount()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoiceService = services.GetRequiredService<IInvoiceService>();
            IReceivableService receivableService = services.GetRequiredService<IReceivableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoiceService, customerId, actorId, amount: 1000m);
            string version = (await receivableService.GetAsync(receivableId))!.Version;

            CatalogOperationResult result = await adjustments.AdjustReceivableAsync(
                receivableId, 0.01m, BalanceAdjustmentDirection.Increase, "Aumento inválido", version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
            Assert.Equal(1000m, (await receivableService.GetAsync(receivableId))!.Balance);
            Assert.Empty(await adjustments.SearchByReceivableAsync(receivableId));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AdjustPayableAsync_WithDecrease_ReducesTheBalanceAndLogsTheAdjustment()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenseService = services.GetRequiredService<IExpenseService>();
            IPayableService payableService = services.GetRequiredService<IPayableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);

            Guid payableId = await CreateConfirmedCreditPayableAsync(
                services, expenseService, supplierId, actorId, amount: 1000m);
            string version = (await payableService.GetAsync(payableId))!.Version;

            CatalogOperationResult result = await adjustments.AdjustPayableAsync(
                payableId, 150m, BalanceAdjustmentDirection.Decrease, "Descuento acordado con el proveedor",
                version, actorId);

            Assert.True(result.IsSuccess);

            PayableModel updated = (await payableService.GetAsync(payableId))!;
            Assert.Equal(850m, updated.Balance);
            Assert.Equal(PayableStatus.PartiallyPaid, updated.Status);

            BalanceAdjustmentModel adjustment = Assert.Single(await adjustments.SearchByPayableAsync(payableId));
            Assert.Equal(150m, adjustment.Amount);
            Assert.Equal(BalanceAdjustmentDirection.Decrease, adjustment.Direction);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AdjustPayableAsync_WithAStaleVersion_ReturnsConcurrencyConflict()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenseService = services.GetRequiredService<IExpenseService>();
            IPayableService payableService = services.GetRequiredService<IPayableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);

            Guid payableId = await CreateConfirmedCreditPayableAsync(
                services, expenseService, supplierId, actorId, amount: 1000m);
            string staleVersion = (await payableService.GetAsync(payableId))!.Version;

            CatalogOperationResult firstAdjustment = await adjustments.AdjustPayableAsync(
                payableId, 100m, BalanceAdjustmentDirection.Decrease, "Primer ajuste", staleVersion, actorId);
            Assert.True(firstAdjustment.IsSuccess);

            CatalogOperationResult secondAdjustment = await adjustments.AdjustPayableAsync(
                payableId, 50m, BalanceAdjustmentDirection.Decrease, "Segundo ajuste con versión vieja",
                staleVersion, actorId);

            Assert.Equal(CatalogOperationStatus.ConcurrencyConflict, secondAdjustment.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AdjustReceivableAsync_WhenTodayIsClosed_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoiceService = services.GetRequiredService<IInvoiceService>();
            IReceivableService receivableService = services.GetRequiredService<IReceivableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoiceService, customerId, actorId, amount: 1000m);
            string version = (await receivableService.GetAsync(receivableId))!.Version;

            await SetClosingDateAsync(services, actorId, DateOnly.FromDateTime(DateTime.UtcNow));

            CatalogOperationResult result = await adjustments.AdjustReceivableAsync(
                receivableId, 100m, BalanceAdjustmentDirection.Decrease, "Ajuste de prueba", version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
            Assert.Equal(1000m, (await receivableService.GetAsync(receivableId))!.Balance);
        });
    }

    private static async Task SetClosingDateAsync(IServiceProvider services, Guid actorId, DateOnly closingDate)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            parameters = new SystemParameters(500_000m, 7, DateTimeOffset.UtcNow, actorId);
            context.SystemParameters.Add(parameters);
        }

        parameters.ChangeClosingDate(closingDate, closingDate, reason: null, DateTimeOffset.UtcNow, actorId);
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid actorId,
        decimal amount)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(null, "Servicio a crédito de pruebas de ajustes", "unidad", 1m, amount, 0m, taxTypeId),
            draft.Version,
            actorId);

        draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(
            invoiceId,
            new ConfirmInvoiceCommand(null, null, null, null),
            draft.Version,
            actorId);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Receivables.AsNoTracking().SingleAsync(r => r.InvoiceId == invoiceId)).Id;
    }

    private static async Task<Guid> CreateConfirmedCreditPayableAsync(
        IServiceProvider services,
        IExpenseService expenses,
        Guid supplierId,
        Guid actorId,
        decimal amount)
    {
        Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);

        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Compra a crédito de pruebas de ajustes", DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentTerm.Credit30, amount, taxTypeId, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(null, null),
            draft.Version,
            actorId,
            actorIsManagement: false);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Payables.AsNoTracking().SingleAsync(p => p.ExpenseId == expenseId)).Id;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de ajustes",
            $"ID-{suffix}",
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de ajustes",
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

    private static async Task<Guid> CreateExpenseCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de pruebas de ajustes",
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
            "Categoría de pruebas de ajustes",
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

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            "IVA 0% de pruebas de ajustes",
            0m,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
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
