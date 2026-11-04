using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Reports;

/// <summary>
/// Escenarios reales de los reportes contables (libro diario RF-80, libro mayor RF-81, balanza de
/// comprobación RF-82 y estado de resultados RF-83, bloque 6 del paquete de mejoras finales) que se
/// habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos. Las fechas de negocio
/// usadas son siempre de un año futuro fabricado (never antes usado en pruebas ni en datos reales) para
/// que las aserciones de totales de un período no dependan de qué otros datos existan ya en la base de
/// desarrollo; las cuentas de efectivo, de categoría y de grupo también se crean nuevas en cada prueba
/// por el mismo motivo.
/// </summary>
public sealed class SqlServerAccountingReportScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerAccountingReportScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task JournalBookReport_IncludesEntriesWithinThePeriodWithBalancedTotals()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IReportService reports = services.GetRequiredService<IReportService>();

            DateOnly entryDate = new(2027, 6, 15);
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid expenseCategoryId = await CreateExpenseCategoryAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid invoiceId = await CreateInvoiceWithLineOnDateAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, 100m, entryDate);
            InvoiceModel invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"), invoiceDraft.Version, actorId);

            Guid expenseId = await CreateExpenseOnDateAsync(
                expenses, supplierId, expenseCategoryId, taxTypeId, 50m, entryDate, actorId);
            ExpenseModel expenseDraft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REC-0001"), expenseDraft.Version, actorId, actorIsManagement: false);

            JournalBookReportResult result = await reports.GetJournalBookReportAsync(entryDate, entryDate, "Usuario de prueba");

            Assert.Equal(2, result.Entries.Count);
            Assert.Contains(result.Entries, entry => entry.SourceType == JournalEntrySourceType.Invoice);
            Assert.Contains(result.Entries, entry => entry.SourceType == JournalEntrySourceType.Expense);
            Assert.All(result.Entries, entry => Assert.Equal(
                entry.Lines.Sum(line => line.Debit), entry.Lines.Sum(line => line.Credit)));
            Assert.Equal(result.TotalDebit, result.TotalCredit);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task JournalBookReport_WithNoEntriesInThePeriod_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();

            JournalBookReportResult result = await reports.GetJournalBookReportAsync(
                new DateOnly(1900, 1, 1), new DateOnly(1900, 1, 2), "Usuario de prueba");

            Assert.Empty(result.Entries);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task LedgerBookReport_ComputesOpeningAndRunningBalanceByAccountNature()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IReportService reports = services.GetRequiredService<IReportService>();

            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid expenseCategoryId = await CreateExpenseCategoryAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            DateOnly beforePeriodDate = new(2027, 1, 10);
            DateOnly periodFrom = new(2027, 1, 15);
            DateOnly periodTo = new(2027, 1, 25);
            DateOnly withinPeriodDate = new(2027, 1, 20);

            Guid openingInvoiceId = await CreateInvoiceWithLineOnDateAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, 200m, beforePeriodDate);
            InvoiceModel openingDraft = (await invoices.GetAsync(openingInvoiceId))!;
            await invoices.ConfirmAsync(
                openingInvoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-OPEN"), openingDraft.Version, actorId);

            Guid expenseId = await CreateExpenseOnDateAsync(
                expenses, supplierId, expenseCategoryId, taxTypeId, 80m, withinPeriodDate, actorId);
            ExpenseModel expenseDraft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REC-OPEN"), expenseDraft.Version, actorId, actorIsManagement: false);

            LedgerBookReportResult result = await reports.GetLedgerBookReportAsync(
                cashAccountId, periodFrom, periodTo, "Usuario de prueba");

            // Activo: naturaleza deudora. El saldo anterior es el débito de la factura, previo al período.
            Assert.Equal(200m, result.OpeningBalance);
            LedgerBookMovement movement = Assert.Single(result.Movements);
            Assert.Equal(withinPeriodDate, movement.EntryDate);
            Assert.Equal(0m, movement.Debit);
            Assert.Equal(80m, movement.Credit);
            Assert.Equal(120m, movement.RunningBalance);
            Assert.Equal(120m, result.ClosingBalance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task LedgerBookReport_OnAFreshAccountWithNoMovements_ReturnsZeroBalances()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();
            Guid freshAccountId = await CreateCashAccountAsync(services, actorId);

            LedgerBookReportResult result = await reports.GetLedgerBookReportAsync(
                freshAccountId, new DateOnly(2020, 1, 1), new DateOnly(2020, 1, 31), "Usuario de prueba");

            Assert.Empty(result.Movements);
            Assert.Equal(0m, result.OpeningBalance);
            Assert.Equal(0m, result.ClosingBalance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task TrialBalanceReport_GroupsDetailAccountsUnderTheirParentWithMatchingSubtotals()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IReportService reports = services.GetRequiredService<IReportService>();
            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();

            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "0");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            LedgerAccount parent = new(
                Guid.NewGuid(),
                $"GRP-{suffix}",
                "Grupo de pruebas de balanza",
                LedgerAccountType.Expense,
                null,
                null,
                null,
                null,
                DateTimeOffset.UtcNow,
                actorId);
            context.LedgerAccounts.Add(parent);
            await context.SaveChangesAsync();

            Guid categoryAId = await CreateChildExpenseCategoryAsync(services, actorId, parent.Id, "A");
            Guid categoryBId = await CreateChildExpenseCategoryAsync(services, actorId, parent.Id, "B");

            DateOnly entryDate = new(2027, 3, 1);

            Guid expenseAId = await CreateExpenseOnDateAsync(expenses, supplierId, categoryAId, taxTypeId, 100m, entryDate, actorId);
            ExpenseModel draftA = (await expenses.GetAsync(expenseAId))!;
            await expenses.ConfirmAsync(
                expenseAId, new ConfirmExpenseCommand(cashAccountId, "REC-A"), draftA.Version, actorId, actorIsManagement: false);

            Guid expenseBId = await CreateExpenseOnDateAsync(expenses, supplierId, categoryBId, taxTypeId, 50m, entryDate, actorId);
            ExpenseModel draftB = (await expenses.GetAsync(expenseBId))!;
            await expenses.ConfirmAsync(
                expenseBId, new ConfirmExpenseCommand(cashAccountId, "REC-B"), draftB.Version, actorId, actorIsManagement: false);

            TrialBalanceReportResult result = await reports.GetTrialBalanceReportAsync(entryDate, "Usuario de prueba");

            TrialBalanceGroupLine group = Assert.Single(result.Groups, candidate => candidate.GroupCode == parent.Code);
            Assert.Equal(2, group.Accounts.Count);
            Assert.Equal(150m, group.TotalDebit);
            Assert.Equal(0m, group.TotalCredit);
            Assert.Equal(150m, group.DebitBalance);
            Assert.Equal(0m, group.CreditBalance);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task TrialBalanceReport_WithNoMovementsUpToTheCutoff_ReturnsAnEmptyResult()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();

            TrialBalanceReportResult result = await reports.GetTrialBalanceReportAsync(
                new DateOnly(1900, 1, 1), "Usuario de prueba");

            Assert.Empty(result.Groups);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task IncomeStatementReport_CreditsIncomeDebitsExpenseAndExcludesVat()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IExpenseService expenses = services.GetRequiredService<IExpenseService>();
            IReportService reports = services.GetRequiredService<IReportService>();

            DateOnly entryDate = new(2027, 4, 1);
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid supplierId = await CreateSupplierAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId, "13");
            Guid cashAccountId = await CreateCashAccountAsync(services, actorId);
            Guid incomeCategoryId = await CreateIncomeCategoryAsync(services, actorId);
            Guid expenseCategoryId = await CreateExpenseCategoryAsync(services, actorId);
            await SetAuthorizationLimitAsync(services, actorId, 10_000m);

            Guid incomeAccountId = await GetCategoryLedgerAccountIdAsync(services, incomeCategoryId);
            Guid expenseAccountId = await GetCategoryLedgerAccountIdAsync(services, expenseCategoryId);
            Guid vatPayableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.VatPayable);
            Guid vatCreditableAccountId = await GetSystemAccountIdAsync(services, LedgerAccountSystemRole.VatCreditable);
            string incomeAccountCode = await GetLedgerAccountCodeAsync(services, incomeAccountId);
            string expenseAccountCode = await GetLedgerAccountCodeAsync(services, expenseAccountId);
            string vatPayableCode = await GetLedgerAccountCodeAsync(services, vatPayableAccountId);
            string vatCreditableCode = await GetLedgerAccountCodeAsync(services, vatCreditableAccountId);

            Guid invoiceId = await CreateInvoiceWithLineOnDateAsync(
                services, invoices, customerId, incomeCategoryId, taxTypeId, actorId, 100m, entryDate);
            InvoiceModel invoiceDraft = (await invoices.GetAsync(invoiceId))!;
            await invoices.ConfirmAsync(
                invoiceId, new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "F-0001"), invoiceDraft.Version, actorId);

            Guid expenseId = await CreateExpenseOnDateAsync(
                expenses, supplierId, expenseCategoryId, taxTypeId, 40m, entryDate, actorId);
            ExpenseModel expenseDraft = (await expenses.GetAsync(expenseId))!;
            await expenses.ConfirmAsync(
                expenseId, new ConfirmExpenseCommand(cashAccountId, "REC-0001"), expenseDraft.Version, actorId, actorIsManagement: false);

            IncomeStatementReportResult result = await reports.GetIncomeStatementReportAsync(
                entryDate, entryDate, "Usuario de prueba");

            IncomeStatementAccountLine incomeLine = result.IncomeGroups
                .SelectMany(group => group.Accounts)
                .Single(account => account.AccountCode == incomeAccountCode);
            IncomeStatementAccountLine expenseLine = result.ExpenseGroups
                .SelectMany(group => group.Accounts)
                .Single(account => account.AccountCode == expenseAccountCode);

            Assert.Equal(100m, incomeLine.Amount);
            Assert.Equal(40m, expenseLine.Amount);
            Assert.Equal(100m, result.TotalIncome);
            Assert.Equal(40m, result.TotalExpense);
            Assert.Equal(60m, result.NetResult);

            Assert.DoesNotContain(
                result.IncomeGroups.SelectMany(group => group.Accounts), account => account.AccountCode == vatPayableCode);
            Assert.DoesNotContain(
                result.ExpenseGroups.SelectMany(group => group.Accounts), account => account.AccountCode == vatCreditableCode);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task IncomeStatementReport_WithNoMovementsInThePeriod_ReturnsEmptyGroups()
    {
        return RunInRollbackTransactionAsync(async (services, _) =>
        {
            IReportService reports = services.GetRequiredService<IReportService>();

            IncomeStatementReportResult result = await reports.GetIncomeStatementReportAsync(
                new DateOnly(1900, 1, 1), new DateOnly(1900, 1, 2), "Usuario de prueba");

            Assert.Empty(result.IncomeGroups);
            Assert.Empty(result.ExpenseGroups);
            Assert.Equal(0m, result.TotalIncome);
            Assert.Equal(0m, result.TotalExpense);
        });
    }

    private static async Task<Guid> CreateInvoiceWithLineOnDateAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeCategoryId,
        Guid taxTypeId,
        Guid actorId,
        decimal netAmount,
        DateOnly issueDate)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeCategoryId, issueDate, PaymentTerm.Cash),
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

    private static async Task<Guid> CreateExpenseOnDateAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid categoryId,
        Guid taxTypeId,
        decimal netAmount,
        DateOnly issueDate,
        Guid actorId)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, categoryId, "Compra de prueba", "VC-0001", issueDate, PaymentTerm.Cash, netAmount, taxTypeId, true, PaymentMethod.Cash),
            actorId);

        Assert.True(created.Operation.IsSuccess);
        return created.ExpenseId!.Value;
    }

    private static async Task<Guid> CreateChildExpenseCategoryAsync(
        IServiceProvider services, Guid actorId, Guid parentAccountId, string label)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(),
            $"GAS-{label}-{suffix}",
            $"Cuenta de gasto {label} de pruebas de balanza",
            LedgerAccountType.Expense,
            parentAccountId,
            null,
            null,
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.LedgerAccounts.Add(ledgerAccount);
        await context.SaveChangesAsync();

        FinancialCategory category = new(
            Guid.NewGuid(),
            $"CAT-{label}-{suffix}",
            $"Categoría de gasto {label} de pruebas de balanza",
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

    private static async Task<string> GetLedgerAccountCodeAsync(IServiceProvider services, Guid accountId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.LedgerAccounts.AsNoTracking().SingleAsync(account => account.Id == accountId)).Code;
    }

    private static async Task<Guid> GetCategoryLedgerAccountIdAsync(IServiceProvider services, Guid categoryId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.FinancialCategories.AsNoTracking().SingleAsync(category => category.Id == categoryId))
            .LedgerAccountId;
    }

    private static async Task<Guid> GetSystemAccountIdAsync(IServiceProvider services, LedgerAccountSystemRole role)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.LedgerAccounts.AsNoTracking().SingleAsync(account => account.SystemRole == role)).Id;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de reportes contables",
            IdentificationType.ForeignNonResident,
            $"ID-{suffix}",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
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
            "Proveedor de pruebas de reportes contables",
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
            "Caja de pruebas de reportes contables",
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
            "Cuenta de ingreso de pruebas de reportes contables",
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
            "Categoría de ingreso de pruebas de reportes contables",
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
            "Cuenta de gasto de pruebas de reportes contables",
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
            "Categoría de gasto de pruebas de reportes contables",
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
