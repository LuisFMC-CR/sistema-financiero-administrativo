using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.CashTransfers;

/// <summary>
/// Traspaso de dinero entre dos cuentas de efectivo propias: depositar la caja chica en el banco,
/// retirar del banco para reponer la caja, o pasar dinero entre dos cuentas bancarias (paquete v8,
/// bloque 1). No es un ingreso ni un gasto: no toca cuentas de ingreso o gasto, presupuesto ni estado
/// de resultados.
/// </summary>
/// <remarks>
/// Conexiones: lo crea <c>CashTransferService.RegisterAsync</c>, en la misma transacción que su asiento
/// contable (débito destino / crédito origen) y su entrada en la bitácora. Se guarda en
/// <c>finanzas.Traspasos</c> vía <c>CashTransferConfiguration</c>. Espejo de <c>SupplierPayment</c> en
/// su forma (sin agrupación: un traspaso siempre es una sola fila).
/// </remarks>
public sealed class CashTransfer
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima de la referencia.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Longitud máxima del motivo de anulación.</summary>
    public const int ReasonMaxLength = 300;

    private CashTransfer()
    {
    }

    /// <summary>Crea un traspaso vigente entre dos cuentas de efectivo distintas.</summary>
    public CashTransfer(
        Guid id,
        string code,
        Guid sourceCashAccountId,
        Guid destinationCashAccountId,
        decimal amount,
        string reference,
        string description,
        DateOnly transferDate,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        SourceCashAccountId = DomainRules.RequiredId(sourceCashAccountId, nameof(sourceCashAccountId));
        DestinationCashAccountId = DomainRules.RequiredId(destinationCashAccountId, nameof(destinationCashAccountId));

        if (SourceCashAccountId == DestinationCashAccountId)
        {
            throw new ArgumentException(
                "La cuenta de origen y la de destino deben ser distintas.",
                nameof(destinationCashAccountId));
        }

        Amount = ValidatePositiveAmount(amount, nameof(amount));
        Reference = DomainRules.RequiredText(reference, ReferenceMaxLength, nameof(reference));
        Description = DomainRules.RequiredText(description, DescriptionMaxLength, nameof(description));
        TransferDate = RequireDate(transferDate, nameof(transferDate));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = CashTransferStatus.Active;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código automático asignado al crear (regla 12, <c>TRA-000001</c>); no es editable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene la cuenta de efectivo de origen.</summary>
    public Guid SourceCashAccountId { get; private set; }

    /// <summary>Obtiene la cuenta de efectivo de destino.</summary>
    public Guid DestinationCashAccountId { get; private set; }

    /// <summary>Obtiene el monto traspasado.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene la referencia (número de transferencia, depósito o comprobante de caja).</summary>
    public string Reference { get; private set; } = string.Empty;

    /// <summary>Obtiene la descripción del traspaso.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Obtiene la fecha de negocio del traspaso.</summary>
    public DateOnly TransferDate { get; private set; }

    /// <summary>Obtiene el estado actual.</summary>
    public CashTransferStatus Status { get; private set; }

    /// <summary>Obtiene el motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Obtiene el instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que anuló el traspaso, cuando corresponde.</summary>
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
    /// Anula este traspaso. Quien llama es responsable de generar la reversión del asiento contable en
    /// la misma transacción (regla de negocio del bloque 1, paquete v8). Solo Gerencia.
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != CashTransferStatus.Active)
        {
            throw new InvalidOperationException("El traspaso ya está anulado.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, el traspaso sigue vigente.
        string validReason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        Status = CashTransferStatus.Cancelled;
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
        : throw new ArgumentException("La fecha del traspaso es obligatoria.", parameterName);

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
