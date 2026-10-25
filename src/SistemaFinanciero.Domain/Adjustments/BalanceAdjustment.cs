using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Adjustments;

/// <summary>
/// Registro inmutable de un ajuste manual de saldo sobre una cuenta por cobrar o por pagar (regla de
/// negocio 4). Exactamente una de <see cref="ReceivableId"/> o <see cref="PayableId"/> está presente.
/// </summary>
/// <remarks>
/// No se modifica ni se anula: un ajuste registrado por error se corrige con otro en sentido
/// contrario (regla 4). No genera ingresos ni pagos ni toca efectivo, documento o presupuesto; solo
/// cambia el saldo de la cuenta por cobrar o por pagar correspondiente, en la misma transacción que
/// esta fila.
///
/// Conexiones: la crea <c>BalanceAdjustmentService</c> (Infrastructure/Adjustments) junto con
/// <c>Receivable.Adjust</c>/<c>Payable.Adjust</c> y su entrada en la bitácora de operaciones
/// (<see cref="SistemaFinanciero.Domain.Audit.OperationLogEntry"/>); se guarda en
/// <c>finanzas.AjustesSaldo</c> vía <c>BalanceAdjustmentConfiguration</c>.
/// </remarks>
public sealed class BalanceAdjustment
{
    /// <summary>Longitud máxima del motivo.</summary>
    public const int ReasonMaxLength = 300;

    private BalanceAdjustment()
    {
    }

    /// <summary>Crea el ajuste de una cuenta por cobrar.</summary>
    public static BalanceAdjustment ForReceivable(
        Guid id,
        Guid receivableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId) => new(
            id,
            DomainRules.RequiredId(receivableId, nameof(receivableId)),
            payableId: null,
            amount,
            direction,
            reason,
            createdAtUtc,
            createdByUserId);

    /// <summary>Crea el ajuste de una cuenta por pagar.</summary>
    public static BalanceAdjustment ForPayable(
        Guid id,
        Guid payableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId) => new(
            id,
            receivableId: null,
            DomainRules.RequiredId(payableId, nameof(payableId)),
            amount,
            direction,
            reason,
            createdAtUtc,
            createdByUserId);

    private BalanceAdjustment(
        Guid id,
        Guid? receivableId,
        Guid? payableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        ReceivableId = receivableId;
        PayableId = payableId;
        Amount = ValidatePositiveAmount(amount, nameof(amount));
        Direction = DomainRules.DefinedEnum(direction, nameof(direction));
        Reason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
    }

    /// <summary>Obtiene el identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la cuenta por cobrar ajustada, si el ajuste es de ese lado.</summary>
    public Guid? ReceivableId { get; private set; }

    /// <summary>Obtiene la cuenta por pagar ajustada, si el ajuste es de ese lado.</summary>
    public Guid? PayableId { get; private set; }

    /// <summary>Obtiene el monto del ajuste, siempre positivo; el sentido lo da <see cref="Direction"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene si el ajuste aumentó o disminuyó el saldo.</summary>
    public BalanceAdjustmentDirection Direction { get; private set; }

    /// <summary>Obtiene el motivo del ajuste.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Obtiene el instante UTC en que se registró.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario de Gerencia que lo registró.</summary>
    public Guid CreatedByUserId { get; private set; }

    private static decimal ValidatePositiveAmount(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        if (rounded <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "El monto debe ser mayor que cero.");
        }

        return rounded;
    }
}
