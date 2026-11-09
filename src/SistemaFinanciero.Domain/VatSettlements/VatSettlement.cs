using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.VatSettlements;

/// <summary>
/// Liquidación mensual del IVA (paquete v8, bloque 2): compensa el IVA por pagar del período con el
/// IVA acreditable disponible y, si queda diferencia, registra el pago a Hacienda o el saldo a favor
/// que se arrastra al siguiente período. Es un registro interno: no sustituye la declaración ante
/// Hacienda.
/// </summary>
/// <remarks>
/// Conexiones: la crea <c>VatSettlementService.RegisterAsync</c>, en la misma transacción que su
/// asiento contable (débito IVA por pagar, crédito IVA acreditable y crédito cuenta de efectivo si
/// hubo pago) y su entrada en la bitácora. Se guarda en <c>finanzas.LiquidacionesIva</c> vía
/// <c>VatSettlementConfiguration</c>, con un índice único filtrado (PeriodYear, PeriodMonth) donde
/// Status = Vigente (una sola liquidación vigente por mes).
/// </remarks>
public sealed class VatSettlement
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima de la referencia del pago.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima del motivo de anulación.</summary>
    public const int ReasonMaxLength = 300;

    private VatSettlement()
    {
    }

    /// <summary>Crea una liquidación vigente para un período.</summary>
    public VatSettlement(
        Guid id,
        string code,
        int periodYear,
        int periodMonth,
        DateOnly settlementDate,
        decimal vatPayableAmount,
        decimal vatCreditApplied,
        decimal paymentAmount,
        decimal creditCarriedForward,
        Guid? cashAccountId,
        string? reference,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        PeriodYear = RequireYear(periodYear, nameof(periodYear));
        PeriodMonth = RequireMonth(periodMonth, nameof(periodMonth));
        SettlementDate = RequireDate(settlementDate, nameof(settlementDate));
        VatPayableAmount = RequireNonNegative(vatPayableAmount, nameof(vatPayableAmount));
        VatCreditApplied = RequireNonNegative(vatCreditApplied, nameof(vatCreditApplied));
        PaymentAmount = RequireNonNegative(paymentAmount, nameof(paymentAmount));
        CreditCarriedForward = RequireNonNegative(creditCarriedForward, nameof(creditCarriedForward));
        CashAccountId = DomainRules.OptionalId(cashAccountId, nameof(cashAccountId));
        Reference = NormalizeOptionalText(reference, ReferenceMaxLength, nameof(reference));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = VatSettlementStatus.Active;

        if (DomainRules.RoundMoney(VatPayableAmount) != DomainRules.RoundMoney(VatCreditApplied + PaymentAmount))
        {
            throw new ArgumentException(
                "El IVA por pagar liquidado debe ser igual al IVA acreditable aplicado más el pago.");
        }

        if (PaymentAmount > 0 && (CashAccountId is null || Reference is null))
        {
            throw new ArgumentException(
                "La cuenta de efectivo y la referencia son obligatorias cuando hay pago a Hacienda.");
        }

        if (PaymentAmount == 0 && (CashAccountId is not null || Reference is not null))
        {
            throw new ArgumentException(
                "No se debe indicar cuenta de efectivo ni referencia cuando no hay pago.");
        }
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código automático asignado al crear (regla 12, <c>LIQ-000001</c>); no es editable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene el año del período liquidado.</summary>
    public int PeriodYear { get; private set; }

    /// <summary>Obtiene el mes del período liquidado.</summary>
    public int PeriodMonth { get; private set; }

    /// <summary>Obtiene la fecha de negocio del pago o de la liquidación.</summary>
    public DateOnly SettlementDate { get; private set; }

    /// <summary>Obtiene el IVA por pagar liquidado (A).</summary>
    public decimal VatPayableAmount { get; private set; }

    /// <summary>Obtiene el IVA acreditable aplicado (B o A, el menor de los dos).</summary>
    public decimal VatCreditApplied { get; private set; }

    /// <summary>Obtiene el monto pagado a Hacienda.</summary>
    public decimal PaymentAmount { get; private set; }

    /// <summary>Obtiene el IVA acreditable que queda para el período siguiente (informativo).</summary>
    public decimal CreditCarriedForward { get; private set; }

    /// <summary>Obtiene la cuenta de efectivo de origen del pago, cuando hubo pago.</summary>
    public Guid? CashAccountId { get; private set; }

    /// <summary>Obtiene la referencia del comprobante de pago, cuando hubo pago.</summary>
    public string? Reference { get; private set; }

    /// <summary>Obtiene el estado actual.</summary>
    public VatSettlementStatus Status { get; private set; }

    /// <summary>Obtiene el motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Obtiene el instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que anuló la liquidación, cuando corresponde.</summary>
    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que creó el registro.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que realizó el último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Obtiene el token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Anula esta liquidación. Quien llama es responsable de generar la reversión del asiento
    /// contable en la misma transacción y de comprobar que es la liquidación vigente más reciente.
    /// Solo Gerencia.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != VatSettlementStatus.Active)
        {
            throw new InvalidOperationException("La liquidación ya está anulada.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, la liquidación sigue vigente.
        string validReason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        Status = VatSettlementStatus.Cancelled;
    }

    private static int RequireYear(int value, string parameterName) => value is >= 2000 and <= 2200
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "El año del período no es válido.");

    private static int RequireMonth(int value, string parameterName) => value is >= 1 and <= 12
        ? value
        : throw new ArgumentOutOfRangeException(parameterName, value, "El mes debe estar entre 1 y 12.");

    private static DateOnly RequireDate(DateOnly value, string parameterName) => value != default
        ? value
        : throw new ArgumentException("La fecha de la liquidación es obligatoria.", parameterName);

    private static decimal RequireNonNegative(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);
        return rounded >= 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto no puede ser negativo.");
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : DomainRules.RequiredText(value, maxLength, parameterName);
    }

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
