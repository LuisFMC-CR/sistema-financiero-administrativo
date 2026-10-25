using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Payables;

/// <summary>
/// Pago aplicado a una cuenta por pagar (regla de negocio 2). Espejo de <c>ReceivablePayment</c>, del
/// lado de ingresos, pero sin retención: "los pagos a proveedores no llevan retención" (regla 3).
/// </summary>
/// <remarks>
/// Un pago a varias cuentas por pagar crea una fila **por cuenta**: todas comparten
/// <see cref="GroupId"/>, <see cref="CashAccountId"/> y <see cref="Reference"/>, pero cada una se
/// anula de forma individual (regla 2) sin afectar a las demás del mismo grupo.
///
/// Conexiones: la crea <c>SupplierPaymentService.RegisterAsync</c>, en la misma transacción que las
/// demás filas del grupo. A diferencia de <c>ReceivablePayment</c>, no genera ningún <c>Income</c> ni
/// documento equivalente: el sistema no modela un "egreso" aparte. Se guarda en
/// <c>finanzas.Pagos</c> vía <c>SupplierPaymentConfiguration</c>.
/// </remarks>
public sealed class SupplierPayment
{
    /// <summary>Longitud máxima de la referencia.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima del motivo de anulación.</summary>
    public const int ReasonMaxLength = 300;

    private SupplierPayment()
    {
    }

    /// <summary>Crea un pago vigente sobre una cuenta por pagar.</summary>
    public SupplierPayment(
        Guid id,
        Guid payableId,
        Guid groupId,
        decimal amount,
        Guid cashAccountId,
        string reference,
        DateOnly paymentDate,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        PayableId = DomainRules.RequiredId(payableId, nameof(payableId));
        GroupId = DomainRules.RequiredId(groupId, nameof(groupId));
        Amount = ValidatePositiveAmount(amount, nameof(amount));
        CashAccountId = DomainRules.RequiredId(cashAccountId, nameof(cashAccountId));
        Reference = DomainRules.RequiredText(reference, ReferenceMaxLength, nameof(reference));
        PaymentDate = RequireDate(paymentDate, nameof(paymentDate));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = SupplierPaymentStatus.Active;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la cuenta por pagar sobre la que se aplica.</summary>
    public Guid PayableId { get; private set; }

    /// <summary>
    /// Obtiene el identificador que comparten todas las filas creadas por un mismo pago a varias
    /// cuentas por pagar.
    /// </summary>
    public Guid GroupId { get; private set; }

    /// <summary>Obtiene el monto aplicado a esta cuenta por pagar.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene la cuenta de efectivo que entregó el pago.</summary>
    public Guid CashAccountId { get; private set; }

    /// <summary>Obtiene la referencia del pago.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Obtiene la fecha de negocio del pago.</summary>
    public DateOnly PaymentDate { get; private set; }

    /// <summary>Obtiene el estado actual.</summary>
    public SupplierPaymentStatus Status { get; private set; }

    /// <summary>Obtiene el motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Obtiene el instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que anuló el pago, cuando corresponde.</summary>
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
    /// Anula este pago. Quien llama es responsable de restituir el saldo de la cuenta por pagar en la
    /// misma transacción (regla 4); no afecta a los demás pagos del mismo <see cref="GroupId"/>.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != SupplierPaymentStatus.Active)
        {
            throw new InvalidOperationException("El pago ya está anulado.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, el pago sigue vigente.
        string validReason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        Status = SupplierPaymentStatus.Cancelled;
    }

    private static decimal ValidatePositiveAmount(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        if (rounded <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "El monto debe ser mayor que cero.");
        }

        return rounded;
    }

    private static DateOnly RequireDate(DateOnly value, string parameterName) => value != default
        ? value
        : throw new ArgumentException("La fecha del pago es obligatoria.", parameterName);

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
