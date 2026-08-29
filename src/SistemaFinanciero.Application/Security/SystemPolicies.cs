namespace SistemaFinanciero.Application.Security;

/// <summary>
/// Nombres estables de las políticas de autorización evaluadas en el servidor.
/// </summary>
public static class SystemPolicies
{
    public const string ManageUsers = "Usuarios.Administrar";
    public const string ManageTechnicalConfiguration = "Configuracion.Administrar";
    public const string ViewBusinessCatalogs = "Catalogos.Ver";
    public const string ManageBusinessContacts = "Terceros.Administrar";
    public const string ManageBusinessCatalogs = "Catalogos.Administrar";
    public const string CreateFinancialDrafts = "Movimientos.CrearBorrador";
    public const string ConfirmFinancialTransactions = "Movimientos.Confirmar";
    public const string VoidFinancialTransactions = "Movimientos.Anular";
    public const string ViewFinancialReports = "Reportes.Ver";
    public const string ViewAuditTrail = "Auditoria.Ver";
}
