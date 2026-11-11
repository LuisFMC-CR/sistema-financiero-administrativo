using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Reports;

/// <summary>
/// Paquete v9, bloque 5 (CU-22): el reporte "IVA del período" suma en compras solo el IVA acreditable;
/// la base de compras incluye todos los gastos confirmados. En un mes fabricado sin registros tardíos
/// (marzo de 2013), IVA cobrado − IVA acreditable del reporte = IVA por pagar − IVA acreditable de la
/// liquidación de ese mes, antes del arrastre. Se habilita con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y
/// siempre revierte los datos.
/// </summary>
public sealed class SqlServerVatPeriodCreditableScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private static readonly DateOnly MonthStart = new(2013, 3, 1);
    private static readonly DateOnly MonthEnd = new(2013, 3, 31);
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerVatPeriodCreditableScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task VatPeriodReport_ExcludesNonCreditableVatAndMatchesTheSettlementBeforeCarryForward()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            await ReportScenarioData.PrepareFabricatedPeriodAsync(services, actorId, MonthStart, MonthStart);
            await SqlTestIsolation.CancelExistingVatSettlementsAsync(services, actorId);

            Guid bankId = await ReportScenarioData.CreateLedgerAccountAsync(
                services, actorId, LedgerAccountType.Asset, CashAccountKind.Bank, "Banco de IVA del período");
            Guid incomeAccountId = await ReportScenarioData.CreateLedgerAccountAsync(
                services, actorId, LedgerAccountType.Income, null, "Ingreso de IVA del período");
            Guid expenseAccountId = await ReportScenarioData.CreateLedgerAccountAsync(
                services, actorId, LedgerAccountType.Expense, null, "Gasto de IVA del período");
            Guid vat13Id = await ReportScenarioData.CreateTaxTypeAsync(services, actorId, 13m);
            Guid customerId = await ReportScenarioData.CreateCustomerAsync(services, actorId, appliesStateWithholding: false);
            Guid supplierId = await ReportScenarioData.CreateSupplierAsync(services, actorId);
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();

            // Venta de 10 000: IVA cobrado 1 300.
            await ReportScenarioData.CreateAndConfirmInvoiceAsync(
                services, invoices, customerId, incomeAccountId, vat13Id, new DateOnly(2013, 3, 5),
                PaymentTerm.Cash, 10_000m, bankId, actorId);

            // Compra acreditable de 2 000 (IVA 260) y compra no acreditable de 1 000 (IVA 130, es costo).
            await ReportScenarioData.CreateAndConfirmExpenseAsync(
                expenses, supplierId, expenseAccountId, vat13Id, new DateOnly(2013, 3, 6),
                PaymentTerm.Cash, 2_000m, bankId, actorId, vatCreditable: true);
            await ReportScenarioData.CreateAndConfirmExpenseAsync(
                expenses, supplierId, expenseAccountId, vat13Id, new DateOnly(2013, 3, 7),
                PaymentTerm.Cash, 1_000m, bankId, actorId, vatCreditable: false);

            VatPeriodReportResult report = await services.GetRequiredService<IReportService>()
                .GetVatPeriodReportAsync(MonthStart, MonthEnd, "Usuario de prueba");
            VatPeriodReportLine line = Assert.Single(report.Lines, candidate => candidate.PurchasesBase > 0m);

            Assert.Equal(3_000m, line.PurchasesBase);
            Assert.Equal(260m, line.VatPaid);
            Assert.Equal(1_300m, report.TotalVatCollected);
            Assert.Equal(260m, report.TotalVatPaid);

            VatSettlementPreview preview = await services.GetRequiredService<IVatSettlementService>()
                .PreviewAsync(MonthStart.Year, MonthStart.Month);
            Assert.Equal(
                preview.VatPayableThisPeriod - preview.VatCreditableThisPeriod,
                report.TotalVatCollected - report.TotalVatPaid);
        });
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
