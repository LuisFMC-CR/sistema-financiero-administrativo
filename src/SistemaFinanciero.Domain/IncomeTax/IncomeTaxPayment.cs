using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.IncomeTax;

/// <summary>
/// Pago de impuesto sobre la renta hecho a Hacienda (RF-99): un pago parcial o el pago de la
/// declaración de un período fiscal. Sale de una cuenta de efectivo y se resta en la estimación del
/// período al que se aplica. No es un gasto: no toca categorías, presupuesto ni estado de resultados.
/// </summary>
/// <remarks>
/// El período fiscal se guarda explícitamente porque el pago de la declaración se hace después de que
/// el período terminó: su fecha cae en el período siguiente, pero se resta en el que declara.
///
/// Conexiones: lo crea <c>IncomeTaxPaymentService.RegisterAsync</c>, en la misma transacción que su
/// asiento (débito "Impuesto sobre la renta pagado por anticipado" / crédito efectivo) y su entrada en
/// la bitácora. Se guarda en <c>finanzas.PagosImpuestoRenta</c>. Espejo de <c>CashTransfer</c>.
/// </remarks>
public sealed class IncomeTaxPayment
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima de la referencia.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Longitud máxima del motivo de anulación.</summary>
    public const int ReasonMaxLength = 300;

    private IncomeTaxPayment()
    {
    }

    /// <summary>Crea un pago vigente.</summary>
    public IncomeTaxPayment(
        Guid id,
        string code,
        DateOnly fiscalPeriodStartDate,
        DateOnly fiscalPeriodEndDate,
        Guid cashAccountId,
        PaymentMethod paymentMethod,
        decimal amount,
        string reference,
        string? description,
        DateOnly paymentDate,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        if (fiscalPeriodStartDate.Year >= DateOnly.MaxValue.Year
            || fiscalPeriodStartDate.Day != 1
            || fiscalPeriodEndDate != fiscalPeriodStartDate.AddYears(1).AddDays(-1))
        {
            throw new ArgumentException(
                "El período fiscal debe iniciar el primer día de un mes y abarcar exactamente doce meses.",
                nameof(fiscalPeriodEndDate));
        }

        if (paymentDate == default)
        {
            throw new ArgumentException("La fecha del pago es obligatoria.", nameof(paymentDate));
        }

        if (paymentDate < fiscalPeriodStartDate)
        {
            throw new ArgumentException(
                "La fecha del pago no puede ser anterior al inicio del período fiscal al que se aplica.",
                nameof(paymentDate));
        }

        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        FiscalPeriodStartDate = fiscalPeriodStartDate;
        FiscalPeriodEndDate = fiscalPeriodEndDate;
        CashAccountId = DomainRules.RequiredId(cashAccountId, nameof(cashAccountId));
        PaymentMethod = DomainRules.DefinedEnum(paymentMethod, nameof(paymentMethod));
        Amount = ValidatePositiveAmount(amount, nameof(amount));
        Reference = DomainRules.RequiredText(reference, ReferenceMaxLength, nameof(reference));
        Description = DomainRules.OptionalText(description, DescriptionMaxLength, nameof(description));
        PaymentDate = paymentDate;
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = IncomeTaxPaymentStatus.Active;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código automático asignado al crear (regla 12, <c>PIR-000001</c>); no es editable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Primer día del período fiscal al que se aplica el pago.</summary>
    public DateOnly FiscalPeriodStartDate { get; private set; }

    /// <summary>Último día del período fiscal al que se aplica el pago.</summary>
    public DateOnly FiscalPeriodEndDate { get; private set; }

    /// <summary>Cuenta de efectivo de la que salió el dinero.</summary>
    public Guid CashAccountId { get; private set; }

    /// <summary>Medio de pago.</summary>
    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Monto pagado.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Referencia (número de recibo o de transferencia).</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Descripción opcional, por ejemplo "Pago parcial de junio".</summary>
    public string? Description { get; private set; }

    /// <summary>Fecha de negocio del pago.</summary>
    public DateOnly PaymentDate { get; private set; }

    /// <summary>Estado actual.</summary>
    public IncomeTaxPaymentStatus Status { get; private set; }

    /// <summary>Motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Usuario que anuló el pago, cuando corresponde.</summary>
    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Usuario que creó el registro.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Usuario que realizó el último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Anula este pago. Quien llama es responsable de generar la reversión del asiento contable en la
    /// misma transacción. Solo Gerencia.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != IncomeTaxPaymentStatus.Active)
        {
            throw new InvalidOperationException("El pago de impuesto sobre la renta ya está anulado.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, el pago sigue vigente.
        string validReason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(cancelledAtUtc, nameof(cancelledAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(cancelledByUserId, nameof(cancelledByUserId));

        CancellationReason = validReason;
        CancelledAtUtc = timestamp;
        CancelledByUserId = userId;
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
        Status = IncomeTaxPaymentStatus.Cancelled;
    }

    private static decimal ValidatePositiveAmount(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        return rounded > 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto debe ser mayor que cero.");
    }
}
