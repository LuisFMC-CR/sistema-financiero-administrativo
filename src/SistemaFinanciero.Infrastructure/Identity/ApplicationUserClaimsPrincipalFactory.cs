using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Incorpora al principal autenticado el estado de contraseña temporal y su nombre completo.
/// </summary>
/// <remarks>
/// Conexiones: Identity la invoca automáticamente al construir la cookie de sesión (registrada vía
/// <c>.AddClaimsPrincipalFactory&lt;ApplicationUserClaimsPrincipalFactory&gt;()</c> en
/// <c>DependencyInjection</c>). <c>SystemClaimTypes.MustChangePassword</c> lo leen
/// <c>PasswordChangeRequirementMiddleware</c> y <c>_Layout.cshtml</c> (Web);
/// <c>SystemClaimTypes.FullName</c> lo lee <c>_Layout.cshtml</c> para mostrar el nombre de la persona
/// en vez del nombre de la aplicación, sin otra consulta a la base de datos.
/// </remarks>
internal sealed class ApplicationUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(
        userManager,
        roleManager,
        optionsAccessor)
{
    /// <inheritdoc />
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        ClaimsIdentity identity = await base.GenerateClaimsAsync(user);

        identity.AddClaim(new Claim(SystemClaimTypes.FullName, user.FullName));

        if (user.MustChangePassword)
        {
            identity.AddClaim(new Claim(
                SystemClaimTypes.MustChangePassword,
                bool.TrueString,
                ClaimValueTypes.Boolean));
        }

        return identity;
    }
}
