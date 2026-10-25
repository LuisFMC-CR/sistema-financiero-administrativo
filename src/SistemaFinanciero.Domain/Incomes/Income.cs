using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Incomes;

/// <summary>
/// Ingreso generado por una factura de contado, por un abono, o registrado manualmente (regla 2).
/// </summary>
/// <remarks>
/// Todo ingreso indica categoría y cuenta de efectivo de destino (regla 2), sin excepción según el
/// origen. <see cref="InvoiceId"/> es único cuando <see cref="Origin"/> es
/// <see cref="IncomeOrigin.InvoiceCash"/>; <see cref="ReceivablePaymentId"/> es único cuando es
/// <see cref="IncomeOrigin.ReceivablePayment"/>; ambos quedan nulos en
/// <see cref="IncomeOrigin.Manual"/>. El medio de pago solo se pide al confirmar de contado o al
/// registrar un ingreso manual (regla 10); un ingreso de abono no lo indica por separado, porque ya
/// quedó en la cuenta de efectivo y la referencia del abono.
///
/// Conexiones: lo crea <c>InvoiceService.ConfirmAsync</c> (origen <see cref="IncomeOrigin.InvoiceCash"/>),
/// <c>ReceivablePaymentService.RegisterAsync</c> (origen <see cref="IncomeOrigin.ReceivablePayment"/>)
/// e <c>IncomeService.CreateManualAsync</c> (origen <see cref="IncomeOrigin.Manual"/>). Se guarda en
/// <c>finanzas.Ingresos</c> vía <c>IncomeConfiguration</c>.
/// </remarks>
public sealed class Income
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima de la referencia.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima de la descripción y del motivo de anulación.</summary>
    public const int DescriptionMaxLength = 300;

    private Income()
    {
    }

    private Income(
        Guid id,
        string code,
        IncomeOrigin origin,
        Guid? invoiceId,
        Guid? receivablePaymentId,
        Guid categoryId,
        Guid cashAccountId,
        decimal amount,
        DateOnly incomeDate,
        PaymentMethod? paymentMethod,
        string? reference,
        string? description,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Origin = DomainRules.DefinedEnum(origin, nameof(origin));
        InvoiceId = DomainRules.OptionalId(invoiceId, nameof(invoiceId));
        ReceivablePaymentId = DomainRules.OptionalId(receivablePaymentId, nameof(receivablePaymentId));
        CategoryId = DomainRules.RequiredId(categoryId, nameof(categoryId));
        CashAccountId = DomainRules.RequiredId(cashAccountId, nameof(cashAccountId));
        Amount = ValidatePositiveAmount(amount, nameof(amount));
        IncomeDate = RequireDate(incomeDate, nameof(incomeDate));
        PaymentMethod = ValidatePaymentMethod(Origin, paymentMethod);
        Reference = DomainRules.OptionalText(reference, ReferenceMaxLength, nameof(reference));
        Description = ValidateDescription(Origin, description);
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = IncomeStatus.Active;

        EnsureOriginConsistency();
    }

    /// <summary>Crea el ingreso automático de una factura de contado recién confirmada.</summary>
    public static Income FromInvoice(
        Guid id,
        string code,
        Guid invoiceId,
        Guid categoryId,
        Guid cashAccountId,
        decimal amount,
        DateOnly incomeDate,
        PaymentMethod paymentMethod,
        string reference,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId) =>
        new(
            id,
            code,
            IncomeOrigin.InvoiceCash,
            invoiceId,
            receivablePaymentId: null,
            categoryId,
            cashAccountId,
            amount,
            incomeDate,
            paymentMethod,
            reference,
            description: null,
            createdAtUtc,
            createdByUserId);

    /// <summary>
    /// Crea el ingreso automático de un abono: el monto incluye el efectivo recibido más la
    /// retención, si la hubo, porque ambos rebajan el saldo de la cuenta por cobrar (regla 2/3).
    /// </summary>
    public static Income FromReceivablePayment(
        Guid id,
        string code,
        Guid receivablePaymentId,
        Guid categoryId,
        Guid cashAccountId,
        decimal amount,
        DateOnly incomeDate,
        string reference,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId) =>
        new(
            id,
            code,
            IncomeOrigin.ReceivablePayment,
            invoiceId: null,
            receivablePaymentId,
            categoryId,
            cashAccountId,
            amount,
            incomeDate,
            paymentMethod: null,
            reference,
            description: null,
            createdAtUtc,
            createdByUserId);

    /// <summary>
    /// Crea un ingreso manual: otras entradas de la operación, como reintegros o intereses que
    /// acredite el banco, nunca préstamos ni aportes (regla 2). La descripción es obligatoria porque
    /// es el único dato que explica de qué se trata.
    /// </summary>
    public static Income CreateManual(
        Guid id,
        string code,
        Guid categoryId,
        Guid cashAccountId,
        decimal amount,
        DateOnly incomeDate,
        PaymentMethod paymentMethod,
        string? reference,
        string description,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId) =>
        new(
            id,
            code,
            IncomeOrigin.Manual,
            invoiceId: null,
            receivablePaymentId: null,
            categoryId,
            cashAccountId,
            amount,
            incomeDate,
            paymentMethod,
            reference,
            description,
            createdAtUtc,
            createdByUserId);

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código automático asignado al crear (regla 12, <c>ING-001</c>); no es editable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene el origen del ingreso.</summary>
    public IncomeOrigin Origin { get; private set; }

    /// <summary>Obtiene la factura de contado que lo generó, solo cuando el origen lo es.</summary>
    public Guid? InvoiceId { get; private set; }

    /// <summary>Obtiene el abono que lo generó, solo cuando el origen lo es.</summary>
    public Guid? ReceivablePaymentId { get; private set; }

    /// <summary>Obtiene la categoría de ingreso.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Obtiene la cuenta de efectivo de destino.</summary>
    public Guid CashAccountId { get; private set; }

    /// <summary>Obtiene el monto del ingreso.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene la fecha de negocio del ingreso.</summary>
    public DateOnly IncomeDate { get; private set; }

    /// <summary>Obtiene el medio de pago, solo presente en contado y manual.</summary>
    public PaymentMethod? PaymentMethod { get; private set; }

    /// <summary>Obtiene la referencia, cuando se indicó.</summary>
    public string? Reference { get; private set; }

    /// <summary>Obtiene la descripción; obligatoria en los manuales, nula en los demás orígenes.</summary>
    public string? Description { get; private set; }

    /// <summary>Obtiene el estado actual.</summary>
    public IncomeStatus Status { get; private set; }

    /// <summary>Obtiene el motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Obtiene el instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que anuló el ingreso, cuando corresponde.</summary>
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
    /// Anula el ingreso. Cuando proviene de una factura o de un abono, quien llama es responsable de
    /// anular también el documento de origen en la misma transacción (regla 4).
    /// </summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != IncomeStatus.Active)
        {
            throw new InvalidOperationException("El ingreso ya está anulado.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, el ingreso sigue vigente.
        string validReason = DomainRules.RequiredText(reason, DescriptionMaxLength, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserId = cancelledByUserId;
        Status = IncomeStatus.Cancelled;
    }

    private void EnsureOriginConsistency()
    {
        switch (Origin)
        {
            case IncomeOrigin.InvoiceCash:
                if (InvoiceId is null || ReceivablePaymentId is not null)
                {
                    throw new ArgumentException(
                        "Un ingreso de factura de contado debe indicar la factura y no un abono.",
                        nameof(InvoiceId));
                }

                break;
            case IncomeOrigin.ReceivablePayment:
                if (ReceivablePaymentId is null || InvoiceId is not null)
                {
                    throw new ArgumentException(
                        "Un ingreso de abono debe indicar el abono y no una factura.",
                        nameof(ReceivablePaymentId));
                }

                break;
            case IncomeOrigin.Manual:
                if (InvoiceId is not null || ReceivablePaymentId is not null)
                {
                    throw new ArgumentException(
                        "Un ingreso manual no puede indicar factura ni abono.",
                        nameof(Origin));
                }

                break;
        }
    }

    private static PaymentMethod? ValidatePaymentMethod(IncomeOrigin origin, PaymentMethod? paymentMethod)
    {
        bool requiresPaymentMethod = origin is IncomeOrigin.InvoiceCash or IncomeOrigin.Manual;

        if (requiresPaymentMethod && paymentMethod is null)
        {
            throw new ArgumentException(
                "El medio de pago es obligatorio para este origen de ingreso.",
                nameof(paymentMethod));
        }

        if (!requiresPaymentMethod && paymentMethod is not null)
        {
            throw new ArgumentException(
                "Un ingreso de abono no indica medio de pago propio.",
                nameof(paymentMethod));
        }

        return paymentMethod is null ? null : DomainRules.DefinedEnum(paymentMethod.Value, nameof(paymentMethod));
    }

    private static string? ValidateDescription(IncomeOrigin origin, string? description)
    {
        if (origin == IncomeOrigin.Manual && string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException(
                "Un ingreso manual debe describir su origen (reintegro, interés bancario, etc.).",
                nameof(description));
        }

        return DomainRules.OptionalText(description, DescriptionMaxLength, nameof(description));
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
        : throw new ArgumentException("La fecha del ingreso es obligatoria.", parameterName);

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
