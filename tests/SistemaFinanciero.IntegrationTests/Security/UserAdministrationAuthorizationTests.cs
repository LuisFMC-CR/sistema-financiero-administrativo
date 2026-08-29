using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class UserAdministrationAuthorizationTests
    : IClassFixture<FinancialWebApplicationFactory>
{
    private const string UserRoutePrefix = "administracion/usuarios";
    private readonly FinancialWebApplicationFactory factory;

    public UserAdministrationAuthorizationTests(FinancialWebApplicationFactory factory)
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

    [Theory]
    [InlineData("/administracion/usuarios")]
    [InlineData("/administracion/usuarios/crear")]
    [InlineData("/administracion/usuarios/editar/00000000-0000-0000-0000-000000000001")]
    [InlineData("/administracion/usuarios/restablecer-contrasena/00000000-0000-0000-0000-000000000001")]
    public async Task UserAdministrationGetRoutes_WhenAnonymous_RedirectToLogin(string route)
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
    public void UserAdministrationEndpoints_RequireTheManageUsersPolicy()
    {
        RouteEndpoint[] endpoints = GetRouteEndpoints()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty)
                .StartsWith(UserRoutePrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(
            endpoints,
            endpoint =>
            {
                Assert.DoesNotContain(endpoint.Metadata, metadata => metadata is IAllowAnonymous);
                Assert.Contains(
                    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                    data => string.Equals(
                        data.Policy,
                        SystemPolicies.ManageUsers,
                        StringComparison.Ordinal));
            });
    }

    [Fact]
    public void UserAdministrationEndpoints_DoNotExposePhysicalDeletion()
    {
        RouteEndpoint[] endpoints = GetRouteEndpoints()
            .Where(endpoint => (endpoint.RoutePattern.RawText ?? string.Empty)
                .StartsWith(UserRoutePrefix, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.DoesNotContain(
            endpoints,
            endpoint =>
            {
                string route = endpoint.RoutePattern.RawText ?? string.Empty;
                IHttpMethodMetadata? methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>();

                return route.Contains("eliminar", StringComparison.OrdinalIgnoreCase) ||
                    route.Contains("borrar", StringComparison.OrdinalIgnoreCase) ||
                    route.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                    methods?.HttpMethods.Contains("DELETE", StringComparer.OrdinalIgnoreCase) == true;
            });
    }

    [Fact]
    public void Endpoints_DoNotOfferRegistrationOrPasswordRecovery()
    {
        string[] routes = GetRouteEndpoints()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            routes,
            route => route.Contains("registr", StringComparison.OrdinalIgnoreCase) ||
                route.Contains("recuper", StringComparison.OrdinalIgnoreCase) ||
                route.Contains("olvid", StringComparison.OrdinalIgnoreCase) ||
                route.Contains("forgot", StringComparison.OrdinalIgnoreCase));
    }

    private RouteEndpoint[] GetRouteEndpoints()
    {
        return factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();
    }
}
