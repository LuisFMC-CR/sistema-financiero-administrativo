namespace SistemaFinanciero.Application.Security;

/// <summary>Tipos de claims internos emitidos exclusivamente por la aplicación.</summary>
/// <remarks>
/// Conexiones: <c>ApplicationUserClaimsPrincipalFactory</c> (Infrastructure/Identity) agrega este claim
/// a la cookie cuando <c>ApplicationUser.MustChangePassword</c> es verdadero.
/// <c>PasswordChangeRequirementMiddleware</c> y <c>_Layout.cshtml</c> (Web) lo leen para bloquear el
/// resto de la aplicación hasta que la contraseña se cambie.
/// </remarks>
public static class SystemClaimTypes
{
    /// <summary>Indica que la contraseña temporal debe cambiarse antes de utilizar los módulos.</summary>
    public const string MustChangePassword = "SistemaFinanciero.MustChangePassword";

    /// <summary>Nombre completo del usuario, para mostrarlo en la interfaz sin otra consulta.</summary>
    public const string FullName = "SistemaFinanciero.FullName";
}
