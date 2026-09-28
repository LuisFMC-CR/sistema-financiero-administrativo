using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class CatalogAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public CatalogAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void ViewCatalogsPolicy_AllowsManagementFinanceAndAssistantOnly()
    {
        AssertPolicyRoles(
            SystemPolicies.ViewBusinessCatalogs,
            SystemRoles.Management,
            SystemRoles.Finance,
            SystemRoles.Assistant);
    }

    [Fact]
    public void ManageContactsPolicy_AllowsFinanceAndAssistantOnly()
    {
        AssertPolicyRoles(
            SystemPolicies.ManageBusinessContacts,
            SystemRoles.Finance,
            SystemRoles.Assistant);
    }

    [Fact]
    public void ManageFinancialCatalogsPolicy_AllowsFinanceOnly()
    {
        AssertPolicyRoles(SystemPolicies.ManageBusinessCatalogs, SystemRoles.Finance);
    }

    [Theory]
    [InlineData("/catalogos/clientes")]
    [InlineData("/catalogos/proveedores")]
    [InlineData("/catalogos/productos-servicios")]
    [InlineData("/catalogos/categorias-financieras")]
    [InlineData("/catalogos/tipos-cambio")]
    [InlineData("/catalogos/tipos-impuesto")]
    [InlineData("/catalogos/cuentas-contables")]
    [InlineData("/catalogos/tipos-retencion")]
    [InlineData("/parametros")]
    public async Task CatalogRoutes_WhenAnonymous_RedirectToLogin(string route)
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("/catalogos/productos-servicios/crear")]
    [InlineData("/catalogos/tipos-cambio/crear")]
    [InlineData("/catalogos/tipos-impuesto/crear")]
    [InlineData("/catalogos/cuentas-contables/crear")]
    [InlineData("/catalogos/categorias-financieras/crear")]
    [InlineData("/catalogos/tipos-retencion/crear")]
    public async Task FinanceUser_CanOpenFinancialCatalogCreationPages(string route)
    {
        using WebApplicationFactory<Program> financeFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialCategoryService>();
                services.AddScoped<IFinancialCategoryService, EmptyFinancialCategoryService>();
                services.RemoveAll<ILedgerAccountService>();
                services.AddScoped<ILedgerAccountService, EmptyLedgerAccountService>();
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = FinanceAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = FinanceAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = FinanceAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, FinanceAuthenticationHandler>(
                        FinanceAuthenticationHandler.SchemeName,
                        _ => { });
            }));
        using HttpClient client = financeFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync(route);

        string responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
    }

    [Fact]
    public void CatalogEndpoints_DoNotExposePhysicalDeleteRoutes()
    {
        IEnumerable<Microsoft.AspNetCore.Routing.EndpointDataSource> dataSources = factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>();

        string[] routes = dataSources
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .Where(route => route.StartsWith("catalogos/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(routes);
        Assert.DoesNotContain(
            routes,
            route => route.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                route.Contains("eliminar", StringComparison.OrdinalIgnoreCase) ||
                route.Contains("borrar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TaxTypeEndpoints_OnlyFinanceCanChangeDataAndLoadingReferencesIsPostOnly()
    {
        Microsoft.AspNetCore.Routing.RouteEndpoint[] endpoints = factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "catalogos/tipos-impuesto",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Microsoft.AspNetCore.Routing.RouteEndpoint[] mutating = endpoints
            .Where(endpoint => endpoint.Metadata
                .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!
                .HttpMethods.Contains("POST"))
            .ToArray();
        Microsoft.AspNetCore.Routing.RouteEndpoint[] readOnly = endpoints.Except(mutating)
            .Where(endpoint => !endpoint.RoutePattern.RawText!.Contains("crear", StringComparison.Ordinal)
                && !endpoint.RoutePattern.RawText.Contains("editar", StringComparison.Ordinal))
            .ToArray();

        // Crear, editar, cambiar estado y cargar referencias son POST y exigen la política de Finanzas.
        Assert.Contains(mutating, endpoint => endpoint.RoutePattern.RawText!.EndsWith("cargar-referencia", StringComparison.Ordinal));
        Assert.All(
            mutating,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ManageBusinessCatalogs));

        // La carga de referencias no se puede disparar con GET.
        Microsoft.AspNetCore.Routing.RouteEndpoint load = Assert.Single(
            endpoints,
            endpoint => endpoint.RoutePattern.RawText!.EndsWith("cargar-referencia", StringComparison.Ordinal));
        Assert.Equal(
            ["POST"],
            load.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!.HttpMethods);

        // El listado es de consulta: no exige la política de mantenimiento.
        Assert.NotEmpty(readOnly);
        Assert.All(
            readOnly,
            endpoint => Assert.DoesNotContain(
                endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ManageBusinessCatalogs));
    }

    [Fact]
    public void ManageSystemParametersPolicy_AllowsManagementOnly()
    {
        AssertPolicyRoles(SystemPolicies.ManageSystemParameters, SystemRoles.Management);
    }

    [Fact]
    public void SystemParametersEndpoints_OnlyManagementCanSave()
    {
        Microsoft.AspNetCore.Routing.RouteEndpoint[] endpoints = factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText == "parametros")
            .ToArray();

        Microsoft.AspNetCore.Routing.RouteEndpoint save = Assert.Single(
            endpoints,
            endpoint => endpoint.Metadata
                .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!
                .HttpMethods.Contains("POST"));
        Assert.Contains(
            save.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            data => data.Policy == SystemPolicies.ManageSystemParameters);

        Microsoft.AspNetCore.Routing.RouteEndpoint index = Assert.Single(
            endpoints,
            endpoint => endpoint.Metadata
                .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!
                .HttpMethods.Contains("GET"));
        Assert.DoesNotContain(
            index.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            data => data.Policy == SystemPolicies.ManageSystemParameters);
        Assert.Contains(
            index.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            data => data.Policy == SystemPolicies.ViewFinancialReports);
    }

    [Fact]
    public void LedgerAccountEndpoints_OnlyFinanceCanChangeData()
    {
        Microsoft.AspNetCore.Routing.RouteEndpoint[] endpoints = factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "catalogos/cuentas-contables",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Microsoft.AspNetCore.Routing.RouteEndpoint[] mutating = endpoints
            .Where(endpoint => endpoint.Metadata
                .GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()!
                .HttpMethods.Contains("POST"))
            .ToArray();
        Microsoft.AspNetCore.Routing.RouteEndpoint[] getEndpoints = endpoints.Except(mutating).ToArray();

        // Crear, editar y cambiar estado son POST y exigen la política de Finanzas.
        Assert.Equal(3, mutating.Length);
        Assert.All(
            mutating,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ManageBusinessCatalogs));

        // Las pantallas de crear y editar también son solo de Finanzas; el listado es de consulta.
        Microsoft.AspNetCore.Routing.RouteEndpoint list = Assert.Single(
            getEndpoints,
            endpoint => endpoint.RoutePattern.RawText == "catalogos/cuentas-contables");
        Assert.DoesNotContain(
            list.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
            data => data.Policy == SystemPolicies.ManageBusinessCatalogs);
        Assert.All(
            getEndpoints.Where(endpoint => endpoint != list),
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ManageBusinessCatalogs));
    }

    private void AssertPolicyRoles(string policyName, params string[] expectedRoles)
    {
        AuthorizationOptions options = factory.Services
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value;
        AuthorizationPolicy policy = options.GetPolicy(policyName)!;
        RolesAuthorizationRequirement requirement = Assert.Single(
            policy.Requirements.OfType<RolesAuthorizationRequirement>());

        Assert.Equal(
            expectedRoles.Order(StringComparer.Ordinal),
            requirement.AllowedRoles.Order(StringComparer.Ordinal));
    }

    private sealed class FinanceAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "FinanceTest";

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

    /// <summary>
    /// Igual que la de categorías: la pantalla de creación solo necesita listas vacías de cuentas
    /// superiores, sin consultar SQL Server.
    /// </summary>
    private sealed class EmptyLedgerAccountService : ILedgerAccountService
    {
        public Task<PagedResult<LedgerAccountModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromException<PagedResult<LedgerAccountModel>>(new NotSupportedException());

        public Task<LedgerAccountModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromException<LedgerAccountModel?>(new NotSupportedException());

        public Task<IReadOnlyList<LedgerAccountOption>> GetParentOptionsAsync(
            LedgerAccountType type,
            Guid? excludedAccountId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LedgerAccountOption>>([]);

        public Task<IReadOnlyList<LedgerAccountOption>> GetActiveOptionsAsync(
            LedgerAccountType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LedgerAccountOption>>([]);

        public Task<IReadOnlyList<CashAccountOption>> GetActiveCashAccountsAsync(
            SistemaFinanciero.Domain.Currencies.CurrencyCode? currency = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CashAccountOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            CreateLedgerAccountCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            UpdateLedgerAccountCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> SetActiveAsync(
            Guid id,
            bool isActive,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());
    }

    /// <summary>
    /// Evita que esta prueba de autorización dependa de datos de SQL Server; las acciones GET
    /// solo necesitan una lista vacía válida para construir el selector de categorías.
    /// </summary>
    private sealed class EmptyFinancialCategoryService : IFinancialCategoryService
    {
        public Task<PagedResult<FinancialCategoryModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromException<PagedResult<FinancialCategoryModel>>(new NotSupportedException());

        public Task<FinancialCategoryModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromException<FinancialCategoryModel?>(new NotSupportedException());

        public Task<IReadOnlyList<FinancialCategoryOption>> GetActiveOptionsAsync(
            FinancialCategoryKind kind,
            Guid? excludedCategoryId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinancialCategoryOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            SaveFinancialCategoryCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            SaveFinancialCategoryCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> SetActiveAsync(
            Guid id,
            bool isActive,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());
    }
}
