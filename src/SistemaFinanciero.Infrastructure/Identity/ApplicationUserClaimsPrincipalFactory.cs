using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Incorpora al principal autenticado el estado de contraseña temporal.
/// </summary>
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
