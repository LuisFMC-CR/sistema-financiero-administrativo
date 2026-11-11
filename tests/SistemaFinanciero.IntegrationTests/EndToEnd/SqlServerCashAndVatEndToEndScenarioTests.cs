using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.EndToEnd;

/// <summary>
/// Prueba de extremo a extremo del paquete v8 (bloque 3, punto 5): traspaso del banco a la caja chica,
/// gasto de contado desde la caja, factura y gasto con IVA, y liquidación del mes. Comprueba que la
/// balanza cuadra, que el IVA del período coincide con los movimientos de las cuentas de IVA sin contar
/// las liquidaciones, y que el flujo de efectivo coincide con el libro mayor de las cuentas de efectivo.
/// Se habilita con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierte los datos.
/// </summary>
/// <remarks>
/// La factura de contado se confirma con una cuenta de efectivo propia, aparte de las dos del flujo
/// principal (banco y caja chica), y se excluye de la comprobación "flujo de efectivo contra libro
/// mayor" para que esa comprobación solo mire los movimientos de este paquete. Desde el paquete v9
/// (bloque 1) el ingreso de la factura de contado toma la fecha de la factura, la misma de su asiento.
/// </remarks>
public sealed class SqlServerCashAndVatEndToEndScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerCashAndVatEndToEndScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task FullCycle_TransferExpensesInvoiceAndVatSettlement_KeepsTheBooksConsistent()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            DateOnly monthStart = new(2016, 8, 1);
            DateOnly monthEnd = new(2016, 8, 31);
            DateOnly transferDate = new(2016, 8, 5);
            DateOnly pettyExpenseDate = new(2016, 8, 6);
            DateOnly invoiceDate = new(2016, 8, 10);
            DateOnly vatExpenseDate = new(2016, 8, 12);
            DateOnly settlementDate = new(2016, 9, 5);
            DateOnly rangeFrom = monthStart;
            DateOnly rangeTo = new(2016, 9, 30);

            await PrepareFabricatedPeriodAsync(services, actorId, transferDate, settlementDate);
            await SetAuthorizationLimitAsync(services, actorId, 100_000m);

            Guid bankAccountId = await CreateCashAccountAsync(services, actorId, CashAccountKind.Bank);
            Guid pettyCashAccountId = await CreateCashAccountAsync(services, actorId, CashAccountKind.Cash);
            Guid salesCashAccountId = await CreateCashAccountAsync(services, actorId, CashAccountKind.Bank);

            // 1. Traspaso del banco a la caja chica.
            ICashTransferService transfers = services.GetRequiredService<ICashTransferService>();
            RegisterCashTransferResult transfer = await transfers.RegisterAsync(
                new RegisterCashTransferCommand(
                    bankAccountId, pettyCashAccountId, 10_000m, "TRA-E2E-1", "Reposición de caja chica", transferDate),
                actorId,
                actorIsManagement: false);
            Assert.True(transfer.Operation.IsSuccess);

            // 2. Gasto de contado desde la caja (sin IVA: solo prueba el movimiento de efectivo).
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid expenseAccountId = await CreateExpenseCategoryAsync(services, actorId);
            Guid noVatTaxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, expenseAccountId, noVatTaxTypeId, pettyCashAccountId, pettyExpenseDate,
                netAmount: 1000m, vatCreditable: true, actorId);

            // 3. Factura y gasto con IVA (260 por pagar, 104 acreditable).
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio con IVA de prueba de extremo a extremo");
            Guid vatTaxTypeId = await CreateTaxTypeAsync(services, actorId, "13");

            InvoiceCreationResult createdInvoice = await invoices.CreateAsync(
                new CreateInvoiceCommand(customerId, incomeAccountId, invoiceDate, PaymentTerm.Cash), actorId);
            Guid invoiceId = createdInvoice.InvoiceId!.Value;
            InvoiceModel invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.AddLineAsync(
                invoiceId, new InvoiceLineInputCommand(catalogItemId, 1m, 2000m, 0m, vatTaxTypeId), invoiceDraft.Version, actorId);
            invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            CatalogOperationResult confirmedInvoice = await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(salesCashAccountId, PaymentMethod.Cash, "F-E2E-1"), invoiceDraft.Version, actorId);
            Assert.True(confirmedInvoice.IsSuccess);

            await CreateAndConfirmCashExpenseAsync(
                expenses, supplierId, expenseAccountId, vatTaxTypeId, bankAccountId, vatExpenseDate,
                netAmount: 800m, vatCreditable: true, actorId);

            // 4. Liquidación del mes: paga 156 (260 - 104) desde el banco.
            IVatSettlementService settlements = services.GetRequiredService<IVatSettlementService>();
            RegisterVatSettlementResult settlement = await settlements.RegisterAsync(
                new RegisterVatSettlementCommand(monthStart.Year, monthStart.Month, settlementDate, bankAccountId, "PAGO-E2E-1"),
                actorId,
                actorIsManagement: true);
            Assert.True(settlement.Operation.IsSuccess, settlement.Operation.Message);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            IReportService reports = services.GetRequiredService<IReportService>();

            // Comprobación 1: la balanza cuadra (invariante de partida doble, cumulativo desde siempre).
            TrialBalanceReportResult trialBalance = await reports.GetTrialBalanceReportAsync(
                settlementDate, "Usuario de prueba");
            Assert.Equal(trialBalance.GrandTotalDebit, trialBalance.GrandTotalCredit);

            // Comprobación 2: el IVA del período coincide con los movimientos de las cuentas de IVA,
            // sin contar los asientos de la liquidación, y la liquidación vigente aparece con su monto.
            Guid vatPayableAccountId = await context.LedgerAccounts.AsNoTracking()
                .Where(account => account.SystemRole == LedgerAccountSystemRole.VatPayable)
                .Select(account => account.Id)
                .SingleAsync();
            Guid vatCreditableAccountId = await context.LedgerAccounts.AsNoTracking()
                .Where(account => account.SystemRole == LedgerAccountSystemRole.VatCreditable)
                .Select(account => account.Id)
                .SingleAsync();

            decimal vatPayableFromLedger = await SumAccountMovementsExcludingSettlementsAsync(
                context, vatPayableAccountId, monthStart, monthEnd, creditMinusDebit: true);
            decimal vatCreditableFromLedger = await SumAccountMovementsExcludingSettlementsAsync(
                context, vatCreditableAccountId, monthStart, monthEnd, creditMinusDebit: false);

            Assert.Equal(260m, vatPayableFromLedger);
            Assert.Equal(104m, vatCreditableFromLedger);

            VatPeriodReportResult vatPeriod = await reports.GetVatPeriodReportAsync(monthStart, monthEnd, "Usuario de prueba");
            Assert.Equal(vatPayableFromLedger, vatPeriod.TotalVatCollected);
            Assert.Equal(vatCreditableFromLedger, vatPeriod.TotalVatPaid);
            Assert.NotNull(vatPeriod.Settlement);
            Assert.Matches("^LIQ-\\d{6}$", vatPeriod.Settlement!.Code);
            Assert.Equal(156m, vatPeriod.Settlement.PaymentAmount);
            Assert.Equal(0m, vatPeriod.Settlement.CreditCarriedForward);

            // Comprobación 3: el flujo de efectivo coincide con el libro mayor de banco y caja chica
            // (la cuenta de la factura queda fuera a propósito; ver el comentario de la clase).
            await AssertCashFlowMatchesLedgerAsync(reports, bankAccountId, rangeFrom, rangeTo);
            await AssertCashFlowMatchesLedgerAsync(reports, pettyCashAccountId, rangeFrom, rangeTo);

            // Montos esperados, calculados a mano, para que las comprobaciones anteriores no queden
            // satisfechas por una coincidencia entre dos números iguales pero vacíos.
            LedgerBookReportResult bankLedger = await reports.GetLedgerBookReportAsync(
                bankAccountId, rangeFrom, rangeTo, "Usuario de prueba");
            Assert.Equal(-11_060m, bankLedger.ClosingBalance - bankLedger.OpeningBalance);

            LedgerBookReportResult pettyCashLedger = await reports.GetLedgerBookReportAsync(
                pettyCashAccountId, rangeFrom, rangeTo, "Usuario de prueba");
            Assert.Equal(9_000m, pettyCashLedger.ClosingBalance - pettyCashLedger.OpeningBalance);
        });
    }

    private static async Task AssertCashFlowMatchesLedgerAsync(
        IReportService reports, Guid cashAccountId, DateOnly rangeFrom, DateOnly rangeTo)
    {
        LedgerBookReportResult ledger = await reports.GetLedgerBookReportAsync(
            cashAccountId, rangeFrom, rangeTo, "Usuario de prueba");
        decimal ledgerNetMovement = ledger.ClosingBalance - ledger.OpeningBalance;

        CashFlowReportResult cashFlow = await reports.GetCashFlowReportAsync(
            rangeFrom, rangeTo, cashAccountId, "Usuario de prueba");

        Assert.Equal(ledgerNetMovement, cashFlow.GrandTotalNet);
    }

    /// <summary>
    /// Replica, directamente sobre los asientos, la misma exclusión que hace internamente
    /// <c>VatSettlementService</c> al calcular A y B: nunca cuenta los asientos de liquidación ni sus
    /// reversiones.
    /// </summary>
    private static async Task<decimal> SumAccountMovementsExcludingSettlementsAsync(
        FinancialDbContext context, Guid accountId, DateOnly periodFrom, DateOnly periodTo, bool creditMinusDebit)
    {
        var lines = await context.Set<JournalEntry>()
            .AsNoTracking()
            .Where(entry => entry.SourceType != JournalEntrySourceType.VatSettlement
                && entry.EntryDate >= periodFrom
                && entry.EntryDate <= periodTo)
            .SelectMany(entry => entry.Lines)
            .Where(line => line.AccountId == accountId)
            .Select(line => new { line.Debit, line.Credit })
            .ToListAsync();

        return creditMinusDebit
            ? lines.Sum(line => line.Credit - line.Debit)
            : lines.Sum(line => line.Debit - line.Credit);
    }

    private static async Task<Guid> CreateAndConfirmCashExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid accountId,
        Guid taxTypeId,
        Guid cashAccountId,
        DateOnly issueDate,
        decimal netAmount,
        bool vatCreditable,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, accountId, "Compra de prueba de extremo a extremo", $"VC-{Guid.NewGuid():N}"[..8],
                issueDate, PaymentTerm.Cash, netAmount, taxTypeId, vatCreditable, PaymentMethod.Cash),
            actorId);
        Guid expenseId = created.ExpenseId!.Value;
        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        CatalogOperationResult confirmed = await expenses.ConfirmAsync(
            expenseId,
            new ConfirmExpenseCommand(cashAccountId, $"REF-{Guid.NewGuid():N}"[..8]),
            draft.Version,
            actorId,
            actorIsManagement: true);
        Assert.True(confirmed.IsSuccess);

        return expenseId;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de prueba de extremo a extremo",
            IdentificationType.ForeignNonResident,
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
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
            "Proveedor de prueba de extremo a extremo",
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
            $"IVA {ratePercent}% de prueba de extremo a extremo",
            decimal.Parse(ratePercent, System.Globalization.CultureInfo.InvariantCulture),
            "08",
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateCashAccountAsync(IServiceProvider services, Guid actorId, CashAccountKind kind)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"CASH-{suffix}",
            "Cuenta de efectivo de prueba de extremo a extremo",
            LedgerAccountType.Asset,
            null,
            kind,
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
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"ING-{suffix}",
            "Cuenta de ingreso de prueba de extremo a extremo",
            LedgerAccountType.Income,
            null,
            null,
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
        LedgerAccount account = new(
            Guid.NewGuid(),
            $"GAS-{suffix}",
            "Cuenta de gasto de prueba de extremo a extremo",
            LedgerAccountType.Expense,
            null,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static async Task SetAuthorizationLimitAsync(IServiceProvider services, Guid actorId, decimal limit)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            context.SystemParameters.Add(new SystemParameters(limit, 7, DateTimeOffset.UtcNow, actorId));
        }
        else
        {
            parameters.UpdateValues(limit, parameters.OverdueAlertDays, DateTimeOffset.UtcNow, actorId);
        }

        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Año fabricado, nunca usado por datos reales: desplaza el período fiscal a los meses que cubren
    /// las fechas dadas y los reabre, para que ni el cierre automático ni el rango fiscal bloqueen la
    /// prueba (mismo patrón que <c>SqlServerReportServiceScenarioTests.PrepareFabricatedPeriodAsync</c>).
    /// </summary>
    private static async Task PrepareFabricatedPeriodAsync(IServiceProvider services, Guid actorId, params DateOnly[] dates)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            parameters = new SystemParameters(500_000m, 7, DateTimeOffset.UtcNow, actorId);
            context.SystemParameters.Add(parameters);
        }

        DateOnly earliest = dates.Min();
        DateOnly fiscalStart = new(earliest.Year, earliest.Month, 1);
        parameters.UpdateFiscalPeriod(fiscalStart, fiscalStart.AddYears(1).AddDays(-1), DateTimeOffset.UtcNow, actorId);

        foreach (var yearMonth in dates.Select(d => (d.Year, d.Month)).Distinct())
        {
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                yearMonth.Year, yearMonth.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
        }

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
