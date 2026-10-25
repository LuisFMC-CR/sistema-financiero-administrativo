namespace SistemaFinanciero.Application.Security;

/// <summary>
/// Nombres estables de las políticas de autorización evaluadas en el servidor.
/// </summary>
/// <remarks>
/// Conexiones: son solo nombres; <c>AuthorizationExtensions.AddSystemAuthorization</c> (Web) asocia
/// cada uno a los roles de <see cref="SystemRoles"/> que lo cumplen. Cada controlador los usa en
/// <c>[Authorize(Policy = ...)]</c>, y las vistas los consultan con
/// <c>AuthorizationService.AuthorizeAsync</c> para ocultar botones. La autorización real siempre ocurre
/// en el servidor; ocultar un botón es solo una ayuda visual.
/// </remarks>
public static class SystemPolicies
{
    public const string ManageUsers = "Usuarios.Administrar";
    public const string ManageTechnicalConfiguration = "Configuracion.Administrar";
    public const string ViewBusinessCatalogs = "Catalogos.Ver";
    public const string ManageBusinessContacts = "Terceros.Administrar";
    public const string ManageBusinessCatalogs = "Catalogos.Administrar";
    public const string ViewFinancialTransactions = "Movimientos.Ver";
    public const string CreateFinancialDrafts = "Movimientos.CrearBorrador";
    public const string ConfirmFinancialTransactions = "Movimientos.Confirmar";
    public const string VoidFinancialTransactions = "Movimientos.Anular";
    public const string AdjustFinancialBalances = "Movimientos.Ajustar";
    public const string ViewFinancialReports = "Reportes.Ver";
    public const string ViewAuditTrail = "Auditoria.Ver";
    public const string ManageSystemParameters = "Parametros.Administrar";
}
