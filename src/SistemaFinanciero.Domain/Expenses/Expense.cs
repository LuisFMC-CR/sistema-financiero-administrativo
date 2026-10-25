using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Domain.Expenses;

/// <summary>
/// Compra a un proveedor respaldada por un comprobante electrónico externo (regla de negocio 2). A
/// diferencia de una factura, no tiene líneas: es una sola compra con un monto neto, una categoría y
/// una tarifa de IVA.
/// </summary>
/// <remarks>
/// El gasto se registra sin IVA; el propio gasto calcula el impuesto y el total sobre su neto (regla
/// 3). Al confirmarse de contado guarda directamente su cuenta de efectivo y su referencia (regla 7);
/// el medio de pago no se vuelve a pedir porque ya quedó fijo desde el borrador. A crédito no guarda
/// cuenta de efectivo ni referencia: esos datos los tendrá el futuro <c>SupplierPayment</c>.
///
/// Conexiones: la crea y confirma <c>ExpenseService</c> (Infrastructure/Expenses), que genera su
/// <see cref="Code"/> (regla 12, <c>GAS-001</c>) antes de construirla y, al confirmar a crédito, crea
/// su <c>Payable</c> (Domain/Payables). Se guarda en <c>finanzas.Gastos</c> vía
/// <c>ExpenseConfiguration</c>. Todavía no valida el límite de autorización ni la fecha de cierre: esas
/// reglas, que requieren datos externos (<c>SystemParameters</c>), las aplica <c>ExpenseService</c>
/// antes de llamar a <see cref="Confirm"/>.
/// </remarks>
public sealed class Expense
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima de la descripción de la compra.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Longitud máxima de la referencia del pago de contado.</summary>
    public const int ReferenceMaxLength = 100;

    /// <summary>Longitud máxima del motivo de anulación.</summary>
    public const int ReasonMaxLength = 300;

    /// <summary>Longitud máxima del código de impuesto conservado como fotografía.</summary>
    public const int TaxCodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre de impuesto conservado como fotografía.</summary>
    public const int TaxNameMaxLength = 120;

    private Expense()
    {
    }

    /// <summary>
    /// Crea un borrador de gasto. El vencimiento no se recibe: se calcula a partir del plazo de pago
    /// (regla 14), igual que en <see cref="Invoice"/>. El IVA se calcula sobre el neto recibido (regla
    /// 3); el gasto no se registra con IVA incluido.
    /// </summary>
    public Expense(
        Guid id,
        string code,
        Guid supplierId,
        Guid categoryId,
        string description,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        decimal netAmount,
        Guid? taxTypeId,
        string taxCode,
        string taxName,
        decimal taxRate,
        PaymentMethod paymentMethod,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        SupplierId = DomainRules.RequiredId(supplierId, nameof(supplierId));
        CategoryId = DomainRules.RequiredId(categoryId, nameof(categoryId));
        Description = DomainRules.RequiredText(description, DescriptionMaxLength, nameof(description));
        IssueDate = RequireDate(issueDate, nameof(issueDate));
        PaymentTerm = DomainRules.DefinedEnum(paymentTerm, nameof(paymentTerm));
        DueDate = CalculateDueDate(PaymentTerm, IssueDate);
        NetAmount = ValidatePositiveAmount(netAmount, nameof(netAmount));
        TaxTypeId = DomainRules.OptionalId(taxTypeId, nameof(taxTypeId));
        TaxCode = DomainRules.NormalizedCode(taxCode, TaxCodeMaxLength, nameof(taxCode));
        TaxName = DomainRules.RequiredText(taxName, TaxNameMaxLength, nameof(taxName));
        TaxRate = NormalizeRate(taxRate, nameof(taxRate));
        TaxAmount = CalculateTaxAmount(NetAmount, TaxRate);
        TotalAmount = DomainRules.RoundMoney(NetAmount + TaxAmount);
        PaymentMethod = DomainRules.DefinedEnum(paymentMethod, nameof(paymentMethod));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = ExpenseStatus.Draft;
    }

    /// <summary>Identificador técnico estable del gasto.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código automático asignado al crear (regla 12, <c>GAS-001</c>); no es editable.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Proveedor de la compra; obligatorio en todo gasto (regla 3).</summary>
    public Guid SupplierId { get; private set; }

    /// <summary>Categoría de gasto de último nivel.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Descripción de la compra.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Fecha de negocio del gasto.</summary>
    public DateOnly IssueDate { get; private set; }

    /// <summary>Condición de pago: contado o crédito.</summary>
    public PaymentTerm PaymentTerm { get; private set; }

    /// <summary>Fecha de vencimiento; solo existe cuando el gasto es a crédito.</summary>
    public DateOnly? DueDate { get; private set; }

    /// <summary>Monto neto (sin IVA) de la compra, tal como se registra (regla 3).</summary>
    public decimal NetAmount { get; private set; }

    /// <summary>Tipo de impuesto del catálogo del que se tomó la fotografía, cuando se usó uno.</summary>
    public Guid? TaxTypeId { get; private set; }

    /// <summary>Código de la tarifa de IVA vigente al preparar el gasto.</summary>
    public string TaxCode { get; private set; } = string.Empty;

    /// <summary>Nombre de la tarifa de IVA vigente al preparar el gasto.</summary>
    public string TaxName { get; private set; } = string.Empty;

    /// <summary>Porcentaje de IVA utilizado.</summary>
    public decimal TaxRate { get; private set; }

    /// <summary>Importe de IVA, calculado sobre <see cref="NetAmount"/>.</summary>
    public decimal TaxAmount { get; private set; }

    /// <summary>Total final de la compra, neto más IVA.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Medio de pago, fijado desde el borrador; no se vuelve a pedir al confirmar (regla 7).</summary>
    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>Cuenta de efectivo de origen; solo se asigna al confirmar de contado.</summary>
    public Guid? CashAccountId { get; private set; }

    /// <summary>Referencia del pago de contado; solo se asigna al confirmar de contado.</summary>
    public string? Reference { get; private set; }

    /// <summary>Estado actual del documento.</summary>
    public ExpenseStatus Status { get; private set; }

    /// <summary>Instante UTC de confirmación, cuando corresponde.</summary>
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    /// <summary>Usuario que confirmó el documento, cuando corresponde.</summary>
    public Guid? ConfirmedByUserId { get; private set; }

    /// <summary>Motivo de anulación, cuando corresponde.</summary>
    public string? CancellationReason { get; private set; }

    /// <summary>Instante UTC de anulación, cuando corresponde.</summary>
    public DateTimeOffset? CancelledAtUtc { get; private set; }

    /// <summary>Usuario que anuló el documento, cuando corresponde.</summary>
    public Guid? CancelledByUserId { get; private set; }

    /// <summary>Instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Usuario creador.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Usuario del último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Modifica los datos del gasto mientras es borrador. Recalcula el vencimiento, el IVA y el total
    /// a partir de los valores recibidos; el código no cambia.
    /// </summary>
    public void UpdateHeader(
        Guid supplierId,
        Guid categoryId,
        string description,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        decimal netAmount,
        Guid? taxTypeId,
        string taxCode,
        string taxName,
        decimal taxRate,
        PaymentMethod paymentMethod,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        EnsureDraft();

        Guid validSupplierId = DomainRules.RequiredId(supplierId, nameof(supplierId));
        Guid validCategoryId = DomainRules.RequiredId(categoryId, nameof(categoryId));
        string validDescription = DomainRules.RequiredText(description, DescriptionMaxLength, nameof(description));
        DateOnly validIssueDate = RequireDate(issueDate, nameof(issueDate));
        PaymentTerm validPaymentTerm = DomainRules.DefinedEnum(paymentTerm, nameof(paymentTerm));
        decimal validNetAmount = ValidatePositiveAmount(netAmount, nameof(netAmount));
        Guid? validTaxTypeId = DomainRules.OptionalId(taxTypeId, nameof(taxTypeId));
        string validTaxCode = DomainRules.NormalizedCode(taxCode, TaxCodeMaxLength, nameof(taxCode));
        string validTaxName = DomainRules.RequiredText(taxName, TaxNameMaxLength, nameof(taxName));
        decimal validTaxRate = NormalizeRate(taxRate, nameof(taxRate));
        PaymentMethod validPaymentMethod = DomainRules.DefinedEnum(paymentMethod, nameof(paymentMethod));

        Touch(updatedAtUtc, updatedByUserId);
        SupplierId = validSupplierId;
        CategoryId = validCategoryId;
        Description = validDescription;
        IssueDate = validIssueDate;
        PaymentTerm = validPaymentTerm;
        DueDate = CalculateDueDate(validPaymentTerm, validIssueDate);
        NetAmount = validNetAmount;
        TaxTypeId = validTaxTypeId;
        TaxCode = validTaxCode;
        TaxName = validTaxName;
        TaxRate = validTaxRate;
        TaxAmount = CalculateTaxAmount(NetAmount, TaxRate);
        TotalAmount = DomainRules.RoundMoney(NetAmount + TaxAmount);
        PaymentMethod = validPaymentMethod;
    }

    /// <summary>
    /// Confirma el gasto. De contado exige cuenta de efectivo y referencia, que quedan guardadas en el
    /// propio gasto (regla 7); a crédito no recibe ninguna de las dos, porque esos datos los tendrá el
    /// futuro pago. Quien llama es responsable de validar antes el límite de autorización: el dominio
    /// no conoce <c>SystemParameters</c>.
    /// </summary>
    public void Confirm(
        DateTimeOffset confirmedAtUtc,
        Guid confirmedByUserId,
        Guid? cashAccountId,
        string? reference)
    {
        EnsureDraft();

        bool isCash = PaymentTerm == PaymentTerm.Cash;

        if (isCash)
        {
            if (cashAccountId is null || string.IsNullOrWhiteSpace(reference))
            {
                throw new ArgumentException(
                    "Para confirmar un gasto de contado debe indicar cuenta de efectivo y referencia.",
                    nameof(cashAccountId));
            }
        }
        else if (cashAccountId is not null || reference is not null)
        {
            throw new ArgumentException(
                "Un gasto a crédito no indica cuenta de efectivo ni referencia al confirmarse.",
                nameof(cashAccountId));
        }

        Guid? validCashAccountId = isCash
            ? DomainRules.RequiredId(cashAccountId!.Value, nameof(cashAccountId))
            : null;
        string? validReference = isCash
            ? DomainRules.RequiredText(reference, ReferenceMaxLength, nameof(reference))
            : null;

        Touch(confirmedAtUtc, confirmedByUserId);
        ConfirmedAtUtc = confirmedAtUtc;
        ConfirmedByUserId = confirmedByUserId;
        CashAccountId = validCashAccountId;
        Reference = validReference;
        Status = ExpenseStatus.Confirmed;
    }

    /// <summary>Anula un gasto confirmado sin eliminar su historia.</summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != ExpenseStatus.Confirmed)
        {
            throw new InvalidOperationException("Solo se puede anular un gasto confirmado.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, el gasto sigue confirmado.
        string validReason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledByUserId = cancelledByUserId;
        CancelledAtUtc = cancelledAtUtc;
        Status = ExpenseStatus.Cancelled;
    }

    private void EnsureDraft()
    {
        if (Status != ExpenseStatus.Draft)
        {
            throw new InvalidOperationException("Solo se puede modificar un gasto en borrador.");
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

    private static decimal NormalizeRate(decimal value, string parameterName)
    {
        decimal roundedValue = DomainRules.Round(value, DomainRules.QuantityDecimalPlaces);

        if (roundedValue < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "La tarifa de impuesto no puede ser negativa.");
        }

        return roundedValue;
    }

    private static decimal CalculateTaxAmount(decimal netAmount, decimal taxRate) =>
        DomainRules.RoundMoney(netAmount * taxRate / 100m);

    private static DateOnly RequireDate(DateOnly value, string parameterName) => value != default
        ? value
        : throw new ArgumentException("La fecha del gasto es obligatoria.", parameterName);

    // Regla 14: el vencimiento no se digita, se calcula como la fecha de emisión más los días de
    // crédito del plazo. De contado no tiene vencimiento. Misma fórmula que Invoice.
    private static DateOnly? CalculateDueDate(PaymentTerm paymentTerm, DateOnly issueDate) =>
        paymentTerm == PaymentTerm.Cash
            ? null
            : issueDate.AddDays(PaymentTermDays.GetDays(paymentTerm));

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }
}
