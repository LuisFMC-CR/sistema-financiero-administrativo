using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Queries;

/// <summary>
/// Verifica, sin tocar SQL Server, que las rutas de <c>QueriesController</c> exigen exactamente la
/// política de perfiles: solo Gerencia y Finanzas entran a Consultas (Asistente "no ve... saldos").
/// Sigue el mismo patrón que <c>BudgetsAuthorizationTests</c>.
/// </summary>
public sealed class QueriesAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public QueriesAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task QueryRoutes_WhenAnonymous_RedirectToLogin()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync("/consultas");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void QueryEndpoints_RequireViewFinancialReports()
    {
        RouteEndpoint[] endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "consultas",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(
            endpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ViewFinancialReports));
    }
}
