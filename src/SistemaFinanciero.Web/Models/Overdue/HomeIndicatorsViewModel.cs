using SistemaFinanciero.Application.Security.Audit;

namespace SistemaFinanciero.Web.Models.Overdue;

/// <summary>
/// Indicadores de inicio: vencimientos para los perfiles con acceso a CxC/CxP, presupuestos
/// excedidos del mes actual solo para Gerencia y Finanzas, y los últimos registros de la bitácora de
/// seguridad solo para el Administrador (ADR-0060).
/// </summary>
public sealed record HomeIndicatorsViewModel(
    OverdueIndicatorViewModel? Overdue,
    HomeBudgetIndicatorViewModel? Budget,
    IReadOnlyList<SecurityAuditEntryModel>? RecentSecurityEvents = null);
