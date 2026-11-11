using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Receivables;

/// <summary>
/// Escenarios reales de <c>IReceivableService.GetOverdueSummaryAsync</c> (regla 9) que se habilitan
/// con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten datos.
/// </summary>
public sealed class SqlServerReceivableServiceScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerReceivableServiceScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetOverdueSummaryAsync_ClassifiesOverdueAndUpcomingSeparately()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            // Aislamiento (paquete v9, bloque 9): el resumen cuenta todas las cuentas abiertas del sistema.
            await SqlTestIsolation.HideExistingOpenReceivablesAndPayablesAsync(services);
            IReceivableService receivables = services.GetRequiredService<IReceivableService>();
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);

            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

            // -35 y -25 días pueden caer en un mes anterior al actual, cerrado automáticamente
            // (regla 16); se reabren ambos meses antes de crear las facturas.
            await ReopenMonthAsync(services, actorId, today.AddDays(-35));
            await ReopenMonthAsync(services, actorId, today.AddDays(-25));

            // Crédito 30 días emitida hace 35 días: vence hace 5 días (vencida).
            await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, taxTypeId, incomeAccountId, actorId, today.AddDays(-35), 100m);

            // Crédito 30 días emitida hace 25 días: vence en 5 días (próxima, dentro de 7 días).
            await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, taxTypeId, incomeAccountId, actorId, today.AddDays(-25), 200m);

            // Crédito 30 días emitida hoy: vence en 30 días (ni vencida ni próxima con alerta de 7).
            await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, taxTypeId, incomeAccountId, actorId, today, 300m);

            OverdueSummary summary = await receivables.GetOverdueSummaryAsync(7);

            Assert.Equal(1, summary.OverdueCount);
            Assert.Equal(100m, summary.OverdueAmount);
            Assert.Equal(1, summary.UpcomingCount);
            Assert.Equal(200m, summary.UpcomingAmount);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task GetAsync_ExposesTheInvoiceElectronicKeyGeneratedAtConfirm()
    {
        return RunInRollbackTransactionAsync(async (services, actorId) =>
        {
            IReceivableService receivables = services.GetRequiredService<IReceivableService>();
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid incomeAccountId = await CreateIncomeCategoryAsync(services, actorId);
            DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

            Guid receivableId = await CreateConfirmedCreditReceivableAsync(
                services, invoices, customerId, taxTypeId, incomeAccountId, actorId, today, 500m);

            ReceivableModel receivable = (await receivables.GetAsync(receivableId))!;
            InvoiceModel invoice = (await invoices.GetAsync(receivable.InvoiceId))!;

            Assert.Equal(invoice.ElectronicInvoiceKey, receivable.InvoiceElectronicKey);
            Assert.NotNull(receivable.InvoiceElectronicKey);
            Assert.Equal(50, receivable.InvoiceElectronicKey!.Length);
        });
    }

    private static async Task<Guid> CreateConfirmedCreditReceivableAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid taxTypeId,
        Guid incomeAccountId,
        Guid actorId,
        DateOnly issueDate,
        decimal amount)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeAccountId, issueDate, PaymentTerm.Credit30),
            actorId);
        Assert.True(created.Operation.IsSuccess, created.Operation.Message);
        Guid invoiceId = created.InvoiceId!.Value;

        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId, "Servicio a crédito");
        await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, amount, 0m, taxTypeId),
            draft.Version,
            actorId);

        draft = (await invoices.GetAsync(invoiceId))!;
        await invoices.ConfirmAsync(
            invoiceId,
            new ConfirmInvoiceCommand(null, null, null),
            draft.Version,
            actorId);

        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        return (await context.Receivables.SingleAsync(r => r.InvoiceId == invoiceId)).Id;
    }

    private static async Task<Guid> CreateIncomeCategoryAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        LedgerAccount ledgerAccount = new(
            Guid.NewGuid(), $"ING-{suffix}", "Cuenta de ingreso de pruebas de cuentas vencidas",
            LedgerAccountType.Income, null, null, null, null, now, actorId);
        context.LedgerAccounts.Add(ledgerAccount);
        await context.SaveChangesAsync();

        return ledgerAccount.Id;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de vencidas",
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

    private static async Task<Guid> CreateTaxTypeAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        TaxType taxType = new(
            Guid.NewGuid(),
            $"IVA-{suffix}",
            "IVA 0%",
            0m,
            "01",
            null,
            DateTimeOffset.UtcNow,
            actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
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

    private static async Task ReopenMonthAsync(IServiceProvider services, Guid actorId, DateOnly date)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        SystemParameters? parameters = await context.SystemParameters.SingleOrDefaultAsync();

        if (parameters is null)
        {
            context.SystemParameters.Add(new SystemParameters(500_000m, 7, DateTimeOffset.UtcNow, actorId));
        }

        bool alreadyReopened = await context.AccountingPeriodOverrides
            .AnyAsync(period => period.Year == date.Year && period.Month == date.Month);

        if (!alreadyReopened)
        {
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                date.Year, date.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
        }

        await context.SaveChangesAsync();
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
