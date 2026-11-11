using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Parameters;

/// <summary>
/// Paquete v9, bloque 1: la fecha de negocio sale de la hora local del <c>TimeProvider</c>, no de UTC.
/// El reloj marca las 23:30 del último día del mes en Costa Rica, cuando la fecha UTC ya es el día 1 del
/// mes siguiente. Se habilitan con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1 y siempre revierten los datos.
/// </summary>
public sealed class SqlServerLocalBusinessDateScenarioTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerLocalBusinessDateScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task CashInvoiceConfirmedAtNight_IncomeKeepsTheInvoiceDateAndMonth()
    {
        return RunAtLastDayOfMonthNightAsync(async (services, actorId, lastDay) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid incomeAccountId = await CreateLedgerAccountAsync(
                services, actorId, LedgerAccountType.Income, null, "Ingreso de pruebas de fecha local");
            Guid cashAccountId = await CreateLedgerAccountAsync(
                services, actorId, LedgerAccountType.Asset, CashAccountKind.Cash, "Caja de pruebas de fecha local");

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId, lastDay, PaymentTerm.Cash, 100m);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

            CatalogOperationResult confirmation = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(cashAccountId, PaymentMethod.Cash, "NOCHE-001"),
                draft.Version,
                actorId);
            Assert.True(confirmation.IsSuccess, confirmation.Message);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Income income = await context.Incomes.SingleAsync(candidate => candidate.InvoiceId == invoiceId);

            Assert.Equal(lastDay, income.IncomeDate);
            Assert.Equal(lastDay.Month, income.IncomeDate.Month);
        });
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public Task ReceivableDueToday_IsNotOverdueAtNight()
    {
        return RunAtLastDayOfMonthNightAsync(async (services, actorId, lastDay) =>
        {
            IInvoiceService invoices = services.GetRequiredService<IInvoiceService>();
            IReceivableService receivables = services.GetRequiredService<IReceivableService>();
            Guid customerId = await CreateCustomerAsync(services, actorId);
            Guid taxTypeId = await CreateTaxTypeAsync(services, actorId);
            Guid incomeAccountId = await CreateLedgerAccountAsync(
                services, actorId, LedgerAccountType.Income, null, "Ingreso de pruebas de vencimiento local");

            // Crédito 30 días emitida 30 días antes: vence justo hoy (fecha local).
            DateOnly issueDate = lastDay.AddDays(-30);
            await ReopenMonthAsync(services, actorId, issueDate);

            OverdueSummary before = await receivables.GetOverdueSummaryAsync(7);

            Guid invoiceId = await CreateInvoiceWithLineAsync(
                services, invoices, customerId, incomeAccountId, taxTypeId, actorId, issueDate, PaymentTerm.Credit30, 250m);
            InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;
            CatalogOperationResult confirmation = await invoices.ConfirmAsync(
                invoiceId,
                new ConfirmInvoiceCommand(null, null, null),
                draft.Version,
                actorId);
            Assert.True(confirmation.IsSuccess, confirmation.Message);

            FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
            Assert.Equal(lastDay, (await context.Receivables.SingleAsync(r => r.InvoiceId == invoiceId)).DueDate);

            OverdueSummary after = await receivables.GetOverdueSummaryAsync(7);

            Assert.Equal(before.OverdueCount, after.OverdueCount);
            Assert.Equal(before.OverdueAmount, after.OverdueAmount);
            Assert.Equal(before.UpcomingCount + 1, after.UpcomingCount);
            Assert.Equal(before.UpcomingAmount + 250m, after.UpcomingAmount);
        });
    }

    /// <summary>
    /// Ejecuta el escenario con un reloj fijo a las 23:30 (hora de Costa Rica) del último día del mes
    /// local en curso, dentro de una transacción que siempre se revierte.
    /// </summary>
    private async Task RunAtLastDayOfMonthNightAsync(Func<IServiceProvider, Guid, DateOnly, Task> scenario)
    {
        if (!string.Equals(
            Environment.GetEnvironmentVariable(EnableVariable),
            "1",
            StringComparison.Ordinal))
        {
            return;
        }

        DateTime realLocalNow = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, FixedLocalTimeProvider.CostaRicaTimeZone).DateTime;
        DateOnly lastDay = new(
            realLocalNow.Year,
            realLocalNow.Month,
            DateTime.DaysInMonth(realLocalNow.Year, realLocalNow.Month));
        FixedLocalTimeProvider clock = FixedLocalTimeProvider.AtCostaRicaLocalTime(
            lastDay.ToDateTime(new TimeOnly(23, 30)));

        // Comprobación del propio escenario: a esa hora la fecha UTC ya es la del mes siguiente.
        Assert.Equal(lastDay.AddDays(1), DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));

        await using WebApplicationFactory<Program> clockedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(clock)));

        using IServiceScope scope = clockedFactory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await context.Database.BeginTransactionAsync();
        Guid actorId = await CreateUserAsync(scope.ServiceProvider);

        try
        {
            await scenario(scope.ServiceProvider, actorId, lastDay);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static async Task<Guid> CreateInvoiceWithLineAsync(
        IServiceProvider services,
        IInvoiceService invoices,
        Guid customerId,
        Guid incomeAccountId,
        Guid taxTypeId,
        Guid actorId,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        decimal amount)
    {
        InvoiceCreationResult created = await invoices.CreateAsync(
            new CreateInvoiceCommand(customerId, incomeAccountId, issueDate, paymentTerm),
            actorId);
        Assert.True(created.Operation.IsSuccess, created.Operation.Message);
        Guid invoiceId = created.InvoiceId!.Value;
        InvoiceModel draft = (await invoices.GetAsync(invoiceId))!;

        Guid catalogItemId = await CreateCatalogItemAsync(services, actorId);
        CatalogOperationResult line = await invoices.AddLineAsync(
            invoiceId,
            new InvoiceLineInputCommand(catalogItemId, 1m, amount, 0m, taxTypeId),
            draft.Version,
            actorId);
        Assert.True(line.IsSuccess, line.Message);

        return invoiceId;
    }

    private static async Task<Guid> CreateLedgerAccountAsync(
        IServiceProvider services,
        Guid actorId,
        LedgerAccountType type,
        CashAccountKind? cashKind,
        string name)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        LedgerAccount account = new(
            Guid.NewGuid(), $"TZ-{suffix}", name, type, null, cashKind, null, null, DateTimeOffset.UtcNow, actorId);
        context.LedgerAccounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    private static async Task<Guid> CreateCustomerAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        Customer customer = new(
            Guid.NewGuid(),
            $"CLI-{suffix}",
            "Cliente de pruebas de fecha local",
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
        TaxType taxType = new(Guid.NewGuid(), $"IVA-{suffix}", "IVA 0%", 0m, "01", null, DateTimeOffset.UtcNow, actorId);
        context.TaxTypes.Add(taxType);
        await context.SaveChangesAsync();
        return taxType.Id;
    }

    private static async Task<Guid> CreateCatalogItemAsync(IServiceProvider services, Guid actorId)
    {
        FinancialDbContext context = services.GetRequiredService<FinancialDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        CatalogItem item = new(
            Guid.NewGuid(),
            $"SRV-{suffix}",
            "Servicio de pruebas de fecha local",
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

        bool alreadyReopened = await context.AccountingPeriodOverrides
            .AnyAsync(period => period.Year == date.Year && period.Month == date.Month);

        if (!alreadyReopened)
        {
            context.AccountingPeriodOverrides.Add(new AccountingPeriodOverride(
                date.Year, date.Month, "Reapertura para la prueba", DateTimeOffset.UtcNow, actorId));
            await context.SaveChangesAsync();
        }
    }
}
