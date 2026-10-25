using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Budgets;

/// <summary>
/// Registro inmutable de un cambio en el monto presupuestado de una categoría y un mes: conserva el
/// monto anterior y el nuevo, quién lo hizo y cuándo (regla de negocio 6).
/// </summary>
/// <remarks>
/// El monto anterior es nulo únicamente en el registro que documenta la primera configuración del
/// presupuesto de esa categoría y ese mes.
///
/// Conexiones: la crea <c>BudgetService.SetAmountAsync</c> (Infrastructure/Budgets), nunca el
/// controlador directamente; se guarda en <c>finanzas.PresupuestoCambios</c> vía
/// <c>BudgetChangeConfiguration</c>. <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c> impide
/// modificarla o borrarla, igual criterio que <c>SystemParameterChange</c>.
/// </remarks>
public sealed class BudgetChange
{
    private BudgetChange()
    {
    }

    /// <summary>Crea un registro de cambio. No admite modificaciones posteriores.</summary>
    public BudgetChange(
        Guid id,
        Guid budgetId,
        decimal? previousAmount,
        decimal newAmount,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        BudgetId = DomainRules.RequiredId(budgetId, nameof(budgetId));
        PreviousAmount = ValidateOptionalAmount(previousAmount, nameof(previousAmount));
        NewAmount = ValidateAmount(newAmount, nameof(newAmount));
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    /// <summary>Obtiene el identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el presupuesto al que pertenece este cambio.</summary>
    public Guid BudgetId { get; private set; }

    /// <summary>Obtiene el monto anterior; nulo si este registro documenta la primera configuración.</summary>
    public decimal? PreviousAmount { get; private set; }

    /// <summary>Obtiene el monto nuevo.</summary>
    public decimal NewAmount { get; private set; }

    /// <summary>Obtiene el instante UTC del cambio.</summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que realizó el cambio.</summary>
    public Guid ChangedByUserId { get; private set; }

    private static decimal? ValidateOptionalAmount(decimal? value, string parameterName) =>
        value is null ? null : ValidateAmount(value.Value, parameterName);

    private static decimal ValidateAmount(decimal value, string parameterName) =>
        value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto no puede ser negativo.");
}
