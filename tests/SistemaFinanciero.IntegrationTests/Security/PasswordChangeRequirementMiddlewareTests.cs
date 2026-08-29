using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class PasswordChangeRequirementMiddlewareTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("not-a-boolean")]
    public async Task InvokeAsync_WithoutATrueRequirement_ContinuesThePipeline(string? claimValue)
    {
        bool nextCalled = false;
        PasswordChangeRequirementMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = AuthenticatedContext(claimValue);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.NotEqual(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WithPendingPasswordChange_RedirectsBeforeProtectedEndpoint()
    {
        bool nextCalled = false;
        PasswordChangeRequirementMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = AuthenticatedContext(bool.TrueString);
        context.Request.Path = "/catalogos/clientes";

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/cuenta/cambiar-contrasena", context.Response.Headers.Location);
    }

    [Fact]
    public async Task InvokeAsync_AllowsAnExplicitPasswordChangeEndpoint()
    {
        bool nextCalled = false;
        PasswordChangeRequirementMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = AuthenticatedContext(bool.TrueString);
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowBeforePasswordChangeAttribute()),
            "cambio permitido"));

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AllowsAnonymousEndpoints()
    {
        bool nextCalled = false;
        PasswordChangeRequirementMiddleware middleware = new(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        DefaultHttpContext context = AuthenticatedContext(bool.TrueString);
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "recurso anónimo"));

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AnyTrueClaim_KeepsTheRestrictionEnabled()
    {
        PasswordChangeRequirementMiddleware middleware = new(_ => Task.CompletedTask);
        DefaultHttpContext context = AuthenticatedContext(bool.FalseString);
        ((ClaimsIdentity)context.User.Identity!).AddClaim(
            new Claim(SystemClaimTypes.MustChangePassword, bool.TrueString));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
    }

    private static DefaultHttpContext AuthenticatedContext(string? claimValue)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())];
        if (claimValue is not null)
        {
            claims.Add(new Claim(SystemClaimTypes.MustChangePassword, claimValue));
        }

        ClaimsIdentity identity = new(claims, authenticationType: "Test");
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}

public sealed class PasswordChangeEndpointMetadataTests
    : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public PasswordChangeEndpointMetadataTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("cuenta/cambiar-contrasena")]
    [InlineData("cuenta/cerrar-sesion")]
    public void RequiredAccountRoutes_AreAllowedBeforeChangingTemporaryPassword(string route)
    {
        RouteEndpoint[] endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => string.Equals(
                endpoint.RoutePattern.RawText,
                route,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(
            endpoints,
            endpoint => Assert.NotNull(
                endpoint.Metadata.GetMetadata<AllowBeforePasswordChangeAttribute>()));
    }
}
