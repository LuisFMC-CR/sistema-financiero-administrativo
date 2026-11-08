using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// Pide por HTTP, como un usuario de Finanzas, las pantallas de los catálogos nuevos con datos reales
/// para detectar errores de Razor que solo aparecen al renderizar. Solo lee: no modifica la base de
/// datos. Se habilita con SISTEMA_FINANCIERO_RUN_SQL_TESTS=1.
/// </summary>
public sealed class SqlServerCatalogPagesTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerCatalogPagesTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [Trait("Category", "SqlServer")]
    [InlineData("/catalogos/cuentas-contables", "Cuentas contables")]
    [InlineData("/catalogos/cuentas-contables?status=All", "Cuentas contables")]
    [InlineData("/catalogos/cuentas-contables/crear", "Nueva cuenta contable")]
    [InlineData("/catalogos/categorias-financieras", "Categorías financieras")]
    [InlineData("/catalogos/categorias-financieras?status=All", "Categorías financieras")]
    [InlineData("/catalogos/categorias-financieras/crear", "Nueva categoría financiera")]
    [InlineData("/impuestos-y-retenciones", "Impuestos y retenciones")]
    [InlineData("/impuestos-y-retenciones?taxStatus=All&withholdingStatus=All", "Impuestos y retenciones")]
    public async Task FinanceUser_CanRenderTheListAndCreationPages(string route, string expectedTitle)
    {
        if (!IsEnabled())
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();

        using HttpResponseMessage response = await client.GetAsync(route);
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains(expectedTitle, body);
    }

    [Theory]
    [Trait("Category", "SqlServer")]
    [InlineData("/catalogos/tipos-impuesto", "/impuestos-y-retenciones")]
    [InlineData("/catalogos/tipos-retencion", "/impuestos-y-retenciones")]
    public async Task FinanceUser_OldTaxAndWithholdingRoutesRedirectToTheUnifiedScreen(string route, string expectedLocation)
    {
        if (!IsEnabled())
        {
            return;
        }

        using WebApplicationFactory<Program> financeFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = FinanceAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = FinanceAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = FinanceAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, FinanceAuthenticationHandler>(
                        FinanceAuthenticationHandler.SchemeName,
                        _ => { })));
        using HttpClient client = financeFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
    }

    [Theory]
    [Trait("Category", "SqlServer")]
    [InlineData("/impuestos-y-retenciones/tarifas/crear", "Nueva tarifa de IVA")]
    [InlineData("/impuestos-y-retenciones/retenciones/crear", "Nuevo tipo de retención")]
    public async Task ManagementUser_CanRenderTheTaxAndWithholdingCreationPages(string route, string expectedTitle)
    {
        if (!IsEnabled())
        {
            return;
        }

        using HttpClient client = CreateManagementClient();

        using HttpResponseMessage response = await client.GetAsync(route);
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains(expectedTitle, body);
    }

    [Theory]
    [Trait("Category", "SqlServer")]
    [InlineData("/impuestos-y-retenciones/tarifas/crear")]
    [InlineData("/impuestos-y-retenciones/retenciones/crear")]
    public async Task FinanceUser_CannotOpenTheTaxAndWithholdingCreationPages(string route)
    {
        if (!IsEnabled())
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task FinanceUser_SeesTheMigratedCashAccountsInTheLedgerList()
    {
        if (!IsEnabled())
        {
            return;
        }

        LedgerAccount? migrated = await FindAnyAsync<LedgerAccount>(account => account.CashKind != null);
        if (migrated is null)
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();

        using HttpResponseMessage response = await client.GetAsync("/catalogos/cuentas-contables?status=All");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(migrated.Code), body);
        Assert.Contains("Activo", body);
        Assert.Contains(migrated.CashKind == CashAccountKind.Bank ? "Cuenta bancaria" : "Caja", body);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task FinanceUser_DeactivationFormPostsAnExplicitFalseValue()
    {
        if (!IsEnabled())
        {
            return;
        }

        LedgerAccountModel? account;
        using (IServiceScope scope = factory.Services.CreateScope())
        {
            ILedgerAccountService service = scope.ServiceProvider.GetRequiredService<ILedgerAccountService>();
            account = (await service.SearchAsync(new LedgerAccountQuery(isActive: true)))
                .Items
                .FirstOrDefault(candidate => candidate.CanDeactivate);
        }

        if (account is null)
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();
        using HttpResponseMessage response = await client.GetAsync(
            $"/catalogos/cuentas-contables?code={Uri.EscapeDataString(account.Code)}");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("name=\"IsActive\" value=\"false\"", body);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task FinanceUser_CanRenderTheEditPageOfACashLedgerAccount()
    {
        if (!IsEnabled())
        {
            return;
        }

        LedgerAccount? account = await FindAnyAsync<LedgerAccount>(candidate => candidate.CashKind != null);
        if (account is null)
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();

        using HttpResponseMessage response = await client.GetAsync($"/catalogos/cuentas-contables/editar/{account.Id}");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Editar cuenta contable", body);
        Assert.Contains("Subtipo de efectivo", body);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(account.Code), body);
        Assert.DoesNotContain("ledger-account-is-cash", body);
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task FinanceUser_CanRenderTheEditPageOfATaxType()
    {
        if (!IsEnabled())
        {
            return;
        }

        TaxType? taxType = await FindAnyAsync<TaxType>(_ => true);
        if (taxType is null)
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();

        using HttpResponseMessage response = await client.GetAsync($"/impuestos-y-retenciones/tarifas/{taxType.Id}/editar");
        string body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Contains("Editar tarifa de IVA", body);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(taxType.Code), body);
    }

    [Theory]
    [Trait("Category", "SqlServer")]
    [InlineData("/catalogos/cuentas-contables/editar/00000000-0000-0000-0000-000000000001")]
    [InlineData("/impuestos-y-retenciones/tarifas/00000000-0000-0000-0000-000000000001/editar")]
    public async Task EditPages_ForAMissingRecord_ReturnNotFound(string route)
    {
        if (!IsEnabled())
        {
            return;
        }

        using HttpClient client = CreateFinanceClient();

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    private static bool IsEnabled() =>
        string.Equals(Environment.GetEnvironmentVariable(EnableVariable), "1", StringComparison.Ordinal);

    private async Task<TEntity?> FindAnyAsync<TEntity>(System.Linq.Expressions.Expression<Func<TEntity, bool>> predicate)
        where TEntity : class
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();

        return await context.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(predicate);
    }

    private HttpClient CreateFinanceClient()
    {
        WebApplicationFactory<Program> financeFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = FinanceAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = FinanceAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = FinanceAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, FinanceAuthenticationHandler>(
                        FinanceAuthenticationHandler.SchemeName,
                        _ => { })));

        return financeFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    private HttpClient CreateManagementClient()
    {
        WebApplicationFactory<Program> managementFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = ManagementAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = ManagementAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = ManagementAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, ManagementAuthenticationHandler>(
                        ManagementAuthenticationHandler.SchemeName,
                        _ => { })));

        return managementFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    private sealed class FinanceAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "FinancePagesTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Name, "Usuario de Finanzas"),
                    new Claim(ClaimTypes.Role, SystemRoles.Finance),
                ],
                SchemeName);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }

    private sealed class ManagementAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ManagementPagesTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Name, "Usuario de Gerencia"),
                    new Claim(ClaimTypes.Role, SystemRoles.Management),
                ],
                SchemeName);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
