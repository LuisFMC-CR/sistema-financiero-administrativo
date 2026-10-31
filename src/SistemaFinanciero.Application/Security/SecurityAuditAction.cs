namespace SistemaFinanciero.Application.Security;

/// <summary>Acciones administrativas admitidas en la bitácora de seguridad.</summary>
/// <remarks>
/// Conexiones: es el valor de <c>SecurityAuditEvent.Action</c> (Infrastructure/Identity). Lo escriben
/// <c>IdentityBootstrapper</c>, <c>UserAdministrationService</c> y <c>UserAccountService</c> cada vez
/// que hacen una acción sensible sobre una cuenta. La bitácora de parámetros financieros
/// (<c>SystemParameterChange</c>) es independiente y no usa este enum.
/// </remarks>
public enum SecurityAuditAction
{
    /// <summary>El bootstrap creó el primer administrador.</summary>
    BootstrapAdministratorCreated = 1,

    /// <summary>Un administrador creó una cuenta.</summary>
    UserCreated = 2,

    /// <summary>Se modificó el nombre o correo de una cuenta.</summary>
    UserProfileUpdated = 3,

    /// <summary>Se cambió el rol único de una cuenta.</summary>
    UserRoleChanged = 4,

    /// <summary>Se reactivó una cuenta.</summary>
    UserActivated = 5,

    /// <summary>Se desactivó una cuenta.</summary>
    UserDeactivated = 6,

    /// <summary>Un administrador asignó una contraseña temporal.</summary>
    PasswordReset = 7,

    /// <summary>Un administrador eliminó un bloqueo temporal.</summary>
    UserUnlocked = 8,

    /// <summary>El propietario cambió su contraseña.</summary>
    PasswordChanged = 9,

    /// <summary>Un usuario inició sesión correctamente (RF-77).</summary>
    LoginSucceeded = 10,

    /// <summary>Un intento de inicio de sesión fue rechazado por credenciales incorrectas (RF-77).</summary>
    LoginFailed = 11,

    /// <summary>La cuenta quedó bloqueada temporalmente tras agotar los intentos permitidos (RF-77).</summary>
    AccountLockedOut = 12,
}
