using Microsoft.AspNetCore.Identity;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Usuario interno autenticado por ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// Los usuarios se desactivan de forma lógica para conservar la trazabilidad de sus operaciones.
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Nombre que se mostrará en la interfaz y en la bitácora.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Indica si el usuario puede iniciar sesión.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Indica que la contraseña vigente es temporal y debe cambiarse antes de operar.
    /// </summary>
    public bool MustChangePassword { get; set; }
}
