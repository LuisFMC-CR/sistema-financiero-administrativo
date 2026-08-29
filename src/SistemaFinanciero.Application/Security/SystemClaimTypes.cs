namespace SistemaFinanciero.Application.Security;

/// <summary>Tipos de claims internos emitidos exclusivamente por la aplicación.</summary>
public static class SystemClaimTypes
{
    /// <summary>Indica que la contraseña temporal debe cambiarse antes de utilizar los módulos.</summary>
    public const string MustChangePassword = "SistemaFinanciero.MustChangePassword";
}
