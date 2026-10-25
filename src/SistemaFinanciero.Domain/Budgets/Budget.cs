using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Budgets;

/// <summary>
/// Presupuesto mensual de una categoría de gasto de último nivel (regla de negocio 6). Las categorías
/// superiores solo suman: no tienen su propia fila.
/// </summary>
/// <remarks>
/// Categoría, año y mes son inmutables: corrigen un error de captura mediante otra fila, nunca
/// reasignando esta. El monto 0 es válido (no hay nada presupuestado para ese mes). Lo ejecutado no se
/// guarda aquí: se calcula en caliente a partir de los gastos confirmados de la categoría y el mes
/// (regla 6), para que anular un gasto después de confirmado lo saque de inmediato sin tocar esta fila.
///
/// Conexiones: la crea y edita <c>BudgetService.SetAmountAsync</c> (Infrastructure/Budgets), que
/// también registra cada cambio en <see cref="BudgetChange"/>. Se guarda en
/// <c>finanzas.Presupuestos</c> vía <c>BudgetConfiguration</c>, con índice único por
/// (CategoryId, PeriodYear, PeriodMonth).
/// </remarks>
public sealed class Budget
{
    /// <summary>Mes mínimo admitido.</summary>
    public const int MinMonth = 1;

    /// <summary>Mes máximo admitido.</summary>
    public const int MaxMonth = 12;

    private Budget()
    {
    }

    /// <summary>Crea la primera configuración del presupuesto de una categoría y un mes.</summary>
    public Budget(
        Guid id,
        Guid categoryId,
        int periodYear,
        int periodMonth,
        decimal amount,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        CategoryId = DomainRules.RequiredId(categoryId, nameof(categoryId));
        PeriodYear = RequireValidYear(periodYear, nameof(periodYear));
        PeriodMonth = RequireValidMonth(periodMonth, nameof(periodMonth));
        Amount = RequireNonNegativeAmount(amount, nameof(amount));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la categoría de gasto de último nivel presupuestada.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Obtiene el año del periodo presupuestado.</summary>
    public int PeriodYear { get; private set; }

    /// <summary>Obtiene el mes del periodo presupuestado (1 a 12).</summary>
    public int PeriodMonth { get; private set; }

    /// <summary>Obtiene el monto presupuestado; 0 es un valor válido.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que configuró el presupuesto por primera vez.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario responsable del último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Obtiene el token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Cambia el monto presupuestado. Quien llama es responsable de registrar el cambio en
    /// <see cref="BudgetChange"/> con el monto anterior, en la misma operación (regla 6).
    /// </summary>
    public void ChangeAmount(decimal newAmount, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        decimal validAmount = RequireNonNegativeAmount(newAmount, nameof(newAmount));
        Guid validUserId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        DateTimeOffset validTimestamp = DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);

        Amount = validAmount;
        UpdatedAtUtc = validTimestamp;
        UpdatedByUserId = validUserId;
    }

    private static int RequireValidYear(int value, string parameterName) => value >= 2000 && value <= 2200
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "El año del periodo no es válido.");

    private static int RequireValidMonth(int value, string parameterName) => value is >= MinMonth and <= MaxMonth
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "El mes del periodo debe estar entre 1 y 12.");

    private static decimal RequireNonNegativeAmount(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        return rounded >= 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto presupuestado no puede ser negativo.");
    }
}
