namespace SistemaFinanciero.Web.Models.Overdue;

/// <summary>
/// Indicadores de inicio: vencimientos para los perfiles con acceso a CxC/CxP y presupuestos
/// excedidos del mes actual solo para Gerencia y Finanzas.
/// </summary>
public sealed record HomeIndicatorsViewModel(
    OverdueIndicatorViewModel? Overdue,
    HomeBudgetIndicatorViewModel? Budget);
