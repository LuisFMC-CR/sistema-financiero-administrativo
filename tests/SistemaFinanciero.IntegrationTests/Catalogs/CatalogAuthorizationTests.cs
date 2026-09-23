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
    [InlineData("/catalogos/cuentas-financieras")]
    [InlineData("/catalogos/tipos-cambio")]
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
    public async Task FinanceUser_CanOpenFinancialCatalogCreationPages(string route)
    {
        using WebApplicationFactory<Program> financeFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFinancialCategoryService>();
                services.AddScoped<IFinancialCategoryService, EmptyFinancialCategoryService>();
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
