using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Payables;

/// <summary>
/// Escenarios reales de <c>ISupplierPaymentService</c> que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Sigue el mismo patrón que
/// <c>SqlServerReceivablePaymentServiceScenarioTests</c>, sin retención.
/// </summary>
public sealed class SqlServerSupplierPaymentServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerSupplierPaymentServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_ToASinglePayable_AppliesThePayment()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService payments = services.GetRequiredService<ISupplierPaymentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid payableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);

            RegisterSupplierPaymentResult result = await payments.RegisterAsync(
                new RegisterSupplierPaymentCommand(
                    supplierId,
                    cashAccountId,
                    "PAG-0001",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new SupplierPaymentAllocationCommand(payableId, 60m)]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.Single(result.SupplierPaymentIds);
            Assert.False(result.DuplicateReferenceWarning);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Payable payable = await context.Payables.SingleAsync(p => p.Id == payableId);
            Assert.Equal(40m, payable.Balance);
            Assert.Equal(PayableStatus.PartiallyPaid, payable.Status);

            SupplierPayment payment = await context.SupplierPayments.SingleAsync(
                p => p.Id == result.SupplierPaymentIds[0]);
            Assert.Equal(60m, payment.Amount);
            Assert.Equal(SupplierPaymentStatus.Active, payment.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_ToSeveralPayablesOfTheSameSupplier_SharesTheGroupIdAndCancelsIndividually()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService payments = services.GetRequiredService<ISupplierPaymentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid firstPayableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);
            Guid secondPayableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);

            RegisterSupplierPaymentResult result = await payments.RegisterAsync(
                new RegisterSupplierPaymentCommand(
                    supplierId,
                    cashAccountId,
                    "PAG-0002",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new SupplierPaymentAllocationCommand(firstPayableId, 100m),
                        new SupplierPaymentAllocationCommand(secondPayableId, 50m),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.Equal(2, result.SupplierPaymentIds.Count);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            SupplierPayment firstPayment = await context.SupplierPayments.SingleAsync(
                p => p.PayableId == firstPayableId);
            SupplierPayment secondPayment = await context.SupplierPayments.SingleAsync(
                p => p.PayableId == secondPayableId);
            Assert.Equal(firstPayment.GroupId, secondPayment.GroupId);

            // Anular uno no debe afectar al otro (regla 2: "cada uno se anula de forma individual").
            SupplierPaymentModel? firstModel = await payments.GetAsync(firstPayment.Id);
            CatalogOperationResult cancel = await payments.CancelAsync(
                firstPayment.Id,
                "Pago registrado por error",
                firstModel!.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            Payable firstPayable = await context.Payables.SingleAsync(p => p.Id == firstPayableId);
            Assert.Equal(100m, firstPayable.Balance);
            Assert.Equal(PayableStatus.Pending, firstPayable.Status);

            Payable secondPayable = await context.Payables.SingleAsync(p => p.Id == secondPayableId);
            Assert.Equal(50m, secondPayable.Balance);
            Assert.Equal(PayableStatus.PartiallyPaid, secondPayable.Status);

            SupplierPayment unaffectedSecondPayment = await context.SupplierPayments.SingleAsync(
                p => p.Id == secondPayment.Id);
            Assert.Equal(SupplierPaymentStatus.Active, unaffectedSecondPayment.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_AboveTheAuthorizationLimitWithoutManagement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService payments = services.GetRequiredService<ISupplierPaymentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 50m);

            Guid payableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);

            RegisterSupplierPaymentCommand command = new(
                supplierId,
                cashAccountId,
                "PAG-0003",
                DateOnly.FromDateTime(DateTime.UtcNow),
                [new SupplierPaymentAllocationCommand(payableId, 60m)]);

            RegisterSupplierPaymentResult blocked = await payments.RegisterAsync(
                command, actorId, actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Operation.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Payable stillPending = await context.Payables.SingleAsync(p => p.Id == payableId);
            Assert.Equal(100m, stillPending.Balance);

            RegisterSupplierPaymentResult allowed = await payments.RegisterAsync(
                command, actorId, actorIsManagement: true);
            Assert.True(allowed.Operation.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService payments = services.GetRequiredService<ISupplierPaymentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid firstPayableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);
            Guid secondPayableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);

            RegisterSupplierPaymentResult first = await payments.RegisterAsync(
                new RegisterSupplierPaymentCommand(
                    supplierId,
                    cashAccountId,
                    "PAG-REPETIDA",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new SupplierPaymentAllocationCommand(firstPayableId, 50m)]),
                actorId,
                actorIsManagement: false);
            Assert.True(first.Operation.IsSuccess);
            Assert.False(first.DuplicateReferenceWarning);

            RegisterSupplierPaymentResult second = await payments.RegisterAsync(
                new RegisterSupplierPaymentCommand(
                    supplierId,
                    cashAccountId,
                    "PAG-REPETIDA",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new SupplierPaymentAllocationCommand(secondPayableId, 50m)]),
                actorId,
                actorIsManagement: false);

            Assert.True(second.Operation.IsSuccess);
            Assert.True(second.DuplicateReferenceWarning);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task RegisterAsync_WithAPaymentDateOnOrBeforeTheClosingDate_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService payments = services.GetRequiredService<ISupplierPaymentService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid payableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 100m);

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            await SetClosingDateAsync(services, actorId, today);

            RegisterSupplierPaymentResult result = await payments.RegisterAsync(
                new RegisterSupplierPaymentCommand(
                    supplierId, cashAccountId, "PAG-CIERRE-1", today,
                    [new SupplierPaymentAllocationCommand(payableId, 50m)]),
                actorId,
                actorIsManagement: false);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Operation.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Payable unchanged = await context.Payables.SingleAsync(p => p.Id == payableId);
            Assert.Equal(100m, unchanged.Balance);
        });
    }

    private static async Task SetClosingDateAsync(IServiceProvider services, Guid actorId, DateOnly closingDate)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            parameters = new SystemParameters(10_000m, 7, DateTimeOffset.UtcNow, actorId);
            context.SystemParameters.Add(parameters);
        }

        parameters.ChangeClosingDate(closingDate, closingDate, reason: null, DateTimeOffset.UtcNow, actorId);
        await context.SaveChangesAsync();
    }

    private static async Task<Guid> CreateConfirmedCreditPayableAsync(
        IServiceProvider services,
        IExpenseService expenses,
        Guid supplierId,
        Guid actorId,
        decimal amount)
    {
        Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");

        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId,
                categoryId,
                "Compra a crédito de pruebas de pago",
                DateOnly.FromDateTime(DateTime.UtcNow),
                PaymentTerm.Credit30,
                amount,
                taxTypeId,
                PaymentMethod.Cash),
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
        return (await context.Payables.SingleAsync(p => p.ExpenseId == expenseId)).Id;
    }

    private static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de pruebas de pagos",
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
            "Caja de pruebas de pagos",
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
            "Cuenta de gasto de pruebas de pagos",
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
            "Categoría de gasto de pruebas de pagos",
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
