using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
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
    public Task Lifecycle_CreatesEditsAndConfirmsThroughTheService()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Assert.True(created.Operation.IsSuccess);
            Guid invoiceId = created.InvoiceId!.Value;

            InvoiceModel? draft = await invoices.GetAsync(invoiceId);
            Assert.NotNull(draft);
            Assert.Null(draft!.Number);
            Assert.Equal(InvoiceStatus.Draft, draft.Status);

            Guid firstItemId = await CreateCatalogItemAsync(services, actorId, "Primera línea");
            Guid secondItemId = await CreateCatalogItemAsync(services, actorId, "Segunda línea");
            Guid correctedItemId = await CreateCatalogItemAsync(services, actorId, "Primera línea corregida");

            CatalogOperationResult addFirst = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(firstItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);
            Assert.True(addFirst.IsSuccess);

            draft = await invoices.GetAsync(invoiceId);
            CatalogOperationResult addSecond = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(secondItemId, 2m, 50m, 0m, taxTypeId),
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
                new InvoiceLineInputCommand(correctedItemId, 1m, 150m, 0m, taxTypeId),
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
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"),
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
            Assert.Equal(incomeAccountId, income.AccountId);

        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreditNoteRegistration_CancelsCashInvoiceAndPostsRefundInTheSameFlow()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            ICreditNoteService creditNotes = services.GetRequiredService<ICreditNoteService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            CatalogOperationResult confirmation = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "VENTA-001"),
                draft.Version,
                actorId);
            Assert.True(confirmation.IsSuccess);

            CreditNoteInvoiceOption eligible = (await creditNotes.GetInvoiceAsync(invoiceId))!;
            Assert.True(eligible.IsCashInvoice);
            Assert.Equal(113m, eligible.InvoiceTotal);
            Assert.Equal(113m, eligible.CashReceived);

            CreditNoteRegistrationResult registration = await creditNotes.RegisterAsync(
                new RegisterCreditNoteCommand(
                    invoiceId,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    "Devolución total de la venta",
                    cashAccountId,
                    "DEV-001",
                    eligible.InvoiceVersion),
                actorId,
                actorIsManagement: true);

            Assert.True(registration.Operation.IsSuccess);
            Assert.NotNull(registration.CreditNoteId);

            InvoiceModel cancelledInvoice = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(InvoiceStatus.Cancelled, cancelledInvoice.Status);
            Assert.Equal("Devolución total de la venta", cancelledInvoice.CancellationReason);

            CreditNoteModel note = (await creditNotes.GetAsync(registration.CreditNoteId!.Value))!;
            Assert.Equal(113m, note.TotalAmount);
            Assert.Equal(113m, note.RefundedAmount);
            Assert.Equal(0m, note.AppliedToBalance);
            Assert.Equal("Caja de pruebas", note.CashAccountName);
            Assert.Equal("DEV-001", note.RefundReference);

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(
                JournalEntrySourceType.CreditNote,
                note.Id))!;
            Assert.Null(trace.Reversal);
            Assert.Equal(3, trace.Entry.Lines.Count);
            Assert.Equal(
                trace.Entry.Lines.Sum(line => line.Debit),
                trace.Entry.Lines.Sum(line => line.Credit));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ConfirmAsync_WhenRequiredSystemAccountIsInactive_IsRejectedWithAnActionableMessage()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "UPDATE catalogos.CuentasContables SET IsActive = 0 WHERE SystemRole = 5");

            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            CatalogOperationResult result = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "TEST-CUENTA-SISTEMA"),
                draft.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, result.Status);
            Assert.Contains("inactiva", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("reactívela", result.Message, StringComparison.OrdinalIgnoreCase);
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
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            ConfirmInvoiceCommand confirmCommand = new(cashAccountId, PaymentMethod.Cash, "F-0001");

            Guid firstId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            Guid secondId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);

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
    public Task Confirm_OnACreditInvoice_CreatesReceivableAndCreditNoteClosesIt()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid creditItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(creditItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;

            // La confirmación a crédito no exige cuenta de efectivo ni medio de pago: solo genera la
            // cuenta por cobrar (regla 2).
            CatalogOperationResult confirm = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(null, null, null),
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

            ICreditNoteService creditNotes = services.GetRequiredService<ICreditNoteService>();
            CreditNoteInvoiceOption eligible = (await creditNotes.GetInvoiceAsync(invoiceId))!;
            CreditNoteRegistrationResult registration = await creditNotes.RegisterAsync(
                new RegisterCreditNoteCommand(
                    invoiceId,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    "Error de digitación",
                    null,
                    null,
                    eligible.InvoiceVersion),
                actorId,
                actorIsManagement: true);
            Assert.True(registration.Operation.IsSuccess);

            Receivable cancelledReceivable = await context.Receivables.SingleAsync(
                candidate => candidate.InvoiceId == invoiceId);
            Assert.Equal(ReceivableStatus.Cancelled, cancelledReceivable.Status);
            Assert.Equal(0m, cancelledReceivable.Balance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreditNoteRegistration_WithAnUnreconciledReceivable_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;

            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid creditItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(creditItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(null, null, null),
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

            ICreditNoteService creditNotes = services.GetRequiredService<ICreditNoteService>();
            CreditNoteInvoiceOption eligible = (await creditNotes.GetInvoiceAsync(invoiceId))!;
            CreditNoteRegistrationResult registration = await creditNotes.RegisterAsync(
                new RegisterCreditNoteCommand(
                    invoiceId,
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    "Devolución",
                    null,
                    null,
                    eligible.InvoiceVersion),
                actorId,
                actorIsManagement: true);
            Assert.Equal(CatalogOperationStatus.Invalid, registration.Operation.Status);

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
    public Task AssistantCanReadAnotherUsersDraft_ButCannotEditIt()
    {
        return RunInRollbackTransactionAsync(async (services, ownerId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, ownerId);
            Guid otherUserId = await CreateUserAsync(services);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, ownerId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                ownerId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel owned = (await invoices.GetAsync(invoiceId))!;

            // La lectura sin restricción permite consultar el borrador ajeno.
            Assert.NotNull(await invoices.GetAsync(invoiceId));

            // Un usuario distinto al creador no lo ve cuando se aplica el límite usado al editar.
            Assert.Null(await invoices.GetAsync(invoiceId, restrictToOwnerUserId: otherUserId));

            CatalogOperationResult blockedUpdate = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, owned.IssueDate, PaymentTerm.Cash),
                owned.Version,
                otherUserId,
                restrictToOwnerUserId: otherUserId);
            Assert.Equal(CatalogOperationStatus.NotFound, blockedUpdate.Status);

            // El propio creador sí la ve y la edita cuando se restringe a su identificador.
            Assert.NotNull(await invoices.GetAsync(invoiceId, restrictToOwnerUserId: ownerId));

            CatalogOperationResult allowedUpdate = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, owned.IssueDate, PaymentTerm.Credit30),
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
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult firstEdit = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, draft.IssueDate, PaymentTerm.Credit30),
                draft.Version,
                actorId);
            Assert.True(firstEdit.IsSuccess);

            // Reutiliza la versión ya obsoleta: simula dos usuarios editando a la vez.
            CatalogOperationResult staleEdit = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, draft.IssueDate, PaymentTerm.Credit60),
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
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, ownerId);

            Guid ownDraftId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, ownerId);
            Guid otherDraftId = (await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                otherUserId)).InvoiceId!.Value;

            InvoiceModel ownDraft = (await invoices.GetAsync(ownDraftId))!;
            await invoices.ConfirmAsync(
                ownDraftId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"),
                ownDraft.Version,
                ownerId);

            PagedResult<InvoiceSummaryModel> allInvoices = await invoices.SearchAsync(
                new InvoiceQuery(status: InvoiceStatusFilter.All));
            Assert.Contains(allInvoices.Items, item => item.Id == ownDraftId);
            Assert.Contains(allInvoices.Items, item => item.Id == otherDraftId);

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

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SearchAsync_FiltersByPaymentTerm()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            Guid cashInvoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            InvoiceModel cashDraft = (await invoices.GetAsync(cashInvoiceId))!;
            await invoices.ConfirmAsync(
                cashInvoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "FILTER-001"),
                cashDraft.Version,
                actorId);

            Guid creditInvoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            InvoiceModel creditDraft = (await invoices.GetAsync(creditInvoiceId))!;
            await invoices.UpdateHeaderAsync(
                creditInvoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, creditDraft.IssueDate, PaymentTerm.Credit30),
                creditDraft.Version,
                actorId);

            PagedResult<InvoiceSummaryModel> onlyCash = await invoices.SearchAsync(
                new InvoiceQuery(paymentTerm: PaymentTerm.Cash, createdByUserId: actorId));
            Assert.Contains(onlyCash.Items, item => item.Id == cashInvoiceId);
            Assert.DoesNotContain(onlyCash.Items, item => item.Id == creditInvoiceId);

            PagedResult<InvoiceSummaryModel> onlyCredit30 = await invoices.SearchAsync(
                new InvoiceQuery(paymentTerm: PaymentTerm.Credit30, createdByUserId: actorId));
            Assert.Contains(onlyCredit30.Items, item => item.Id == creditInvoiceId);
            Assert.DoesNotContain(onlyCredit30.Items, item => item.Id == cashInvoiceId);

            InvoiceModel cashInvoice = (await invoices.GetAsync(cashInvoiceId))!;
            PagedResult<InvoiceSummaryModel> matchingColumns = await invoices.SearchAsync(
                new InvoiceQuery(
                    status: InvoiceStatusFilter.All,
                    createdByUserId: actorId,
                    paymentTerm: cashInvoice.PaymentTerm,
                    number: cashInvoice.Number,
                    customerName: cashInvoice.CustomerName,
                    issueDate: cashInvoice.IssueDate,
                    dueDate: cashInvoice.DueDate,
                    totalAmount: cashInvoice.TotalAmount,
                    createdByUserName: cashInvoice.CreatedByUserName));
            Assert.Collection(matchingColumns.Items, item => Assert.Equal(cashInvoiceId, item.Id));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreateAsync_WithAnIssueDateOnOrBeforeTheClosingDate_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
            await SetClosingDateAsync(services, actorId, today);

            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult closed = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, today, PaymentTerm.Cash), actorId);
            Assert.Equal(CatalogOperationStatus.Invalid, closed.Operation.Status);

            InvoiceCreationResult open = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, today.AddDays(1), PaymentTerm.Cash), actorId);
            Assert.True(open.Operation.IsSuccess);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ConfirmAsync_WhenTheInvoiceIssueDateIsNowClosed_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            DateOnly issueDate = new(2026, 1, 15);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, issueDate, PaymentTerm.Credit30), actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid creditItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(creditItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);
            draft = (await invoices.GetAsync(invoiceId))!;

            await SetClosingDateAsync(services, actorId, new DateOnly(2026, 1, 31));

            CatalogOperationResult confirm = await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(null, null, null), draft.Version, actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, confirm.Status);
            InvoiceModel stillDraft = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(InvoiceStatus.Draft, stillDraft.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CreditNoteRegistration_WhenTheIssueDateIsNowClosed_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            DateOnly issueDate = new(2026, 1, 15);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, issueDate, PaymentTerm.Credit30), actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid creditItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(creditItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);
            draft = (await invoices.GetAsync(invoiceId))!;

            // La factura se confirma antes de cerrar su período (regla 16 solo bloquea desde el cierre).
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(null, null, null), draft.Version, actorId);
            InvoiceModel confirmed = (await invoices.GetAsync(invoiceId))!;

            await SetClosingDateAsync(services, actorId, new DateOnly(2026, 1, 31));

            ICreditNoteService creditNotes = services.GetRequiredService<ICreditNoteService>();
            CreditNoteInvoiceOption eligible = (await creditNotes.GetInvoiceAsync(invoiceId))!;
            CreditNoteRegistrationResult registration = await creditNotes.RegisterAsync(
                new RegisterCreditNoteCommand(
                    invoiceId,
                    issueDate,
                    "Corrección",
                    null,
                    null,
                    eligible.InvoiceVersion),
                actorId,
                actorIsManagement: true);

            Assert.Equal(CatalogOperationStatus.Invalid, registration.Operation.Status);
            InvoiceModel stillConfirmed = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(InvoiceStatus.Confirmed, stillConfirmed.Status);
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

        context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
            closingDate.Year, closingDate.Month, "Período cerrado para la prueba", DateTimeOffset.UtcNow, actorId));
        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetAsync_ForAConfirmedCashInvoice_IncludesThePaymentMethodFromItsIncome()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            Assert.Null(draft.PaymentMethod);

            await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.SinpeMobile, "SINPE-001"),
                draft.Version,
                actorId);

            InvoiceModel confirmed = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(PaymentMethod.SinpeMobile, confirmed.PaymentMethod);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetAsync_ForACreditInvoice_HasNoPaymentMethod()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            Guid creditItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
            await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(creditItemId, 1m, 100m, 0m, taxTypeId),
                draft.Version,
                actorId);

            draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(null, null, null), draft.Version, actorId);

            InvoiceModel confirmed = (await invoices.GetAsync(invoiceId))!;
            Assert.Null(confirmed.PaymentMethod);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task HasDuplicateCashReferenceAsync_DetectsAReferenceAlreadyUsedByAnIncome()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            Assert.False(await invoices.HasDuplicateCashReferenceAsync(cashAccountId, "F-REPETIDA"));

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-REPETIDA"),
                draft.Version,
                actorId);

            Assert.True(await invoices.HasDuplicateCashReferenceAsync(cashAccountId, "F-REPETIDA"));
            Assert.False(await invoices.HasDuplicateCashReferenceAsync(cashAccountId, "OTRA-REFERENCIA"));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CheckCustomerCreditLimitAsync_WithoutALimitConfigured_NeverExceeds()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);

            CustomerCreditLimitCheck check = await invoices.CheckCustomerCreditLimitAsync(customerId, 1_000_000m);

            Assert.False(check.HasLimit);
            Assert.Null(check.CreditLimit);
            Assert.False(check.ExceedsLimit);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CheckCustomerCreditLimitAsync_SumsOutstandingReceivablesAndNeverBlocksConfirmation()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, creditLimit: 150m);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            // Primera factura a crédito de ₡100: todavía dentro del límite de ₡150.
            Guid firstInvoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            InvoiceModel firstDraft = (await invoices.GetAsync(firstInvoiceId))!;
            await invoices.UpdateHeaderAsync(
                firstInvoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, firstDraft.IssueDate, PaymentTerm.Credit30),
                firstDraft.Version,
                actorId);
            firstDraft = (await invoices.GetAsync(firstInvoiceId))!;

            CustomerCreditLimitCheck beforeFirstConfirm = await invoices.CheckCustomerCreditLimitAsync(
                customerId, firstDraft.TotalAmount);
            Assert.True(beforeFirstConfirm.HasLimit);
            Assert.Equal(150m, beforeFirstConfirm.CreditLimit);
            Assert.Equal(0m, beforeFirstConfirm.OutstandingBalanceBeforeThisInvoice);
            Assert.False(beforeFirstConfirm.ExceedsLimit);

            await invoices.ConfirmAsync(
                firstInvoiceId, new ConfirmInvoiceCommand(null, null, null), firstDraft.Version, actorId);

            // Segunda factura a crédito de ₡100: el saldo pendiente (₡100) más esta (₡100) supera el
            // límite de ₡150, pero el sistema solo lo advierte: nunca bloquea la confirmación.
            Guid secondInvoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId);
            InvoiceModel secondDraft = (await invoices.GetAsync(secondInvoiceId))!;
            await invoices.UpdateHeaderAsync(
                secondInvoiceId,
                new UpdateInvoiceHeaderCommand(customerId, incomeAccountId, secondDraft.IssueDate, PaymentTerm.Credit30),
                secondDraft.Version,
                actorId);
            secondDraft = (await invoices.GetAsync(secondInvoiceId))!;

            CustomerCreditLimitCheck beforeSecondConfirm = await invoices.CheckCustomerCreditLimitAsync(
                customerId, secondDraft.TotalAmount);
            Assert.True(beforeSecondConfirm.HasLimit);
            Assert.Equal(100m, beforeSecondConfirm.OutstandingBalanceBeforeThisInvoice);
            Assert.True(beforeSecondConfirm.ExceedsLimit);

            CatalogOperationResult secondConfirm = await invoices.ConfirmAsync(
                secondInvoiceId, new ConfirmInvoiceCommand(null, null, null), secondDraft.Version, actorId);
            Assert.True(secondConfirm.IsSuccess);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            decimal totalOutstanding = await context.Receivables
                .Where(receivable => receivable.CustomerId == customerId
                    && (receivable.Status == ReceivableStatus.Pending
                        || receivable.Status == ReceivableStatus.PartiallyPaid))
                .SumAsync(receivable => receivable.Balance);
            Assert.Equal(200m, totalOutstanding);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_WithACatalogItemWithItsOwnPriceAndTax_IgnoresManualPriceAndTaxAndCopiesTheItemsData()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid itemTaxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid catalogItemId = await CreateCatalogItemAsync(
                services, actorId, "Soporte técnico mensual", referencePriceCrc: 75_000m, defaultTaxTypeId: itemTaxTypeId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            // No se indican precio ni tarifa: el artículo ya los tiene (ADR-0043).
            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 2m, null, 0m, null),
                draft.Version,
                actorId);
            Assert.True(add.IsSuccess);

            InvoiceModel updated = (await invoices.GetAsync(invoiceId))!;
            InvoiceLineModel line = Assert.Single(updated.Lines);
            Assert.Equal("Soporte técnico mensual", line.Description);
            Assert.Equal("Unidad", line.UnitOfMeasure);
            Assert.Equal(75_000m, line.UnitPrice);
            Assert.Equal(13m, line.TaxRate);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_WithACatalogItemWithoutAReferencePrice_UsesTheManualUnitPrice()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a medida");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, 45_000m, 0m, taxTypeId),
                draft.Version,
                actorId);
            Assert.True(add.IsSuccess);

            InvoiceModel updated = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(45_000m, updated.Lines.Single().UnitPrice);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_WithACatalogItemWithoutAReferencePriceAndNoManualPrice_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio sin precio");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, null, 0m, taxTypeId),
                draft.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, add.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_WithACatalogItemWithoutADefaultTaxTypeAndNoManualTaxType_FallsBackToGeneral13ForANormalCustomer()
    {
        // Bloque 1 del paquete v7, punto 3: un cliente de condición Normal sin tarifa del artículo ni
        // del formulario usa IVA 13 % en vez de rechazar la línea.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid catalogItemId = await CreateCatalogItemAsync(
                services, actorId, "Servicio sin tarifa", referencePriceCrc: 10_000m);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, null, 0m, null),
                draft.Version,
                actorId);

            Assert.True(add.IsSuccess);
            InvoiceModel updated = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(13m, updated.Lines.Single().TaxRate);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_WithATaxTypeReservedForAnotherVatCondition_IsRejectedForANormalCustomer()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid catalogItemId = await CreateCatalogItemAsync(
                services, actorId, "Servicio sin tarifa", referencePriceCrc: 10_000m);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid reduced2Id = (await context.Set<TaxType>()
                .SingleAsync(taxType => taxType.SystemRole == TaxTypeSystemRole.Reduced2)).Id;

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, null, 0m, reduced2Id),
                draft.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, add.Status);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_ForAStateHigherEducationInstitutionCustomer_AlwaysForcesReduced2()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(
                services, actorId, vatCondition: VatCondition.StateHigherEducationInstitution);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid catalogItemId = await CreateCatalogItemAsync(
                services, actorId, "Soporte técnico", referencePriceCrc: 10_000m, defaultTaxTypeId: taxTypeId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            // El artículo trae su propia tarifa de 13 %, pero la condición del cliente la ignora.
            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, null, 0m, null),
                draft.Version,
                actorId);

            Assert.True(add.IsSuccess);
            InvoiceModel updated = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(2m, updated.Lines.Single().TaxRate);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task UpdateHeaderAsync_ChangingTheCustomer_RecalculatesLineTaxesForTheNewCondition()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid normalCustomerId = await CreateCustomerAsync(services, actorId);
            Guid notSubjectCustomerId = await CreateCustomerAsync(
                services, actorId, vatCondition: VatCondition.NotSubject);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, normalCustomerId, incomeAccountId, taxTypeId, actorId);
            InvoiceModel beforeChange = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(13m, beforeChange.Lines.Single().TaxRate);

            CatalogOperationResult result = await invoices.UpdateHeaderAsync(
                invoiceId,
                new UpdateInvoiceHeaderCommand(
                    notSubjectCustomerId, incomeAccountId, beforeChange.IssueDate, beforeChange.PaymentTerm),
                beforeChange.Version,
                actorId);

            Assert.True(result.IsSuccess);
            Assert.Contains("recalculó el IVA de 1 línea", result.Message);

            InvoiceModel afterChange = (await invoices.GetAsync(invoiceId))!;
            Assert.Equal(0m, afterChange.Lines.Single().TaxRate);
            Assert.Equal("No sujeto", afterChange.Lines.Single().TaxName);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task AddLineAsync_WithAnInactiveCatalogItem_IsRejected()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            ICatalogItemService catalogItems = services.GetRequiredService<ICatalogItemService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid catalogItemId = await CreateCatalogItemAsync(
                services, actorId, "Servicio inactivo", referencePriceCrc: 1_000m, defaultTaxTypeId: taxTypeId);

            CatalogItemModel item = (await catalogItems.GetAsync(catalogItemId))!;
            await catalogItems.SetActiveAsync(catalogItemId, isActive: false, item.Version, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            InvoiceCreationResult created = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
                actorId);
            Guid invoiceId = created.InvoiceId!.Value;
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult add = await invoices.AddLineAsync(
                invoiceId,
                new InvoiceLineInputCommand(catalogItemId, 1m, null, 0m, null),
                draft.Version,
                actorId);

            Assert.Equal(CatalogOperationStatus.Invalid, add.Status);
        });
    }

    private static async Task<Guid> CreateInvoiceWithLineAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeAccountId,
        Guid taxTypeId,
        Guid actorId)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeAccountId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;
        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Línea");
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, 100m, 0m, taxTypeId),
            draft.Version,
            actorId);

        return invoiceId;
    }

    private static async Task<Guid> CreateCustomerAsync(
        IServiceProvider services,
        Guid actorId,
        decimal? creditLimit = null,
        VatCondition vatCondition = VatCondition.Normal,
        bool appliesStateWithholding = false)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de servicio",
            IdentificationType.ForeignNonResident,
            $"ID-{suffix}",
            null,
            null,
            null,
            creditLimit,
            vatCondition,
            appliesStateWithholding,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();
        return customer.Id;
    }

    private static async Task<Guid> CreateCatalogItemAsync(
        IServiceProvider services,
        Guid actorId,
        string name,
        decimal? referencePriceCrc = null,
        Guid? defaultTaxTypeId = null)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        CatalogItem item = new(
            Guid.NewGuid(),
            $"SRV-{suffix}",
            name,
            CatalogItemType.Service,
            "8311500000000",
            null,
            UnitOfMeasureCode.Unit,
            referencePriceCrc,
            null,
            defaultTaxTypeId,
            DateTimeOffset.UtcNow,
            actorId);
        context.CatalogItems.Add(item);
        await context.SaveChangesAsync();
        return item.Id;
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
            "08",
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

        return ledgerAccount.Id;
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
