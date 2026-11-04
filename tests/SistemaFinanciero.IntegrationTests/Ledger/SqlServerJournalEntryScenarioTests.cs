using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Ledger;

/// <summary>
/// Escenarios reales de <c>IJournalEntryService</c> y de las reglas de contabilización automática
/// (RF-78/RF-79, bloque 5 del paquete de mejoras finales) que se habilitan con
/// SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Pasa siempre por los servicios reales
/// (<c>IInvoiceService</c>, <c>IExpenseService</c>, etc.): nunca construye <c>JournalEntry</c>
/// directamente, igual criterio que <c>SqlServerInvoiceServiceScenarioTests</c>.
/// </summary>
public sealed class SqlServerJournalEntryScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerJournalEntryScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Invoice_ConfirmedCash_DebitsCashAndCreditsIncomeAndVat()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid incomeAccountId = await GetCategoryLedgerAccountIdAsync(services, incomeCategoryId);
            Guid vatPayableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.VatPayable);

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, netAmount: 100m);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"), draft.Version, actorId);

            JournalEntryTraceModel? trace = await journal.GetTraceAsync(JournalEntrySourceType.Invoice, invoiceId);
            Assert.NotNull(trace);
            Assert.Null(trace!.Reversal);

            JournalEntryModel entry = trace.Entry;
            Assert.Equal(JournalEntrySourceType.Invoice, entry.SourceType);
            Assert.Equal(invoiceId, entry.SourceId);
            Assert.Equal(3, entry.Lines.Count);
            AssertBalanced(entry);

            AssertLine(entry, cashAccountId, debit: 113m, credit: 0m);
            AssertLine(entry, incomeAccountId, debit: 0m, credit: 100m);
            AssertLine(entry, vatPayableAccountId, debit: 0m, credit: 13m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Invoice_ConfirmedCredit_DebitsReceivablesInsteadOfCash()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid incomeAccountId = await GetCategoryLedgerAccountIdAsync(services, incomeCategoryId);
            Guid vatPayableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.VatPayable);
            Guid receivableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.ReceivablesFromCustomers);

            Guid invoiceId = await CreateCreditInvoiceWithLineAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, netAmount: 100m);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            await invoices.ConfirmAsync(invoiceId, new ConfirmInvoiceCommand(null, null, null), draft.Version, actorId);

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.Invoice, invoiceId))!;
            Assert.Equal(3, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, receivableAccountId, debit: 113m, credit: 0m);
            AssertLine(trace.Entry, incomeAccountId, debit: 0m, credit: 100m);
            AssertLine(trace.Entry, vatPayableAccountId, debit: 0m, credit: 13m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Invoice_CancelledCash_GeneratesAReversalWithSwappedLinesAndTheSameDate()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, netAmount: 100m);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"), draft.Version, actorId);

            InvoiceModel confirmed = (await invoices.GetAsync(invoiceId))!;
            await invoices.CancelAsync(invoiceId, "Error de digitación", confirmed.Version, actorId);

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.Invoice, invoiceId))!;
            Assert.NotNull(trace.Reversal);
            JournalEntryModel reversal = trace.Reversal!;

            Assert.Equal(trace.Entry.Id, reversal.ReversesEntryId);
            Assert.Equal(trace.Entry.EntryDate, reversal.EntryDate);
            AssertBalanced(reversal);

            foreach (JournalEntryLineModel originalLine in trace.Entry.Lines)
            {
                JournalEntryLineModel reversedLine = Assert.Single(
                    reversal.Lines, line => line.AccountId == originalLine.AccountId);
                Assert.Equal(originalLine.Debit, reversedLine.Credit);
                Assert.Equal(originalLine.Credit, reversedLine.Debit);
            }
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Expense_ConfirmedCash_DebitsCategoryAndVatCreditsCash()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid categoryAccountId = await GetCategoryLedgerAccountIdAsync(services, categoryId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid vatCreditableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.VatCreditable);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Cash, 1000m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REC-0001"), draft.Version, actorId, actorIsManagement: false);

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.Expense, expenseId))!;
            Assert.Null(trace.Reversal);
            Assert.Equal(3, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, categoryAccountId, debit: 1000m, credit: 0m);
            AssertLine(trace.Entry, vatCreditableAccountId, debit: 130m, credit: 0m);
            AssertLine(trace.Entry, cashAccountId, debit: 0m, credit: 1130m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Expense_ConfirmedCashWithNonCreditableVat_DebitsCategoryByTheTotalAndSkipsVatCreditable()
    {
        // Bloque 1 del paquete v7, RF-94: el IVA no acreditable forma parte del costo, sin línea
        // propia de IVA acreditable.
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid categoryAccountId = await GetCategoryLedgerAccountIdAsync(services, categoryId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Cash, 1000m, actorId, vatCreditable: false);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REC-0002"), draft.Version, actorId, actorIsManagement: false);

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.Expense, expenseId))!;
            Assert.Equal(2, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, categoryAccountId, debit: 1130m, credit: 0m);
            AssertLine(trace.Entry, cashAccountId, debit: 0m, credit: 1130m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Expense_ConfirmedCredit_CreditsPayablesInsteadOfCash()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid categoryId = await CreateExpenseCategoryAsync(services, actorId);
            Guid categoryAccountId = await GetCategoryLedgerAccountIdAsync(services, categoryId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid vatCreditableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.VatCreditable);
            Guid payablesAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.PayablesToSuppliers);

            Guid expenseId = await CreateDraftExpenseAsync(
                expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Credit30, 1000m, actorId);
            ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(null, null), draft.Version, actorId, actorIsManagement: false);

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.Expense, expenseId))!;
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, categoryAccountId, debit: 1000m, credit: 0m);
            AssertLine(trace.Entry, vatCreditableAccountId, debit: 130m, credit: 0m);
            AssertLine(trace.Entry, payablesAccountId, debit: 0m, credit: 1130m);

            ExpenseModel confirmed = (await expenses.GetAsync(expenseId))!;
            await expenses.CancelAsync(expenseId, "Compra registrada por error", confirmed.Version, actorId);

            JournalEntryTraceModel afterCancel = (await journal.GetTraceAsync(JournalEntrySourceType.Expense, expenseId))!;
            Assert.NotNull(afterCancel.Reversal);
            AssertBalanced(afterCancel.Reversal!);
            Assert.Equal(afterCancel.Entry.Id, afterCancel.Reversal!.ReversesEntryId);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ReceivablePayment_WithAWithholding_DebitsCashAndWithholdingAndCreditsReceivables()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid withholdingTypeId = await CreateWithholdingTypeAsync(services, actorId, rate: 2m);
            Guid receivableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.ReceivablesFromCustomers);
            Guid withholdingAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.WithholdingsReceivable);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 1000m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-0001",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [
                        new ReceivablePaymentAllocationCommand(
                            receivableId, 980m, new WithholdingInputCommand(withholdingTypeId, 1000m)),
                    ]),
                actorId,
                actorIsManagement: false);
            Assert.True(result.Operation.IsSuccess);
            Guid paymentId = result.ReceivablePaymentIds[0];

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.ReceivablePayment, paymentId))!;
            Assert.Equal(3, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, cashAccountId, debit: 980m, credit: 0m);
            AssertLine(trace.Entry, withholdingAccountId, debit: 20m, credit: 0m);
            AssertLine(trace.Entry, receivableAccountId, debit: 0m, credit: 1000m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ReceivablePayment_WithoutAWithholding_OmitsTheWithholdingLineAndCancelPostsAReversal()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid receivableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.ReceivablesFromCustomers);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 100m);

            RegisterReceivablePaymentResult result = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId,
                    cashAccountId,
                    "AB-0002",
                    DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(receivableId, 60m, null)]),
                actorId,
                actorIsManagement: false);
            Guid paymentId = result.ReceivablePaymentIds[0];

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.ReceivablePayment, paymentId))!;
            Assert.Equal(2, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, cashAccountId, debit: 60m, credit: 0m);
            AssertLine(trace.Entry, receivableAccountId, debit: 0m, credit: 60m);

            ReceivablePaymentModel paymentModel = (await payments.GetAsync(paymentId))!;
            await payments.CancelAsync(paymentId, "Abono registrado por error", paymentModel.Version, actorId);

            JournalEntryTraceModel afterCancel = (await journal.GetTraceAsync(JournalEntrySourceType.ReceivablePayment, paymentId))!;
            Assert.NotNull(afterCancel.Reversal);
            Assert.Equal(afterCancel.Entry.Id, afterCancel.Reversal!.ReversesEntryId);
            AssertBalanced(afterCancel.Reversal!);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task SupplierPayment_DebitsPayablesAndCreditsCashAndCancelPostsAReversal()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            ISupplierPaymentService payments = services.GetRequiredService<ISupplierPaymentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid payablesAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.PayablesToSuppliers);
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
            Guid paymentId = result.SupplierPaymentIds[0];

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.SupplierPayment, paymentId))!;
            Assert.Equal(2, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, payablesAccountId, debit: 60m, credit: 0m);
            AssertLine(trace.Entry, cashAccountId, debit: 0m, credit: 60m);

            SupplierPaymentModel paymentModel = (await payments.GetAsync(paymentId))!;
            await payments.CancelAsync(paymentId, "Pago registrado por error", paymentModel.Version, actorId);

            JournalEntryTraceModel afterCancel = (await journal.GetTraceAsync(JournalEntrySourceType.SupplierPayment, paymentId))!;
            Assert.NotNull(afterCancel.Reversal);
            AssertBalanced(afterCancel.Reversal!);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ManualIncome_DebitsCashAndCreditsCategoryAndCancelPostsAReversal()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid incomeAccountId = await GetCategoryLedgerAccountIdAsync(services, incomeCategoryId);

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
            Guid incomeId = created.IncomeId!.Value;

            JournalEntryTraceModel trace = (await journal.GetTraceAsync(JournalEntrySourceType.Income, incomeId))!;
            Assert.Equal(2, trace.Entry.Lines.Count);
            AssertBalanced(trace.Entry);
            AssertLine(trace.Entry, cashAccountId, debit: 15_000m, credit: 0m);
            AssertLine(trace.Entry, incomeAccountId, debit: 0m, credit: 15_000m);

            IncomeModel incomeModel = (await incomes.GetAsync(incomeId))!;
            await incomes.CancelAsync(incomeId, "Registrado por error", incomeModel.Version, actorId);

            JournalEntryTraceModel afterCancel = (await journal.GetTraceAsync(JournalEntrySourceType.Income, incomeId))!;
            Assert.NotNull(afterCancel.Reversal);
            AssertBalanced(afterCancel.Reversal!);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task Income_FromInvoiceOrFromReceivablePayment_HasNoOwnJournalEntry()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivablePaymentService payments = services.GetRequiredService<IReceivablePaymentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            // Ingreso de factura de contado: regla de contabilización — el ingreso no tiene asiento
            // propio, su efecto ya está en el asiento de la factura.
            Guid cashInvoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, netAmount: 100m);
            InvoiceModel draft = (await invoices.GetAsync(cashInvoiceId))!;
            await invoices.ConfirmAsync(
                cashInvoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"), draft.Version, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Income invoiceIncome = await context.Incomes.AsNoTracking().SingleAsync(i => i.InvoiceId == cashInvoiceId);
            Assert.Null(await journal.GetTraceAsync(JournalEntrySourceType.Income, invoiceIncome.Id));

            // Ingreso de abono: mismo criterio.
            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 100m);
            RegisterReceivablePaymentResult registered = await payments.RegisterAsync(
                new RegisterReceivablePaymentCommand(
                    customerId, cashAccountId, "AB-0003", DateOnly.FromDateTime(DateTime.UtcNow),
                    [new ReceivablePaymentAllocationCommand(receivableId, 50m, null)]),
                actorId,
                actorIsManagement: false);

            Income paymentIncome = await context.Incomes.AsNoTracking()
                .SingleAsync(i => i.ReceivablePaymentId == registered.ReceivablePaymentIds[0]);
            Assert.Null(await journal.GetTraceAsync(JournalEntrySourceType.Income, paymentIncome.Id));
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task BalanceAdjustment_OnAReceivable_PostsTheDirectionDependentLines()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivableService receivables = services.GetRequiredService<IReceivableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid receivableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.ReceivablesFromCustomers);
            Guid adjustmentAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.ReceivableAdjustments);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, incomeCategoryId, actorId, amount: 1000m);
            string version = (await receivables.GetAsync(receivableId))!.Version;

            await adjustments.AdjustReceivableAsync(
                receivableId, 300m, BalanceAdjustmentDirection.Decrease, "Ajuste por error de digitación", version, actorId);

            BalanceAdjustmentModel decreaseAdjustment = (await adjustments.SearchByReceivableAsync(receivableId))
                .Single(item => item.Direction == BalanceAdjustmentDirection.Decrease);
            JournalEntryTraceModel decreaseTrace = (await journal.GetTraceAsync(
                JournalEntrySourceType.BalanceAdjustment, decreaseAdjustment.Id))!;
            AssertBalanced(decreaseTrace.Entry);
            AssertLine(decreaseTrace.Entry, adjustmentAccountId, debit: 300m, credit: 0m);
            AssertLine(decreaseTrace.Entry, receivableAccountId, debit: 0m, credit: 300m);
            Assert.Null(decreaseTrace.Reversal);

            version = (await receivables.GetAsync(receivableId))!.Version;
            await adjustments.AdjustReceivableAsync(
                receivableId, 300m, BalanceAdjustmentDirection.Increase, "Corrección del ajuste anterior", version, actorId);

            BalanceAdjustmentModel increaseAdjustment = (await adjustments.SearchByReceivableAsync(receivableId))
                .Single(item => item.Direction == BalanceAdjustmentDirection.Increase);
            JournalEntryTraceModel increaseTrace = (await journal.GetTraceAsync(
                JournalEntrySourceType.BalanceAdjustment, increaseAdjustment.Id))!;
            AssertBalanced(increaseTrace.Entry);
            AssertLine(increaseTrace.Entry, receivableAccountId, debit: 300m, credit: 0m);
            AssertLine(increaseTrace.Entry, adjustmentAccountId, debit: 0m, credit: 300m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task BalanceAdjustment_OnAPayable_PostsTheDirectionDependentLines()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IPayableService payables = services.GetRequiredService<IPayableService>();
            IBalanceAdjustmentService adjustments = services.GetRequiredService<IBalanceAdjustmentService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid payablesAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.PayablesToSuppliers);
            Guid adjustmentAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.PayableAdjustmentsInFavor);

            Guid payableId = await CreateConfirmedCreditPayableAsync(services, expenses, supplierId, actorId, amount: 1000m);
            string version = (await payables.GetAsync(payableId))!.Version;

            await adjustments.AdjustPayableAsync(
                payableId, 300m, BalanceAdjustmentDirection.Decrease, "Ajuste por error de digitación", version, actorId);

            BalanceAdjustmentModel decreaseAdjustment = (await adjustments.SearchByPayableAsync(payableId))
                .Single(item => item.Direction == BalanceAdjustmentDirection.Decrease);
            JournalEntryTraceModel decreaseTrace = (await journal.GetTraceAsync(
                JournalEntrySourceType.BalanceAdjustment, decreaseAdjustment.Id))!;
            AssertBalanced(decreaseTrace.Entry);
            AssertLine(decreaseTrace.Entry, payablesAccountId, debit: 300m, credit: 0m);
            AssertLine(decreaseTrace.Entry, adjustmentAccountId, debit: 0m, credit: 300m);

            version = (await payables.GetAsync(payableId))!.Version;
            await adjustments.AdjustPayableAsync(
                payableId, 300m, BalanceAdjustmentDirection.Increase, "Corrección del ajuste anterior", version, actorId);

            BalanceAdjustmentModel increaseAdjustment = (await adjustments.SearchByPayableAsync(payableId))
                .Single(item => item.Direction == BalanceAdjustmentDirection.Increase);
            JournalEntryTraceModel increaseTrace = (await journal.GetTraceAsync(
                JournalEntrySourceType.BalanceAdjustment, increaseAdjustment.Id))!;
            AssertBalanced(increaseTrace.Entry);
            AssertLine(increaseTrace.Entry, adjustmentAccountId, debit: 300m, credit: 0m);
            AssertLine(increaseTrace.Entry, payablesAccountId, debit: 0m, credit: 300m);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task BackfillAsync_RegeneratesAMissingEntryOnceAndStaysIdempotent()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IJournalEntryService journal = services.GetRequiredService<IJournalEntryService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, netAmount: 100m);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"), draft.Version, actorId);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();

            // El asiento ya existe (lo generó la confirmación en vivo). Se guarda una fotografía de sus
            // líneas y se borra con SQL directo (fuera del change tracker, que lo protege de
            // modificarse o borrarse por el camino normal de EF) para simular un documento previo al
            // bloque 5, sin asiento todavía.
            JournalEntryTraceModel beforeDeletion = (await journal.GetTraceAsync(JournalEntrySourceType.Invoice, invoiceId))!;
            Guid originalEntryId = beforeDeletion.Entry.Id;
            (Guid AccountId, decimal Debit, decimal Credit)[] originalLines = beforeDeletion.Entry.Lines
                .Select(line => (line.AccountId, line.Debit, line.Credit))
                .OrderBy(line => line.AccountId)
                .ToArray();

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM finanzas.Asientos WHERE Id = {originalEntryId}");

            Assert.Null(await journal.GetTraceAsync(JournalEntrySourceType.Invoice, invoiceId));

            await journal.BackfillAsync();

            JournalEntryTraceModel regenerated = (await journal.GetTraceAsync(JournalEntrySourceType.Invoice, invoiceId))!;
            Assert.NotEqual(originalEntryId, regenerated.Entry.Id);
            (Guid AccountId, decimal Debit, decimal Credit)[] regeneratedLines = regenerated.Entry.Lines
                .Select(line => (line.AccountId, line.Debit, line.Credit))
                .OrderBy(line => line.AccountId)
                .ToArray();
            Assert.Equal(originalLines, regeneratedLines);

            // Repetirlo no debe duplicar el asiento de esta factura: sigue existiendo exactamente uno.
            await journal.BackfillAsync();

            int entriesForInvoice = await context.Set<JournalEntry>()
                .CountAsync(entry => entry.SourceType == JournalEntrySourceType.Invoice
                    && entry.SourceId == invoiceId
                    && entry.ReversesEntryId == null);
            Assert.Equal(1, entriesForInvoice);
        });
    }

    private static void AssertBalanced(JournalEntryModel entry)
    {
        decimal totalDebit = entry.Lines.Sum(line => line.Debit);
        decimal totalCredit = entry.Lines.Sum(line => line.Credit);
        Assert.Equal(totalDebit, totalCredit);
        Assert.True(entry.Lines.Count >= 2);
    }

    private static void AssertLine(JournalEntryModel entry, Guid accountId, decimal debit, decimal credit)
    {
        JournalEntryLineModel line = Assert.Single(entry.Lines, candidate => candidate.AccountId == accountId);
        Assert.Equal(debit, line.Debit);
        Assert.Equal(credit, line.Credit);
    }

    private static async Task<Guid> GetSystemAccountIdAsync(IServiceProvider services, LedgerAccountSystemRole role)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.LedgerAccounts.AsNoTracking().SingleAsync(account => account.SystemRole == role)).Id;
    }

    private static async Task<Guid> GetCategoryLedgerAccountIdAsync(IServiceProvider services, Guid categoryId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.FinancialCategories.AsNoTracking().SingleAsync(category => category.Id == categoryId))
            .LedgerAccountId;
    }

    private static async Task<Guid> CreateInvoiceWithLineAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeCategoryId,
        Guid taxTypeId,
        Guid actorId,
        decimal netAmount)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeCategoryId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Cash),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;
        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Línea");
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, netAmount, 0m, taxTypeId),
            draft.Version,
            actorId);

        return invoiceId;
    }

    private static async Task<Guid> CreateCreditInvoiceWithLineAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeCategoryId,
        Guid taxTypeId,
        Guid actorId,
        decimal netAmount)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeCategoryId, DateOnly.FromDateTime(DateTime.UtcNow), PaymentTerm.Credit30),
            actorId);
        Guid invoiceId = created.InvoiceId!.Value;
        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, netAmount, 0m, taxTypeId),
            draft.Version,
            actorId);

        return invoiceId;
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeCategoryId,
        Guid actorId,
        decimal amount)
    {
        Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
        Guid invoiceId = await CreateCreditInvoiceWithLineAsync(
            services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, amount);

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(invoiceId, new ConfirmInvoiceCommand(null, null, null), draft.Version, actorId);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Receivables.AsNoTracking().SingleAsync(r => r.InvoiceId == invoiceId)).Id;
    }

    private static async Task<Guid> CreateDraftExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        PaymentTerm paymentTerm,
        decimal netAmount,
        Guid actorId,
        bool vatCreditable = true)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId,
                categoryId,
                "Compra de prueba",
                "VC-0001",
                DateOnly.FromDateTime(DateTime.UtcNow),
                paymentTerm,
                netAmount,
                taxTypeId,
                vatCreditable,
                PaymentMethod.Cash),
            actorId);

        Assert.True(created.Operation.IsSuccess);
        return created.ExpenseId!.Value;
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

        Guid expenseId = await CreateDraftExpenseAsync(
            expenses, supplierId, categoryId, taxTypeId, PaymentTerm.Credit30, amount, actorId);

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        await expenses.ConfirmAsync(
            expenseId, new ConfirmExpenseCommand(null, null), draft.Version, actorId, actorIsManagement: false);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Payables.AsNoTracking().SingleAsync(p => p.ExpenseId == expenseId)).Id;
    }

    private static async Task<Guid> CreateCustomerAsync(
        IServiceProvider services,
        Guid actorId,
        bool appliesStateWithholding = false)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de asientos",
            IdentificationType.ForeignNonResident,
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            appliesStateWithholding,
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
            "Proveedor de pruebas de asientos",
            IdentificationType.ForeignNonResident,
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

    private static async Task<Guid> CreateCatalogItemAsync(IServiceProvider services, Guid actorId, string name)
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
            null,
            null,
            null,
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
            "Caja de pruebas de asientos",
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
            "Cuenta de ingreso de pruebas de asientos",
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
            "Categoría de ingreso de pruebas de asientos",
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

    private static async Task<Guid> CreateExpenseCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de pruebas de asientos",
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
            "Categoría de gasto de pruebas de asientos",
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
