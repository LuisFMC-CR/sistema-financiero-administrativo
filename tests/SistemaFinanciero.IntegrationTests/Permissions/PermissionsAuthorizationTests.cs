using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace SistemaFinanciero.IntegrationTests.Permissions;

/// <summary>
/// Verifica, sin tocar SQL Server, que la pantalla de perfiles y permisos exige una sesión iniciada
/// pero ningún permiso adicional: cualquier perfil autenticado la ve (regla de perfiles, "pantalla de
/// solo consulta con esta matriz").
/// </summary>
public sealed class PermissionsAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public PermissionsAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task PermissionsRoute_WhenAnonymous_RedirectsToLogin()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync("/seguridad/perfiles-y-permisos");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void PermissionsEndpoint_HasNoAdditionalPolicy_RelyingOnlyOnTheGlobalAuthenticatedUserFallback()
    {
        RouteEndpoint endpoint = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "seguridad/perfiles-y-permisos");

        Assert.Empty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
    }
}
