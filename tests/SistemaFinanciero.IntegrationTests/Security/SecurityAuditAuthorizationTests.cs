using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

/// <summary>
/// Verifica, sin tocar SQL Server, que la bitácora de seguridad (pantalla nueva en el área de
/// Administrador) exige la misma política que la administración de usuarios de la que forma parte.
/// </summary>
public sealed class SecurityAuditAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string RoutePrefix = "administracion/bitacora-seguridad";
    private readonly FinancialWebApplicationFactory factory;

    public SecurityAuditAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void ManageUsersPolicy_AllowsAdministratorOnly()
    {
        AuthorizationOptions options = factory.Services
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value;
        AuthorizationPolicy policy = options.GetPolicy(SystemPolicies.ManageUsers)!;
        RolesAuthorizationRequirement requirement = Assert.Single(
            policy.Requirements.OfType<RolesAuthorizationRequirement>());

        Assert.Equal([SystemRoles.Administrator], requirement.AllowedRoles);
    }

    [Fact]
    public async Task SecurityAuditRoute_WhenAnonymous_RedirectsToLogin()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync($"/{RoutePrefix}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void SecurityAuditEndpoints_RequireTheManageUsersPolicy()
    {
        RouteEndpoint[] endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty)
                .StartsWith(RoutePrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(
            endpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => string.Equals(
                    data.Policy,
                    SystemPolicies.ManageUsers,
                    StringComparison.Ordinal)));
    }
}
