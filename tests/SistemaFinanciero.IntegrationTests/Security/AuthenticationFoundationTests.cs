using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class AuthenticationFoundationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public AuthenticationFoundationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task ProtectedHome_WhenUserIsAnonymous_RedirectsToLogin()
    {
        using HttpClient client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://localhost"),
            });

        using HttpResponseMessage response = await client.GetAsync("/");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location.AbsolutePath);
    }

    [Fact]
    public async Task LoginPage_IsAvailableWithoutAuthentication()
    {
        using HttpClient client = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
            });

        using HttpResponseMessage response = await client.GetAsync("/cuenta/iniciar-sesion");

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public void IdentityOptions_EnforceApprovedPasswordAndLockoutRules()
    {
        IOptions<IdentityOptions> options = factory.Services.GetRequiredService<IOptions<IdentityOptions>>();

        Assert.Equal(12, options.Value.Password.RequiredLength);
        Assert.Equal(4, options.Value.Password.RequiredUniqueChars);
        Assert.True(options.Value.Password.RequireUppercase);
        Assert.True(options.Value.Password.RequireLowercase);
        Assert.True(options.Value.Password.RequireDigit);
        Assert.True(options.Value.Password.RequireNonAlphanumeric);
        Assert.Equal(5, options.Value.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Value.Lockout.DefaultLockoutTimeSpan);
        Assert.True(options.Value.User.RequireUniqueEmail);
    }

    [Fact]
    public void AuthenticationCookie_IsSecureAndNonPersistentByDesign()
    {
        IOptionsMonitor<CookieAuthenticationOptions> options = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();
        CookieAuthenticationOptions cookie = options.Get(IdentityConstants.ApplicationScheme);

        Assert.Equal("__Host-SistemaFinanciero.Auth", cookie.Cookie.Name);
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(Microsoft.AspNetCore.Http.CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Strict, cookie.Cookie.SameSite);
        Assert.Equal(TimeSpan.FromMinutes(30), cookie.ExpireTimeSpan);
        Assert.True(cookie.SlidingExpiration);
    }

    [Fact]
    public void SecurityStamp_IsValidatedOnEveryAuthenticatedRequest()
    {
        IOptions<SecurityStampValidatorOptions> options = factory.Services
            .GetRequiredService<IOptions<SecurityStampValidatorOptions>>();

        Assert.Equal(TimeSpan.Zero, options.Value.ValidationInterval);
    }

    [Fact]
    public void PublicRegistrationEndpoint_IsNotMapped()
    {
        IEnumerable<Microsoft.AspNetCore.Routing.EndpointDataSource> dataSources = factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>();

        string[] routes = dataSources
            .SelectMany(source => source.Endpoints)
            .OfType<Microsoft.AspNetCore.Routing.RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(
            routes,
            route => route.Contains("Register", StringComparison.OrdinalIgnoreCase));
    }
}
