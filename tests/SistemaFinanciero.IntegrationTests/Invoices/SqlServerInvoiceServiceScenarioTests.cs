using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Invoices;

/// <summary>
/// Escenarios reales de <c>IInvoiceService</c> que se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1
/// y siempre revierten datos. A diferencia de <c>SqlServerInvoiceScenarioTests</c> (que guarda el
/// dominio directamente contra <c>FinancialDbContext</c>), estas pruebas pasan por el servicio real que
/// usará <c>InvoicesController</c>.
/// </summary>
public sealed class SqlServerInvoiceServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerInvoiceServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Lifecycle_CreatesEditsConfirmsAndCancelsThroughTheService()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Assert.True(created.Operation.IsSuccess);
            Guid invoiceId = created.InvoiceId!.Value;

            InvoiceModel? draft = await invoices.GetAsync(invoiceId);
            Assert.NotNull(draft);
            Assert.Null(draft!.Number);
            Assert.Equal(InvoiceStatus.Draft, draft.Status);

            CatalogOperationResult addFirst = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(null, "Primera línea", "unidad", 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);
            Assert.True(addFirst.IsSuccess);

            draft = await invoices.GetAsync(invoiceId);
            CatalogOperationResult addSecond = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(null, "Segunda línea", "unidad", 2m, 50m, 0m, taxTypeId),
                draft!.Version,
                actorId);
            Assert.True(addSecond.IsSuccess);

            draft = await invoices.GetAsync(invoiceId);
            Assert.Equal(2, draft!.Lines.Count);
            Assert.Equal(200m, draft.NetAmount);
            Assert.Equal(26m, draft.TaxAmount);
            Assert.Equal(226m, draft.TotalAmount);

            Guid firstLineId = draft.Lines[0].Id;
            CatalogOperationResult replace = await invoices.ReplaceLineAsync(
                invoiceId,
                firstLineId,
                new InvoiceLineInputCommand(null, "Primera línea corregida", "unidad", 1m, 150m, 0m, taxTypeId),
                draft.Version,
                actorId);
            Assert.True(replace.IsSuccess);

            draft = await invoices.GetAsync(invoiceId);
            Guid secondLineId = draft!.Lines.Single(line => line.Description == "Segunda línea").Id;
            CatalogOperationResult remove = await invoices.RemoveLineAsync(
                invoiceId,
                secondLineId,
                draft.Version,
                actorId);
            Assert.True(remove.IsSuccess);

            draft = await invoices.GetAsync(invoiceId);
            Assert.Single(draft!.Lines);
            Assert.Equal(150m, draft.NetAmount);
            Assert.Equal(19.5m, draft.TaxAmount);

            CatalogOperationResult confirm = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001", incomeCategoryId),
                draft.Version,
                actorId);
            Assert.True(confirm.IsSuccess);

            InvoiceModel? confirmed = await invoices.GetAsync(invoiceId);
            Assert.Equal(InvoiceStatus.Confirmed, confirmed!.Status);
            Assert.NotNull(confirmed.Number);
            Assert.True(confirmed.Number > 0);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            OperationLogEntry confirmLogEntry = await context.OperationLogEntries.SingleAsync(
                entry => entry.EntityId == invoiceId && entry.Action == OperationLogAction.Confirmed);
            Assert.Equal(OperationLogEntityType.Invoice, confirmLogEntry.EntityType);
            Assert.Null(confirmLogEntry.Reason);
            Assert.Equal(actorId, confirmLogEntry.PerformedByUserId);

            Income income = await context.Incomes.SingleAsync(candidate => candidate.InvoiceId == invoiceId);
            Assert.Equal(IncomeOrigin.InvoiceCash, income.Origin);
            Assert.Equal(IncomeStatus.Active, income.Status);
            Assert.Equal(confirmed.TotalAmount, income.Amount);
            Assert.Equal(cashAccountId, income.CashAccountId);
            Assert.Equal(incomeCategoryId, income.CategoryId);

            CatalogOperationResult cancel = await invoices.CancelAsync(
                invoiceId,
                "Error de digitación",
                confirmed.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            InvoiceModel? cancelled = await invoices.GetAsync(invoiceId);
            Assert.Equal(InvoiceStatus.Cancelled, cancelled!.Status);
            Assert.Equal("Error de digitación", cancelled.CancellationReason);
            Assert.Equal(confirmed.Number, cancelled.Number);

            Income cancelledIncome = await context.Incomes.SingleAsync(candidate => candidate.InvoiceId == invoiceId);
            Assert.Equal(IncomeStatus.Cancelled, cancelledIncome.Status);
            Assert.Equal("Error de digitación", cancelledIncome.CancellationReason);

            OperationLogEntry cancelLogEntry = await context.OperationLogEntries.SingleAsync(
                entry => entry.EntityId == invoiceId && entry.Action == OperationLogAction.Cancelled);
            Assert.Equal("Error de digitación", cancelLogEntry.Reason);
            Assert.Equal(actorId, cancelLogEntry.PerformedByUserId);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Confirm_AssignsDistinctConsecutiveNumbersAcrossInvoices()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            ConfirmInvoiceCommand confirmCommand = new(cashAccountId, PaymentMethod.Cash, "F-0001", incomeCategoryId);

            Guid firstId = await CreateInvoiceWithLineAsync(invoices, customerId, taxTypeId, actorId);
            Guid secondId = await CreateInvoiceWithLineAsync(invoices, customerId, taxTypeId, actorId);

            InvoiceModel first = (await invoices.GetAsync(firstId))!;
            InvoiceModel second = (await invoices.GetAsync(secondId))!;

            Assert.True((await invoices.ConfirmAsync(firstId, confirmCommand, first.Version, actorId)).IsSuccess);
            Assert.True((await invoices.ConfirmAsync(secondId, confirmCommand, second.Version, actorId)).IsSuccess);

            long? firstNumber = (await invoices.GetAsync(firstId))!.Number;
            long? secondNumber = (await invoices.GetAsync(secondId))!.Number;

            Assert.NotEqual(firstNumber, secondNumber);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Confirm_OnACreditInvoice_CreatesTheReceivableAndCancelCascades()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(null, "Servicio a crédito", "unidad", 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;

            // La confirmación a crédito no exige cuenta de efectivo, medio de pago ni categoría: solo
            // genera la cuenta por cobrar (regla 2).
            CatalogOperationResult confirm = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(null, null, null, null),
                draft.Version,
                actorId);
            Assert.True(confirm.IsSuccess);

            InvoiceModel confirmed = (await invoices.GetAsync(invoiceId))!;

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(
                candidate => candidate.InvoiceId == invoiceId);
            Assert.Equal(ReceivableStatus.Pending, receivable.Status);
            Assert.Equal(confirmed.TotalAmount, receivable.OriginalAmount);
            Assert.Equal(confirmed.TotalAmount, receivable.Balance);
            Assert.Equal(confirmed.DueDate, receivable.DueDate);
            Assert.False(context.Incomes.Any(income => income.InvoiceId == invoiceId));

            CatalogOperationResult cancel = await invoices.CancelAsync(
                invoiceId,
                "Error de digitación",
                confirmed.Version,
                actorId);
            Assert.True(cancel.IsSuccess);

            Receivable cancelledReceivable = await context.Receivables.SingleAsync(
                candidate => candidate.InvoiceId == invoiceId);
            Assert.Equal(ReceivableStatus.Cancelled, cancelledReceivable.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Cancel_OnACreditInvoiceWithAnActivePayment_IsBlockedAndKeepsEverythingUnchanged()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(null, "Servicio a crédito", "unidad", 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(null, null, null, null),
                draft.Version,
                actorId);

            InvoiceModel confirmed = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(100m, confirmed.TotalAmount);

            // Simula un abono vigente directamente sobre el dominio: el servicio de abonos (siguiente
            // bloque del roadmap) todavía no existe.
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Receivable receivable = await context.Receivables.SingleAsync(
                candidate => candidate.InvoiceId == invoiceId);
            receivable.ApplyPayment(50m, DateTimeOffset.UtcNow, actorId);
            await context.SaveChangesAsync();

            CatalogOperationResult cancel = await invoices.CancelAsync(
                invoiceId,
                "Error de digitación",
                confirmed.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, cancel.Status);

            InvoiceModel stillConfirmed = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(InvoiceStatus.Confirmed, stillConfirmed.Status);

            Receivable unchangedReceivable = await context.Receivables.SingleAsync(
                candidate => candidate.InvoiceId == invoiceId);
            Assert.Equal(ReceivableStatus.PartiallyPaid, unchangedReceivable.Status);
            Assert.Equal(50m, unchangedReceivable.Balance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task OwnershipRestriction_HidesAndBlocksAnotherUsersDraft()
    {
        return RunInRollbackTransactionAsync(async (services, ownerId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, ownerId);
            Guid otherUserId = await CreateUserAsync(services);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                ownerId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel owned = (await invoices.GetAsync(invoiceId))!;

            // Un usuario distinto al creador no la ve cuando se restringe a su propio identificador.
            Assert.Null(await invoices.GetAsync(invoiceId, restrictToOwnerUserId: otherUserId));

            CatalogOperationResult blockedUpdate = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, owned.IssueDate, PaymentTerm.Cash),
                owned.Version,
                otherUserId,
                restrictToOwnerUserId: otherUserId);
            Assert.Equal(CatalogOperationStatus.NotFound, blockedUpdate.Status);

            // El propio creador sí la ve y la edita cuando se restringe a su identificador.
            Assert.NotNull(await invoices.GetAsync(invoiceId, restrictToOwnerUserId: ownerId));

            CatalogOperationResult allowedUpdate = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, owned.IssueDate, PaymentTerm.Credit30),
                owned.Version,
                ownerId,
                restrictToOwnerUserId: ownerId);
            Assert.True(allowedUpdate.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task UpdateHeader_OnAConcurrencyConflict_DoesNotOverwrite()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult firstEdit = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, draft.IssueDate, PaymentTerm.Credit30),
                draft.Version,
                actorId);
            Assert.True(firstEdit.IsSuccess);

            // Reutiliza la versión ya obsoleta: simula dos usuarios editando a la vez.
            CatalogOperationResult staleEdit = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, draft.IssueDate, PaymentTerm.Credit60),
                draft.Version,
                actorId);
            Assert.Equal(CatalogOperationStatus.ConcurrencyConflict, staleEdit.Status);

            InvoiceModel current = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(PaymentTerm.Credit30, current.PaymentTerm);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Search_FiltersByStatusAndByOwner()
    {
        return RunInRollbackTransactionAsync(async (services, ownerId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, ownerId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, ownerId, "13");
            Guid otherUserId = await CreateUserAsync(services);
            Guid cashAccountId = await CreateCashAccountAsync(services, ownerId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, ownerId);

            Guid ownDraftId = await CreateInvoiceWithLineAsync(invoices, customerId, taxTypeId, ownerId);
            Guid otherDraftId = (await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                otherUserId)).InvoiceId!.Value;

            InvoiceModel ownDraft = (await invoices.GetAsync(ownDraftId))!;
            await invoices.ConfirmAsync(
                ownDraftId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001", incomeCategoryId),
                ownDraft.Version,
                ownerId);

            PagedResult<InvoiceSummaryModel> onlyOwner = await invoices.SearchAsync(
                new InvoiceQuery(status: InvoiceStatusFilter.All, createdByUserId: ownerId));
            Assert.Contains(onlyOwner.Items, item => item.Id == ownDraftId);
            Assert.DoesNotContain(onlyOwner.Items, item => item.Id == otherDraftId);

            PagedResult<InvoiceSummaryModel> onlyConfirmed = await invoices.SearchAsync(
                new InvoiceQuery(status: InvoiceStatusFilter.Confirmed, createdByUserId: ownerId));
            Assert.Contains(onlyConfirmed.Items, item => item.Id == ownDraftId);

            PagedResult<InvoiceSummaryModel> onlyDrafts = await invoices.SearchAsync(
                new InvoiceQuery(status: InvoiceStatusFilter.Draft, createdByUserId: ownerId));
            Assert.DoesNotContain(onlyDrafts.Items, item => item.Id == ownDraftId);
        });
    }

    private static async Task<Guid> CreateInvoiceWithLineAsync(
        IInvoiceService invoices,
        Guid customerId,
        Guid taxTypeId,
        Guid actorId)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;
        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(null, "Línea", "unidad", 1m, 100m, 0m, taxTypeId),
            draft.Version,
            actorId);

        return invoiceId;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de servicio",
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
            "Caja de pruebas",
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
            "Cuenta de ingreso de pruebas",
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
            "Categoría de ingreso de pruebas",
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
