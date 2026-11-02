using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Budgets;

/// <summary>Datos para crear o corregir el monto presupuestado de una categoría y un mes (regla 6).</summary>
public sealed record SetBudgetAmountCommand(
    Guid CategoryId,
    int PeriodYear,
    int PeriodMonth,
    decimal Amount);

/// <summary>
/// Semáforo de ejecución presupuestaria (RF-92, bloque 7 del paquete de mejoras finales, punto 9):
/// verde por debajo del 80 %, amarillo del 80 % al 100 % y rojo por encima del 100 %. El color
/// siempre va acompañado del texto (Normal, Atención, Excedido), nunca solo del color.
/// </summary>
public enum BudgetExecutionStatus
{
    /// <summary>Ejecución por debajo del 80 % de lo presupuestado.</summary>
    Normal = 1,

    /// <summary>Ejecución entre el 80 % y el 100 % de lo presupuestado.</summary>
    Attention = 2,

    /// <summary>Ejecución por encima del 100 % de lo presupuestado.</summary>
    Exceeded = 3,
}

/// <summary>
/// Calcula el semáforo de ejecución (RF-92) a partir de lo presupuestado y lo ejecutado, para que la
/// pantalla de presupuesto y los reportes de presupuesto contra lo ejecutado y anual a la fecha usen
/// siempre el mismo criterio.
/// </summary>
public static class BudgetExecutionStatusCalculator
{
    /// <summary>Umbral de atención (80 %).</summary>
    public const decimal AttentionThreshold = 0.8m;

    /// <summary>
    /// Nulo cuando no hay presupuesto configurado (regla 6: "si no hay presupuesto, se indica y se
    /// permite continuar", sin semáforo). Con presupuesto en 0, cualquier ejecución mayor que cero ya
    /// es Excedido; sin ejecución, es Normal.
    /// </summary>
    public static BudgetExecutionStatus? Compute(decimal? budgetedAmount, decimal executedAmount)
    {
        if (budgetedAmount is not decimal budgeted)
        {
            return null;
        }

        if (budgeted == 0)
        {
            return executedAmount > 0 ? BudgetExecutionStatus.Exceeded : BudgetExecutionStatus.Normal;
        }

        decimal ratio = executedAmount / budgeted;

        if (ratio > 1m)
        {
            return BudgetExecutionStatus.Exceeded;
        }

        return ratio >= AttentionThreshold ? BudgetExecutionStatus.Attention : BudgetExecutionStatus.Normal;
    }
}

/// <summary>
/// Proyección de una categoría de gasto de último nivel para un mes: lo presupuestado (si se
/// configuró), lo ejecutado (siempre calculado) y los indicadores derivados.
/// </summary>
/// <remarks>
/// <see cref="BudgetId"/>, <see cref="BudgetedAmount"/> y <see cref="Version"/> son nulos cuando la
/// categoría no tiene presupuesto configurado para este mes (regla 6: "si no hay presupuesto, se
/// indica y se permite continuar"). <see cref="AvailableAmount"/>, <see cref="VarianceAmount"/> y
/// <see cref="VariancePercent"/> también quedan nulos en ese caso; con presupuesto en 0,
/// <see cref="VariancePercent"/> queda nulo (división por cero) pero <see cref="AvailableAmount"/> es 0.
/// </remarks>
public sealed record BudgetModel(
    Guid? BudgetId,
    Guid CategoryId,
    string CategoryCode,
    string CategoryName,
    int PeriodYear,
    int PeriodMonth,
    decimal? BudgetedAmount,
    decimal ExecutedAmount,
    decimal? AvailableAmount,
    decimal? VarianceAmount,
    decimal? VariancePercent,
    DateTimeOffset? UpdatedAtUtc,
    string? Version)
{
    /// <summary>Semáforo de ejecución (RF-92); nulo cuando no hay presupuesto configurado.</summary>
    public BudgetExecutionStatus? ExecutionStatus => BudgetExecutionStatusCalculator.Compute(BudgetedAmount, ExecutedAmount);
}

/// <summary>
/// Resumen de categorías excedidas de un mes (RF-92, bloque 7 del paquete de mejoras finales, punto
/// 10): cuántas categorías quedaron en rojo (<see cref="BudgetExecutionStatus.Exceeded"/>) y el monto
/// total excedido (la suma de lo ejecutado por encima de lo presupuestado en esas categorías).
/// </summary>
public sealed record BudgetExceededSummary(int ExceededCount, decimal ExceededAmount);

/// <summary>
/// Casos de uso del presupuesto mensual por categoría de gasto de último nivel (regla de negocio 6).
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>BudgetService</c> (Infrastructure/Budgets); lo consume
/// <c>BudgetsController</c> (Web, ruta <c>/presupuesto</c>). La alerta no bloqueante al confirmar un
/// gasto la expone <c>IExpenseService.CheckBudgetAsync</c>, no este servicio.
/// </remarks>
public interface IBudgetService
{
    /// <summary>
    /// Lista todas las categorías de gasto de último nivel activas para un mes, con o sin
    /// presupuesto configurado. Las categorías superiores no aparecen: solo suman (regla 6).
    /// </summary>
    public Task<IReadOnlyList<BudgetModel>> SearchByPeriodAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene el presupuesto de una categoría y un mes, exista o no todavía.</summary>
    public Task<BudgetModel?> GetAsync(
        Guid categoryId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea o corrige el monto presupuestado de una categoría y un mes; cada cambio queda en
    /// <c>BudgetChange</c> con el monto anterior, el nuevo, quién y cuándo (regla 6).
    /// <paramref name="version"/> es vacío al crear la primera configuración, y obligatorio para
    /// corregir una ya existente.
    /// </summary>
    public Task<CatalogOperationResult> SetAmountAsync(
        SetBudgetAmountCommand command,
        string? version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cantidad de categorías excedidas (semáforo en rojo) de un mes y el monto total excedido, para
    /// el indicador de inicio de Gerencia y Finanzas (RF-92, punto 10). El excedente nunca bloquea el
    /// gasto; esto es solo informativo.
    /// </summary>
    public Task<BudgetExceededSummary> GetExceededSummaryAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);
}
