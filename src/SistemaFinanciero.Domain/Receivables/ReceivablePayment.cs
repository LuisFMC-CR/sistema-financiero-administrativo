using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Receivables;

/// <summary>
/// Abono aplicado a una cuenta por cobrar (regla de negocio 2).
/// </summary>
/// <remarks>
/// Un abono a varias facturas crea una fila **por factura**: todas comparten <see cref="GroupId"/>,
/// <see cref="CashAccountId"/> y <see cref="Reference"/>, pero cada una se anula de forma individual
/// (regla 2) sin afectar a las demás del mismo grupo.
///
/// Conexiones: la crea <c>ReceivablePaymentService.RegisterAsync</c> junto con su
/// <see cref="Receivables.Withholding"/> opcional (FK única por <see cref="Id"/>) y su
/// <c>Income</c> (origen <c>IncomeOrigin.ReceivablePayment</c>), todo en la misma transacción. Se
/// guarda en <c>finanzas.Abonos</c> vía <c>ReceivablePaymentConfiguration</c>.
/// </remarks>
public sealed class ReceivablePayment
{
    /// <summary>Longitud máxima de la referencia.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima del motivo de anulación.</summary>
    public const int ReasonMaxLength = 300;

    private ReceivablePayment()
    {
    }

    /// <summary>Crea un abono vigente sobre una cuenta por cobrar.</summary>
    public ReceivablePayment(
        Guid id,
        Guid receivableId,
        Guid groupId,
        decimal amount,
        Guid cashAccountId,
        string reference,
        DateOnly paymentDate,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        ReceivableId = DomainRules.RequiredId(receivableId, nameof(receivableId));
        GroupId = DomainRules.RequiredId(groupId, nameof(groupId));
        Amount = ValidatePositiveAmount(amount, nameof(amount));
        CashAccountId = DomainRules.RequiredId(cashAccountId, nameof(cashAccountId));
        Reference = DomainRules.RequiredText(reference, ReferenceMaxLength, nameof(reference));
        PaymentDate = RequireDate(paymentDate, nameof(paymentDate));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = ReceivablePaymentStatus.Active;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la cuenta por cobrar sobre la que se aplica.</summary>
    public Guid ReceivableId { get; private set; }

    /// <summary>
    /// Obtiene el identificador que comparten todas las filas creadas por un mismo abono a varias
    /// facturas.
    /// </summary>
    public Guid GroupId { get; private set; }

    /// <summary>Obtiene el monto aplicado a esta factura (sin incluir la retención, si la hay).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene la cuenta de efectivo que recibió el abono.</summary>
    public Guid CashAccountId { get; private set; }

    /// <summary>Obtiene la referencia del abono.</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Obtiene la fecha de negocio del abono.</summary>
    public DateOnly PaymentDate { get; private set; }

    /// <summary>Obtiene el estado actual.</summary>
    public ReceivablePaymentStatus Status { get; private set; }

    /// <summary>Obtiene el motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Obtiene el instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que anuló el abono, cuando corresponde.</summary>
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
    /// Anula este abono. Quien llama es responsable de restituir el saldo de la cuenta por cobrar y
    /// de anular el ingreso que generó, en la misma transacción (regla 4); no afecta a los demás
    /// abonos del mismo <see cref="GroupId"/>.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != ReceivablePaymentStatus.Active)
        {
            throw new InvalidOperationException("El abono ya está anulado.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, el abono sigue vigente.
        string validReason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        Status = ReceivablePaymentStatus.Cancelled;
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
        : throw new ArgumentException("La fecha del abono es obligatoria.", parameterName);

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
