using Microsoft.AspNetCore.Identity;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Usuario interno autenticado por ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// Los usuarios se desactivan de forma lógica para conservar la trazabilidad de sus operaciones.
///
/// Conexiones: es el <c>TUser</c> de Identity en <c>FinancialDbContext</c> (tabla
/// <c>seguridad.Usuarios</c>); todas las entidades de negocio apuntan a su <see cref="IdentityUser{T}.Id"/>
/// en sus columnas <c>CreatedByUserId</c>/<c>UpdatedByUserId</c>, sin referencia de objeto (viven en
/// proyectos distintos). <c>UserAdministrationService</c> lo administra; <c>UserAccountService</c>
/// cambia su contraseña; <c>ApplicationUserClaimsPrincipalFactory</c> lee
/// <see cref="MustChangePassword"/> al iniciar sesión.
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
