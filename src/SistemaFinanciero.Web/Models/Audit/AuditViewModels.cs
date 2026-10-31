using SistemaFinanciero.Application.Audit;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Audit;
using SistemaFinanciero.Domain.Audit;

namespace SistemaFinanciero.Web.Models.Audit;

/// <summary>Datos para presentar una página de la bitácora de seguridad.</summary>
public sealed record SecurityAuditIndexViewModel(PagedResult<SecurityAuditEntryModel> Results);

/// <summary>Datos para presentar una página de la bitácora de operaciones.</summary>
public sealed record OperationAuditIndexViewModel(PagedResult<OperationAuditEntryModel> Results);

/// <summary>Traduce las acciones y los tipos de documento de ambas bitácoras a etiquetas de interfaz.</summary>
public static class AuditDisplayExtensions
{
    /// <summary>Obtiene la etiqueta visible de una acción de la bitácora de seguridad.</summary>
    public static string ToDisplayName(this SecurityAuditAction action)
    {
        return action switch
        {
            SecurityAuditAction.BootstrapAdministratorCreated => "Creación inicial del administrador",
            SecurityAuditAction.UserCreated => "Alta de usuario",
            SecurityAuditAction.UserProfileUpdated => "Actualización de datos",
            SecurityAuditAction.UserRoleChanged => "Cambio de rol",
            SecurityAuditAction.UserActivated => "Reactivación de cuenta",
            SecurityAuditAction.UserDeactivated => "Desactivación de cuenta",
            SecurityAuditAction.PasswordReset => "Restablecimiento de contraseña",
            SecurityAuditAction.UserUnlocked => "Eliminación de bloqueo",
            SecurityAuditAction.PasswordChanged => "Cambio de contraseña propia",
            SecurityAuditAction.LoginSucceeded => "Inicio de sesión correcto",
            SecurityAuditAction.LoginFailed => "Intento de inicio de sesión fallido",
            SecurityAuditAction.AccountLockedOut => "Bloqueo de cuenta por intentos fallidos",
            _ => action.ToString(),
        };
    }

    /// <summary>Obtiene la etiqueta visible de una acción de la bitácora de operaciones.</summary>
    public static string ToDisplayName(this OperationLogAction action)
    {
        return action switch
        {
            OperationLogAction.Confirmed => "Confirmación",
            OperationLogAction.Cancelled => "Anulación",
            OperationLogAction.Adjusted => "Ajuste de saldo",
            _ => action.ToString(),
        };
    }

    /// <summary>Obtiene la etiqueta visible del tipo de documento de la bitácora de operaciones.</summary>
    public static string ToDisplayName(this OperationLogEntityType entityType)
    {
        return entityType switch
        {
            OperationLogEntityType.Invoice => "Factura",
            OperationLogEntityType.Receivable => "Cuenta por cobrar",
            OperationLogEntityType.ReceivablePayment => "Abono",
            OperationLogEntityType.Income => "Ingreso",
            OperationLogEntityType.Expense => "Gasto",
            OperationLogEntityType.Payable => "Cuenta por pagar",
            OperationLogEntityType.SupplierPayment => "Pago a proveedor",
            _ => entityType.ToString(),
        };
    }

    /// <summary>Obtiene la etiqueta visible de un rol, o un guion cuando la acción no lo requirió.</summary>
    public static string ToDisplayRole(this string? role)
    {
        return string.IsNullOrWhiteSpace(role) ? "—" : role;
    }
}
