using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Receivables;

/// <summary>
/// Escenarios reales de <c>IReceivablePaymentService</c> que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Sigue el mismo patrón que
/// <c>SqlServerInvoiceServiceScenarioTests</c>.
/// </summary>
public sealed class SqlServerReceivablePaymentServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerReceivablePaymentServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_ToASingleInvoice_AppliesThePaymentAndCreatesItsIncome()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 100m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    incomeCategoryId,
                    "AB-0001",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(receivableId, 60m, null)]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.Single(result.ReceivablePaymentIds);
            Assert.False(result.DuplicateReferenceWarning);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(40m, receivable.Balance);
            Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);

            ReceivablePayment payment = await context.ReceivablePayments.SingleAsync(
                p => p.Id == result.ReceivablePaymentIds[0]);
            Assert.Equal(60m, payment.Amount);
            Assert.Equal(ReceivablePaymentStatus.Active, payment.Status);

            Income income = await context.Incomes.SingleAsync(i => i.ReceivablePaymentId == payment.Id);
            Assert.Equal(IncomeOrigin.ReceivablePayment, income.Origin);
            Assert.Equal(60m, income.Amount);
            Assert.Null(income.PaymentMethod);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAWithholding_ReducesTheBalanceByTheAmountPlusTheWithholding()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 1000m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    incomeCategoryId,
                    "AB-0002",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(
                            receivableId,
                            980m,
                            new WithholdingInputCommand(withholdingTypeId, 1000m)),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(0m, receivable.Balance);
            Assert.Equal(ReceivableStatus.Paid, receivable.Status);

            Withholding withholding = await context.Withholdings.SingleAsync(
                w => w.ReceivablePaymentId == result.ReceivablePaymentIds[0]);
            Assert.Equal(20m, withholding.Amount);

            Income income = await context.Incomes.SingleAsync(
                i => i.ReceivablePaymentId == result.ReceivablePaymentIds[0]);
            Assert.Equal(1000m, income.Amount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_ToSeveralInvoicesOfTheSameCustomer_SharesTheGroupIdAndCancelsIndividually()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid firstReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 100m);
            Guid secondReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 100m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    incomeCategoryId,
                    "AB-0003",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(firstReceivableId, 100m, null),
                        new ReceivablePaymentAllocationCommand(secondReceivableId, 50m, null),
                    ]),
                actorId,
                actorIsManagement: false);

            Assert.True(result.Operation.IsSuccess);
            Assert.Equal(2, result.ReceivablePaymentIds.Count);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            ReceivablePayment firstPayment = await context.ReceivablePayments.SingleAsync(
                p => p.ReceivableId == firstReceivableId);
            ReceivablePayment secondPayment = await context.ReceivablePayments.SingleAsync(
                p => p.ReceivableId == secondReceivableId);
            Assert.Equal(firstPayment.GroupId, secondPayment.GroupId);

            // Anular uno no debe afectar al otro (regla 2: "cada uno se anula de forma individual").
            ReceivablePaymentModel? firstModel = await payments.GetAsync(firstPayment.Id);
            CatalogOperationResult cancel = await payments.CancelAsync(
                firstPayment.Id,
                "Abono registrado por error",
                firstModel!.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            Receivable firstReceivable = await context.Receivables.SingleAsync(r => r.Id == firstReceivableId);
            Assert.Equal(100m, firstReceivable.Balance);
            Assert.Equal(ReceivableStatus.Pending, firstReceivable.Status);

            Income firstIncome = await context.Incomes.SingleAsync(i => i.ReceivablePaymentId == firstPayment.Id);
            Assert.Equal(IncomeStatus.Cancelled, firstIncome.Status);

            Receivable secondReceivable = await context.Receivables.SingleAsync(r => r.Id == secondReceivableId);
            Assert.Equal(50m, secondReceivable.Balance);
            Assert.Equal(ReceivableStatus.PartiallyPaid, secondReceivable.Status);

            ReceivablePayment unaffectedSecondPayment = await context.ReceivablePayments.SingleAsync(
                p => p.Id == secondPayment.Id);
            Assert.Equal(ReceivablePaymentStatus.Active, unaffectedSecondPayment.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_AboveTheAuthorizationLimitWithoutManagement_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 50m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 100m);

            RegisterReceivablePaymentCommand command = new(
                customerId,
                cashAccountId,
                incomeCategoryId,
                "AB-0004",
                DateOnly.FromDateTime(DateTime.UtcNow),
                [new ReceivablePaymentAllocationCommand(receivableId, 60m, null)]);

            RegisterReceivablePaymentResult blocked = await payments.RegisterAsync(
                command,
                actorId,
                actorIsManagement: false);
            Assert.Equal(CatalogOperationStatus.Invalid, blocked.Operation.Status);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable stillPending = await context.Receivables.SingleAsync(r => r.Id == receivableId);
            Assert.Equal(100m, stillPending.Balance);

            RegisterReceivablePaymentResult allowed = await payments.RegisterAsync(
                command,
                actorId,
                actorIsManagement: true);
            Assert.True(allowed.Operation.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Register_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid firstReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 100m);
            Guid secondReceivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, actorId, amount: 100m);

            RegisterReceivablePaymentResult first = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    incomeCategoryId,
                    "AB-REPETIDA",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(firstReceivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);
            Assert.True(first.Operation.IsSuccess);
            Assert.False(first.DuplicateReferenceWarning);

            RegisterReceivablePaymentResult second = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    incomeCategoryId,
                    "AB-REPETIDA",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(secondReceivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);

            Assert.True(second.Operation.IsSuccess);
            Assert.True(second.DuplicateReferenceWarning);
        });
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid actorId,
        decimal amount)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");

        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(null, "Servicio a crédito", "unidad", 1m, amount, 0m, taxTypeId),
            draft.Version,
            actorId);

        draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(
            invoiceId,
            new ConfirmInvoiceCommand(null, null, null, null),
            draft.Version,
            actorId);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Receivables.SingleAsync(r => r.InvoiceId == invoiceId)).Id;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de abonos",
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
            "Caja de pruebas de abonos",
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
            "Cuenta de ingreso de pruebas de abonos",
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
            "Categoría de ingreso de pruebas de abonos",
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

    private static async Task<Guid> CreateWithholdingTypeAsync(IServiceProvider services, Guid actorId, decimal rate)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        WithholdingType type = new(
            Guid.NewGuid(),
            $"RET-{suffix}",
            $"Renta {rate}%",
            rate,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.WithholdingTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
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
