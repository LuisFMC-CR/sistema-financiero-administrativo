using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SistemaFinanciero.IntegrationTests;

/// <summary>
/// Autenticación de prueba que firma la solicitud con el perfil del encabezado
/// <see cref="RoleHeader"/>, para validar permisos del servidor sin iniciar sesión.
/// </summary>
public sealed class RoleTestAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "RoleTest";
    public const string RoleHeader = "X-Test-Role";

    /// <summary>Registra este esquema como el predeterminado de autenticación, desafío y prohibición.</summary>
    public static void AddTo(IServiceCollection services) =>
        services
            .AddAuthentication(authentication =>
            {
                authentication.DefaultAuthenticateScheme = SchemeName;
                authentication.DefaultChallengeScheme = SchemeName;
                authentication.DefaultForbidScheme = SchemeName;
            })
            .AddScheme<AuthenticationSchemeOptions, RoleTestAuthentication>(SchemeName, _ => { });

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
