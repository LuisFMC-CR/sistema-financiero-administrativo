using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Budgets;

/// <summary>Datos para crear o corregir el monto presupuestado de una categoría y un mes (regla 6).</summary>
public sealed record SetBudgetAmountCommand(
    Guid CategoryId,
    int PeriodYear,
    int PeriodMonth,
    decimal Amount);

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
    string? Version);

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
}
