using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.IncomeTax;

/// <summary>
/// RF-99: permisos de las pantallas de pagos de renta, de la escala en Parámetros y del reporte de
/// estimación, validados en el servidor. Solo prueba los rechazos, que nunca llegan a la base de datos.
/// </summary>
public sealed class IncomeTaxAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public IncomeTaxAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/pagos-renta")]
    [InlineData("/reportes/estimacion-renta")]
    public async Task Routes_WhenAnonymous_RedirectToLogin(string route)
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("/pagos-renta/nuevo")]
    [InlineData("/reportes/estimacion-renta")]
    public async Task Assistant_CannotRegisterPaymentsOrSeeTheEstimate(string route)
    {
        using HttpClient client = CreateClientFor(SystemRoles.Assistant);

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Finance_CannotCancelAPayment()
    {
        using HttpClient client = CreateClientFor(SystemRoles.Finance);

        using HttpResponseMessage response = await client.GetAsync($"/pagos-renta/{Guid.NewGuid()}/anular");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Finance_CannotChangeTheIncomeTaxScale()
    {
        using HttpClient client = CreateClientFor(SystemRoles.Finance);

        using HttpResponseMessage response = await client.PostAsync(
            "/parametros/escala-renta",
            new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private HttpClient CreateClientFor(string role)
    {
        WebApplicationFactory<Program> roleFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = RoleAuthenticationHandler.SchemeName;
                        options.DefaultChallengeScheme = RoleAuthenticationHandler.SchemeName;
                        options.DefaultForbidScheme = RoleAuthenticationHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, RoleAuthenticationHandler>(
                        RoleAuthenticationHandler.SchemeName,
                        _ => { })));

        HttpClient client = roleFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add(RoleAuthenticationHandler.RoleHeader, role);
        return client;
    }

    /// <summary>Autentica con el perfil que indica el encabezado <see cref="RoleHeader"/>.</summary>
    private sealed class RoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "IncomeTaxRoleTest";
        public const string RoleHeader = "X-Test-Role";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string role = Request.Headers[RoleHeader].ToString();

            ClaimsIdentity identity = new(
                [
                    new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                    new Claim(ClaimTypes.Name, $"Usuario de {role}"),
                    new Claim(ClaimTypes.Role, role),
                ],
                SchemeName);

            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
