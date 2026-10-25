using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Receivables;

/// <summary>
/// Cuenta por cobrar generada al confirmar una factura a crédito (regla de negocio 2).
/// </summary>
/// <remarks>
/// Una factura a crédito tiene como máximo una cuenta por cobrar (<see cref="InvoiceId"/> es único en
/// la base de datos). El saldo lo reduce cada abono (<see cref="ApplyPayment"/>, por el monto pagado
/// más la retención, si la hay) y lo restituye anular uno de ellos (<see cref="RevertPayment"/>).
/// Todavía no admite un ajuste de saldo independiente (regla 4): esa operación, junto con la
/// validación de la fecha de cierre, queda para el paso 6 del roadmap de <c>CLAUDE.md</c>.
///
/// Conexiones: la crea <c>InvoiceService.ConfirmAsync</c> cuando la factura es a crédito, y la anula
/// en cascada <c>InvoiceService.CancelAsync</c> cuando la factura se anula sin abonos vigentes. Se
/// guarda en <c>finanzas.CuentasPorCobrar</c> vía <c>ReceivableConfiguration</c>.
/// <c>ReceivablePaymentService</c> (siguiente bloque del roadmap) la actualiza al registrar o anular
/// un abono.
/// </remarks>
public sealed class Receivable
{
    private Receivable()
    {
    }

    /// <summary>Crea la cuenta por cobrar de una factura recién confirmada a crédito.</summary>
    public Receivable(
        Guid id,
        Guid invoiceId,
        Guid customerId,
        decimal originalAmount,
        DateOnly dueDate,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        InvoiceId = DomainRules.RequiredId(invoiceId, nameof(invoiceId));
        CustomerId = DomainRules.RequiredId(customerId, nameof(customerId));
        OriginalAmount = ValidatePositiveAmount(originalAmount, nameof(originalAmount));
        Balance = OriginalAmount;
        DueDate = RequireDate(dueDate, nameof(dueDate));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = ReceivableStatus.Pending;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la factura a crédito que originó esta cuenta por cobrar.</summary>
    public Guid InvoiceId { get; private set; }

    /// <summary>Obtiene el cliente deudor.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Obtiene el monto original, copiado del total de la factura al confirmarse.</summary>
    public decimal OriginalAmount { get; private set; }

    /// <summary>Obtiene el saldo pendiente. Invariante: 0 ≤ <see cref="Balance"/> ≤ <see cref="OriginalAmount"/>.</summary>
    public decimal Balance { get; private set; }

    /// <summary>Obtiene la fecha de vencimiento, copiada de la factura al confirmarse.</summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Obtiene el estado actual.</summary>
    public ReceivableStatus Status { get; private set; }

    /// <summary>Obtiene el instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que anuló la cuenta por cobrar, cuando corresponde.</summary>
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
    /// Reduce el saldo al registrar un abono vigente, por el monto pagado más la retención aplicada,
    /// si la hay (regla 2/3).
    /// </summary>
    public void ApplyPayment(decimal amount, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        EnsureNotCancelled();
        decimal validAmount = ValidatePositiveAmount(amount, nameof(amount));

        if (validAmount > Balance)
        {
            throw new InvalidOperationException("El abono no puede superar el saldo pendiente.");
        }

        Touch(updatedAtUtc, updatedByUserId);
        Balance = DomainRules.RoundMoney(Balance - validAmount);
        Status = Balance == 0 ? ReceivableStatus.Paid : ReceivableStatus.PartiallyPaid;
    }

    /// <summary>Restituye el saldo al anular un abono vigente (regla 4).</summary>
    public void RevertPayment(decimal amount, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        EnsureNotCancelled();
        decimal validAmount = ValidatePositiveAmount(amount, nameof(amount));
        decimal restoredBalance = DomainRules.RoundMoney(Balance + validAmount);

        if (restoredBalance > OriginalAmount)
        {
            throw new InvalidOperationException("El saldo restituido no puede superar el monto original.");
        }

        Touch(updatedAtUtc, updatedByUserId);
        Balance = restoredBalance;
        Status = Balance == OriginalAmount ? ReceivableStatus.Pending : ReceivableStatus.PartiallyPaid;
    }

    /// <summary>
    /// Ajusta manualmente el saldo, con el motivo ya validado por quien llama (regla 4). No genera
    /// ingresos ni pagos ni toca efectivo, documento o presupuesto: solo cambia <see cref="Balance"/>
    /// y <see cref="Status"/>, respetando la invariante 0 ≤ <see cref="Balance"/> ≤
    /// <see cref="OriginalAmount"/>. Un ajuste registrado por error se corrige con otro en sentido
    /// contrario, nunca editando o anulando este.
    /// </summary>
    public void Adjust(
        decimal amount,
        BalanceAdjustmentDirection direction,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        EnsureNotCancelled();
        decimal validAmount = ValidatePositiveAmount(amount, nameof(amount));
        decimal newBalance = direction == BalanceAdjustmentDirection.Increase
            ? DomainRules.RoundMoney(Balance + validAmount)
            : DomainRules.RoundMoney(Balance - validAmount);

        if (newBalance < 0 || newBalance > OriginalAmount)
        {
            throw new InvalidOperationException(
                "El ajuste dejaría el saldo fuera del rango permitido (entre 0 y el monto original).");
        }

        Touch(updatedAtUtc, updatedByUserId);
        Balance = newBalance;
        Status = Balance == 0
            ? ReceivableStatus.Paid
            : Balance == OriginalAmount
                ? ReceivableStatus.Pending
                : ReceivableStatus.PartiallyPaid;
    }

    /// <summary>
    /// Anula la cuenta por cobrar junto con la factura que la originó. Exige que no tenga abonos
    /// vigentes (regla 4: el saldo debe seguir igual al monto original); si los tiene, hay que
    /// anularlos antes.
    /// </summary>
    public void Cancel(DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        EnsureNotCancelled();

        if (Balance != OriginalAmount)
        {
            throw new InvalidOperationException(
                "No se puede anular una cuenta por cobrar con abonos vigentes; anule antes esos abonos.");
        }

        Touch(cancelledAtUtc, cancelledByUserId);
        Status = ReceivableStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
    }

    private void EnsureNotCancelled()
    {
        if (Status == ReceivableStatus.Cancelled)
        {
            throw new InvalidOperationException("La cuenta por cobrar está anulada.");
        }
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
        : throw new ArgumentException("La fecha de vencimiento es obligatoria.", parameterName);

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
