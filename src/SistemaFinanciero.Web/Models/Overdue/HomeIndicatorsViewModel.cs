using SistemaFinanciero.Application.Budgets;

namespace SistemaFinanciero.Web.Models.Overdue;

/// <summary>
/// Indicadores de inicio para Gerencia y Finanzas: cuentas vencidas (regla 9) y presupuestos
/// excedidos del mes actual (RF-92, bloque 7 del paquete de mejoras finales, punto 10).
/// </summary>
public sealed record HomeIndicatorsViewModel(
    OverdueIndicatorViewModel Overdue,
    BudgetExceededSummary ExceededBudgets,
    int PeriodYear,
    int PeriodMonth);
