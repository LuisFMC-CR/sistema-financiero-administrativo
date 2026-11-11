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
/// Datos de prueba compartidos por los escenarios SQL de reportes del paquete v9 (bloques 3 y 5).
/// Todo se crea dentro de la transacción de la prueba, que siempre se revierte.
/// </summary>
internal static class ReportScenarioData
{
    public static async Task<Guid> CreateAndConfirmInvoiceAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeAccountId,
        Guid taxTypeId,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        decimal amount,
        Guid? cashAccountId,
        Guid actorId)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeAccountId, issueDate, paymentTerm), actorId);
        Assert.True(created.Operation.IsSuccess, created.Operation.Message);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        Guid itemId = await CreateCatalogItemAsync(services, actorId);
        CatalogOperationResult line = await invoices.AddLineAsync(
            invoiceId, new InvoiceLineInputCommand(itemId, 1m, amount, 0m, taxTypeId), draft.Version, actorId);
        Assert.True(line.IsSuccess, line.Message);

        draft = (await invoices.GetAsync(invoiceId))!;
        ConfirmInvoiceCommand confirm = cashAccountId is Guid cash
            ? new ConfirmInvoiceCommand(cash, PaymentMethod.Cash, $"F-{Guid.NewGuid():N}"[..12])
            : new ConfirmInvoiceCommand(null, null, null);
        CatalogOperationResult confirmed = await invoices.ConfirmAsync(invoiceId, confirm, draft.Version, actorId);
        Assert.True(confirmed.IsSuccess, confirmed.Message);
        return invoiceId;
    }

    public static async Task<Guid> CreateAndConfirmExpenseAsync(
        IExpenseService expenses,
        Guid supplierId,
        Guid accountId,
        Guid taxTypeId,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        decimal netAmount,
        Guid? cashAccountId,
        Guid actorId,
        bool vatCreditable = true)
    {
        ExpenseCreationResult created = await expenses.CreateAsync(
            new SaveExpenseCommand(
                supplierId, accountId, "Compra de prueba de flujo", $"VC-{Guid.NewGuid():N}"[..10],
                issueDate, paymentTerm, netAmount, taxTypeId, vatCreditable, PaymentMethod.Cash),
            actorId);
        Assert.True(created.Operation.IsSuccess, created.Operation.Message);
        Guid expenseId = created.ExpenseId!.Value;

        ExpenseModel draft = (await expenses.GetAsync(expenseId))!;
        ConfirmExpenseCommand confirm = cashAccountId is Guid cash
            ? new ConfirmExpenseCommand(cash, $"G-{Guid.NewGuid():N}"[..10])
            : new ConfirmExpenseCommand(null, null);
        CatalogOperationResult confirmed = await expenses.ConfirmAsync(
            expenseId, confirm, draft.Version, actorId, actorIsManagement: true);
        Assert.True(confirmed.IsSuccess, confirmed.Message);
        return expenseId;
    }

    /// <summary>
    /// Período fiscal fabricado de doce meses que inicia en <paramref name="fiscalStart"/>, con los
    /// meses indicados reabiertos (años sin datos reales: ni el cierre automático ni el rango fiscal
    /// bloquean la prueba).
    /// </summary>
    public static async Task PrepareFabricatedPeriodAsync(
        IServiceProvider services, Guid actorId, DateOnly fiscalStart, params DateOnly[] openMonths)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            parameters = new SystemParameters(10_000_000m, 7, DateTimeOffset.UtcNow, actorId);
            context.SystemParameters.Add(parameters);
        }

        parameters.UpdateFiscalPeriod(fiscalStart, fiscalStart.AddYears(1).AddDays(-1), DateTimeOffset.UtcNow, actorId);

        foreach (var month in openMonths.Select(date => (date.Year, date.Month)).Distinct())
        {
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                month.Year, month.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
        }

        await context.SaveChangesAsync();
    }

    public static async Task<Guid> CreateLedgerAccountAsync(
        IServiceProvider services, Guid actorId, LedgerAccountType type, CashAccountKind? cashKind, string name)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(), $"CF-{suffix}", name, type, null, cashKind, null, null, DateTimeOffset.UtcNow, actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    public static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId, decimal rate)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(Guid.NewGuid(), $"IVA-{suffix}", $"IVA {rate}% de flujo", rate, "08", null, DateTimeOffset.UtcNow, actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    public static async Task<Guid> CreateWithholdingTypeAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        WithholdingType type = new(Guid.NewGuid(), $"RET-{suffix}", "Retención 2 % de flujo", 2m, null, null, DateTimeOffset.UtcNow, actorId);
        context.WithholdingTypes.Add(type);
        await context.SaveChangesAsync();
        return type.Id;
    }

    public static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId, bool appliesStateWithholding)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de prueba de flujo",
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

    public static async Task<Guid> CreateSupplierAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Supplier supplier = new(
            Guid.NewGuid(),
            $"PRV-{suffix}",
            "Proveedor de prueba de flujo",
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

    public static async Task<Guid> CreateCatalogItemAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        CatalogItem item = new(
            Guid.NewGuid(),
            $"SRV-{suffix}",
            "Servicio de prueba de flujo",
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

    public static async Task<Guid> CreateUserAsync(IServiceProvider services)
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
}
