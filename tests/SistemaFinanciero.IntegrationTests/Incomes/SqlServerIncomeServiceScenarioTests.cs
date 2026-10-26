using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Incomes;

/// <summary>
/// Escenarios reales de <c>IIncomeService</c> que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1
/// y siempre revierten datos. Sigue el mismo patrón que
/// <c>SqlServerReceivablePaymentServiceScenarioTests</c>.
/// </summary>
public sealed class SqlServerIncomeServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerIncomeServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithValidData_CreatesTheIncomeAndCancelAsyncCancelsIt()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            IncomeCreationResult created = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeCategoryId,
                    cashAccountId,
                    15_000m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentMethod.BankTransfer,
                    "DEP-0099",
                    "Reintegro de un proveedor por un pago duplicado"),
                actorId);

            Assert.True(created.Operation.IsSuccess);
            Assert.NotNull(created.IncomeId);

            IncomeModel? income = await incomes.GetAsync(created.IncomeId!.Value);
            Assert.NotNull(income);
            Assert.Equal(IncomeOrigin.Manual, income!.Origin);
            Assert.Equal(IncomeStatus.Active, income.Status);
            Assert.Equal(15_000m, income.Amount);
            Assert.Equal("Reintegro de un proveedor por un pago duplicado", income.Description);

            CatalogOperationResult cancel = await incomes.CancelAsync(
                income.Id,
                "Registrado por error",
                income.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            IncomeModel? cancelled = await incomes.GetAsync(income.Id);
            Assert.Equal(IncomeStatus.Cancelled, cancelled!.Status);
            Assert.Equal("Registrado por error", cancelled.CancellationReason);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithAnInactiveCashAccount_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            IncomeCreationResult created = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeCategoryId,
                    Guid.NewGuid(),
                    15_000m,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    PaymentMethod.BankTransfer,
                    "DEP-0099",
                    "Reintegro"),
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, created.Operation.Status);
            Assert.Null(created.IncomeId);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_OnAnAutomaticIncome_IsRejectedAndKeepsItActive()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = createdInvoice.InvoiceId!.Value;

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(null, "Servicio de contado", "unidad", 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001", incomeCategoryId),
                draft.Version,
                actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Income automaticIncome = await context.Incomes.SingleAsync(income => income.InvoiceId == invoiceId);
            IncomeModel model = (await incomes.GetAsync(automaticIncome.Id))!;

            CatalogOperationResult cancel = await incomes.CancelAsync(
                model.Id,
                "Intento de anular directo",
                model.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, cancel.Status);

            IncomeModel? stillActive = await incomes.GetAsync(model.Id);
            Assert.Equal(IncomeStatus.Active, stillActive!.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateManualAsync_WithAnIncomeDateOnOrBeforeTheClosingDate_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            await SetClosingDateAsync(services, actorId, today);

            IncomeCreationResult closed = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeCategoryId, cashAccountId, 15_000m, today,
                    PaymentMethod.BankTransfer, "DEP-CIERRE", "Reintegro"),
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, closed.Operation.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CancelAsync_WhenTheIncomeDateIsNowClosed_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            DateOnly incomeDate = new(2026, 1, 15);

            IncomeCreationResult created = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeCategoryId, cashAccountId, 15_000m, incomeDate,
                    PaymentMethod.BankTransfer, "DEP-0099", "Reintegro"),
                actorId);
            IncomeModel income = (await incomes.GetAsync(created.IncomeId!.Value))!;

            await SetClosingDateAsync(services, actorId, new DateOnly(2026, 1, 31));

            CatalogOperationResult cancel = await incomes.CancelAsync(
                income.Id, "Registrado por error", income.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, cancel.Status);
            IncomeModel stillActive = (await incomes.GetAsync(income.Id))!;
            Assert.Equal(IncomeStatus.Active, stillActive.Status);
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

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de ingresos",
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
            "Caja de pruebas de ingresos",
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

    private static async Task<Guid> CreateIncomeCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"ING-{suffix}",
            "Cuenta de ingreso de pruebas de ingresos",
            LedgerAccountType.Income,
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
            "Categoría de ingreso de pruebas de ingresos",
            FinancialCategoryKind.Income,
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
