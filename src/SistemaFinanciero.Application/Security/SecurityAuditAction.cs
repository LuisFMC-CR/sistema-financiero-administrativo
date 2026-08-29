namespace SistemaFinanciero.Application.Security;

/// <summary>Acciones administrativas admitidas en la bitácora de seguridad.</summary>
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
}
