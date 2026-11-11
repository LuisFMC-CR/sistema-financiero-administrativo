using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Reports;

/// <summary>
/// Paquete v9, bloque 3: el flujo de efectivo sale de las líneas de asiento de las cuentas de efectivo.
/// En un período fabricado (mayo y junio de 2014, sin datos reales) se registran una factura de
/// contado, un abono con retención, un gasto de contado, un pago a proveedor, una nota de crédito con
/// devolución, un traspaso, una liquidación del IVA con pago, un pago de renta y un ingreso anulado. El
/// neto de cada mes debe ser igual a la variación del saldo de las cuentas de efectivo en la balanza
/// de comprobación, sin filtro y con filtro de cuenta. Se habilita con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1
/// y siempre revierte los datos.
/// </summary>
public sealed class SqlServerCashFlowFromJournalScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private const string User = "Usuario de prueba";
    private static readonly DateOnly MayStart = new(2014, 5, 1);
    private static readonly DateOnly MayEnd = new(2014, 5, 31);
    private static readonly DateOnly JuneStart = new(2014, 6, 1);
    private static readonly DateOnly JuneEnd = new(2014, 6, 30);
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerCashFlowFromJournalScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CashFlow_NetEqualsTheCashBalanceChangeInTheTrialBalance_WithAndWithoutAccountFilter()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            await ReportScenarioData.PrepareFabricatedPeriodAsync(services, actorId, MayStart, MayStart, JuneStart);
            await SqlTestIsolation.CancelExistingVatSettlementsAsync(services, actorId);

            Guid bankId = await ReportScenarioData.CreateLedgerAccountAsync(services, actorId, LedgerAccountType.Asset, CashAccountKind.Bank, "Banco de flujo");
            Guid pettyCashId = await ReportScenarioData.CreateLedgerAccountAsync(services, actorId, LedgerAccountType.Asset, CashAccountKind.Cash, "Caja de flujo");
            Guid incomeAccountId = await ReportScenarioData.CreateLedgerAccountAsync(services, actorId, LedgerAccountType.Income, null, "Ingreso de flujo");
            Guid expenseAccountId = await ReportScenarioData.CreateLedgerAccountAsync(services, actorId, LedgerAccountType.Expense, null, "Gasto de flujo");
            Guid vat13Id = await ReportScenarioData.CreateTaxTypeAsync(services, actorId, 13m);
            Guid vat0Id = await ReportScenarioData.CreateTaxTypeAsync(services, actorId, 0m);
            Guid customerId = await ReportScenarioData.CreateCustomerAsync(services, actorId, appliesStateWithholding: true);
            Guid supplierId = await ReportScenarioData.CreateSupplierAsync(services, actorId);
            Guid withholdingTypeId = await ReportScenarioData.CreateWithholdingTypeAsync(services, actorId);

            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();

            // 1. Factura de contado con IVA 13 %: entran 11 300 al banco.
            await ReportScenarioData.CreateAndConfirmInvoiceAsync(
                services, invoices, customerId, incomeAccountId, vat13Id, new DateOnly(2014, 5, 3),
                PaymentTerm.Cash, 10_000m, bankId, actorId);

            // 2. Abono con retención: factura a crédito de 50 000 (IVA 0 %), entran 49 000 y se retienen 1 000.
            Guid creditInvoiceId = await ReportScenarioData.CreateAndConfirmInvoiceAsync(
                services, invoices, customerId, incomeAccountId, vat0Id, new DateOnly(2014, 5, 4),
                PaymentTerm.Credit30, 50_000m, null, actorId);
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Guid receivableId = (await context.Receivables.SingleAsync(r => r.InvoiceId == creditInvoiceId)).Id;
            RegisterReceivablePaymentResult receivablePayment = await services.GetRequiredService<IReceivablePaymentService>()
                .RegisterAsync(
                    new RegisterReceivablePaymentCommand(
                        customerId, bankId, "FLUJO-ABO-1", new DateOnly(2014, 5, 8),
                        [new ReceivablePaymentAllocationCommand(receivableId, 49_000m, new WithholdingInputCommand(withholdingTypeId, 50_000m))]),
                    actorId,
                    actorIsManagement: true);
            Assert.True(receivablePayment.Operation.IsSuccess, receivablePayment.Operation.Message);

            // 3. Gasto de contado desde la caja: salen 1 130 (1 000 + IVA acreditable 130).
            await ReportScenarioData.CreateAndConfirmExpenseAsync(
                expenses, supplierId, expenseAccountId, vat13Id, new DateOnly(2014, 5, 10),
                PaymentTerm.Cash, 1_000m, pettyCashId, actorId);

            // 4. Pago a proveedor: gasto a crédito de 2 000 (IVA 0 %) pagado desde el banco.
            Guid creditExpenseId = await ReportScenarioData.CreateAndConfirmExpenseAsync(
                expenses, supplierId, expenseAccountId, vat0Id, new DateOnly(2014, 5, 11),
                PaymentTerm.Credit30, 2_000m, null, actorId);
            Guid payableId = (await context.Payables.SingleAsync(p => p.ExpenseId == creditExpenseId)).Id;
            RegisterSupplierPaymentResult supplierPayment = await services.GetRequiredService<ISupplierPaymentService>()
                .RegisterAsync(
                    new RegisterSupplierPaymentCommand(
                        supplierId, bankId, "FLUJO-PAG-1", new DateOnly(2014, 5, 12),
                        [new SupplierPaymentAllocationCommand(payableId, 2_000m)]),
                    actorId,
                    actorIsManagement: true);
            Assert.True(supplierPayment.Operation.IsSuccess, supplierPayment.Operation.Message);

            // 5. Nota de crédito con devolución: factura de contado de 3 000 (IVA 0 %) devuelta desde la caja.
            Guid refundedInvoiceId = await ReportScenarioData.CreateAndConfirmInvoiceAsync(
                services, invoices, customerId, incomeAccountId, vat0Id, new DateOnly(2014, 5, 13),
                PaymentTerm.Cash, 3_000m, pettyCashId, actorId);
            ICreditNoteService creditNotes = services.GetRequiredService<ICreditNoteService>();
            CreditNoteInvoiceOption eligible = (await creditNotes.GetInvoiceAsync(refundedInvoiceId))!;
            CreditNoteRegistrationResult creditNote = await creditNotes.RegisterAsync(
                new RegisterCreditNoteCommand(
                    refundedInvoiceId, new DateOnly(2014, 5, 15), "Devolución de la venta", pettyCashId, "FLUJO-DEV-1",
                    eligible.InvoiceVersion),
                actorId,
                actorIsManagement: true);
            Assert.True(creditNote.Operation.IsSuccess, creditNote.Operation.Message);

            // 6. Traspaso del banco a la caja: 5 000.
            RegisterCashTransferResult transfer = await services.GetRequiredService<ICashTransferService>().RegisterAsync(
                new RegisterCashTransferCommand(bankId, pettyCashId, 5_000m, "FLUJO-TRA-1", "Reposición de caja", new DateOnly(2014, 5, 16)),
                actorId,
                actorIsManagement: true);
            Assert.True(transfer.Operation.IsSuccess, transfer.Operation.Message);

            // 7. Ingreso manual de 700 a la caja, anulado: no debe mover el flujo.
            IIncomeService incomes = services.GetRequiredService<IIncomeService>();
            IncomeCreationResult manualIncome = await incomes.CreateManualAsync(
                new CreateManualIncomeCommand(
                    incomeAccountId, pettyCashId, 700m, new DateOnly(2014, 5, 18), PaymentMethod.Cash, "FLUJO-ING-1", "Reintegro"),
                actorId);
            Assert.True(manualIncome.Operation.IsSuccess, manualIncome.Operation.Message);
            IncomeModel income = (await incomes.GetAsync(manualIncome.IncomeId!.Value))!;
            CatalogOperationResult incomeCancellation = await incomes.CancelAsync(
                income.Id, "Registrado por error", income.Version, actorId);
            Assert.True(incomeCancellation.IsSuccess, incomeCancellation.Message);

            // 8. Liquidación del IVA de mayo con pago desde el banco (junio): 1 300 − 130 = 1 170.
            RegisterVatSettlementResult settlement = await services.GetRequiredService<IVatSettlementService>().RegisterAsync(
                new RegisterVatSettlementCommand(2014, 5, new DateOnly(2014, 6, 5), bankId, "FLUJO-LIQ-1"),
                actorId,
                actorIsManagement: true);
            Assert.True(settlement.Operation.IsSuccess, settlement.Operation.Message);

            // 9. Pago de renta desde el banco (junio): 4 000.
            RegisterIncomeTaxPaymentResult incomeTaxPayment = await services.GetRequiredService<IIncomeTaxService>().RegisterPaymentAsync(
                new RegisterIncomeTaxPaymentCommand(MayStart, bankId, PaymentMethod.BankTransfer, 4_000m, "FLUJO-PIR-1", null, new DateOnly(2014, 6, 20)),
                actorId,
                actorIsManagement: true);
            Assert.True(incomeTaxPayment.Operation.IsSuccess, incomeTaxPayment.Operation.Message);

            IReportService reports = services.GetRequiredService<IReportService>();
            List<string> allCashCodes = await context.LedgerAccounts.AsNoTracking()
                .Where(account => account.CashKind != null)
                .Select(account => account.Code)
                .ToListAsync();
            string bankCode = await CodeOfAsync(context, bankId);
            string pettyCashCode = await CodeOfAsync(context, pettyCashId);

            // Sin filtro: todas las cuentas de efectivo.
            CashFlowReportResult all = await reports.GetCashFlowReportAsync(MayStart, JuneEnd, null, User);
            await AssertMonthlyNetMatchesTrialBalanceAsync(reports, all, allCashCodes);
            Assert.All(all.Lines, line => Assert.Equal(0m, line.TransfersReceived));
            Assert.All(all.Lines, line => Assert.Equal(0m, line.TransfersSent));
            // Mayo: 11 300 + 49 000 + 3 000 entran; 1 130 + 2 000 + 3 000 salen (el traspaso y el ingreso anulado no cuentan).
            Assert.Equal(63_300m, all.Lines[0].TotalInflow);
            Assert.Equal(6_130m, all.Lines[0].TotalOutflow);
            // Junio: salen 1 170 de IVA y 4 000 de renta.
            Assert.Equal(1_170m, all.Lines[1].VatPayments);
            Assert.Equal(5_170m, all.Lines[1].TotalOutflow);

            // Con filtro de cuenta: el traspaso aparece como enviado (banco) y recibido (caja).
            CashFlowReportResult bank = await reports.GetCashFlowReportAsync(MayStart, JuneEnd, bankId, User);
            await AssertMonthlyNetMatchesTrialBalanceAsync(reports, bank, [bankCode]);
            Assert.Equal(5_000m, bank.Lines[0].TransfersSent);
            Assert.Equal(1_170m, bank.Lines[1].VatPayments);

            CashFlowReportResult pettyCash = await reports.GetCashFlowReportAsync(MayStart, JuneEnd, pettyCashId, User);
            await AssertMonthlyNetMatchesTrialBalanceAsync(reports, pettyCash, [pettyCashCode]);
            Assert.Equal(5_000m, pettyCash.Lines[0].TransfersReceived);
        });
    }

    /// <summary>
    /// El neto de cada mes del flujo es igual a la variación del saldo (débito − crédito) de las cuentas
    /// indicadas entre la balanza al cierre del mes anterior y la balanza al cierre del mes.
    /// </summary>
    private static async Task AssertMonthlyNetMatchesTrialBalanceAsync(
        IReportService reports, CashFlowReportResult cashFlow, IReadOnlyCollection<string> accountCodes)
    {
        Assert.NotEmpty(cashFlow.Lines);
        foreach (CashFlowReportLine line in cashFlow.Lines)
        {
            DateOnly monthStart = new(line.PeriodYear, line.PeriodMonth, 1);
            decimal opening = await CashBalanceAsync(reports, monthStart.AddDays(-1), accountCodes);
            decimal closing = await CashBalanceAsync(reports, monthStart.AddMonths(1).AddDays(-1), accountCodes);
            Assert.Equal(closing - opening, line.Net);
        }

        Assert.Equal(cashFlow.Lines.Sum(line => line.Net), cashFlow.GrandTotalNet);
    }

    private static async Task<decimal> CashBalanceAsync(
        IReportService reports, DateOnly cutoff, IReadOnlyCollection<string> accountCodes)
    {
        TrialBalanceReportResult trialBalance = await reports.GetTrialBalanceReportAsync(cutoff, User);
        return trialBalance.Groups
            .SelectMany(group => group.Accounts)
            .Where(account => accountCodes.Contains(account.AccountCode))
            .Sum(account => account.DebitBalance - account.CreditBalance);
    }

    private static Task<string> CodeOfAsync(FinancialDbContext context, Guid accountId) =>
        context.LedgerAccounts.AsNoTracking().Where(account => account.Id == accountId).Select(account => account.Code).SingleAsync();

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
        Guid actorId = await ReportScenarioData.CreateUserAsync(scope.ServiceProvider);

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
