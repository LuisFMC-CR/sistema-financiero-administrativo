using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Audit;

/// <summary>
/// Verifica, sin tocar SQL Server, que la bitácora de operaciones (pantalla nueva en Consultas) exige
/// la política <c>Auditoria.Ver</c>, restringida a Gerencia: el perfil Administrador no tiene acceso a
/// información financiera, así que ya no entra en esta política (antes la compartía sin pantalla
/// propia que la usara).
/// </summary>
public sealed class OperationAuditAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string RoutePrefix = "auditoria/bitacora-operaciones";
    private readonly FinancialWebApplicationFactory factory;

    public OperationAuditAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void ViewAuditTrailPolicy_AllowsManagementOnly()
    {
        AuthorizationOptions options = factory.Services
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value;
        AuthorizationPolicy policy = options.GetPolicy(SystemPolicies.ViewAuditTrail)!;
        RolesAuthorizationRequirement requirement = Assert.Single(
            policy.Requirements.OfType<RolesAuthorizationRequirement>());

        Assert.Equal([SystemRoles.Management], requirement.AllowedRoles);
    }

    [Fact]
    public async Task OperationAuditRoute_WhenAnonymous_RedirectsToLogin()
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
    public void OperationAuditEndpoints_RequireTheViewAuditTrailPolicy()
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
                    SystemPolicies.ViewAuditTrail,
                    StringComparison.Ordinal)));
    }
}
