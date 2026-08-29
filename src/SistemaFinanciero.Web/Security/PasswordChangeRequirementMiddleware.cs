using Microsoft.AspNetCore.Authorization;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.Web.Security;

/// <summary>
/// Restringe una sesión con contraseña temporal a los recursos anónimos, el cambio de
/// contraseña propio y el cierre de sesión.
/// </summary>
public sealed class PasswordChangeRequirementMiddleware(RequestDelegate next)
{
    private const string ChangePasswordPath = "/cuenta/cambiar-contrasena";

    /// <summary>Evalúa la obligación emitida en el principal autenticado.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        Endpoint? endpoint = context.GetEndpoint();
        bool allowsAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        bool allowsPendingPasswordChange = endpoint?.Metadata
            .GetMetadata<AllowBeforePasswordChangeAttribute>() is not null;

        if (MustChangePassword(context.User) &&
            !allowsAnonymous &&
            !allowsPendingPasswordChange)
        {
            context.Response.Redirect(ChangePasswordPath);
            return;
        }

        await next(context);
    }

    private static bool MustChangePassword(System.Security.Claims.ClaimsPrincipal user)
    {
        return user.Identity?.IsAuthenticated == true &&
            user.FindAll(SystemClaimTypes.MustChangePassword).Any(claim =>
                bool.TryParse(claim.Value, out bool required) && required);
    }
}
