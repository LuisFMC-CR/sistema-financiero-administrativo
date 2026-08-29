using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

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
}
