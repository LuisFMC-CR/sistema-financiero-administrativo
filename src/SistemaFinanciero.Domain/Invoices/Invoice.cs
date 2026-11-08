using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Factura administrativa que conserva sus líneas y valores al confirmarse.</summary>
/// <remarks>
/// Conexiones: se guarda en <c>finanzas.Facturas</c> vía <c>InvoiceConfiguration</c>, con sus líneas
/// (<see cref="InvoiceLine"/>) en cascada. <see cref="CustomerId"/> apunta a <c>Customer</c>. Hoy no
/// existe ningún <c>IInvoiceService</c> ni <c>InvoicesController</c>: solo hay dominio, persistencia y
/// pruebas (<c>InvoiceDraftEditingTests</c>, <c>InvoiceConfirmationTests</c>,
/// <c>SqlServerInvoiceScenarioTests</c>...). Nadie puede crear una factura desde el navegador todavía;
/// es el primer bloque pendiente del paso 2 de <c>CLAUDE.md</c>. Al confirmarse, debería generar una
/// cuenta por cobrar (crédito) o un ingreso (contado), pero esas entidades tampoco existen aún.
/// </remarks>
public sealed class Invoice
{
    /// <summary>Longitud exacta de la clave del comprobante electrónico (bloque 3, RF-89).</summary>
    public const int ElectronicInvoiceKeyLength = 50;

    /// <summary>Longitud exacta del consecutivo del comprobante electrónico (bloque 3, RF-89).</summary>
    public const int ElectronicInvoiceNumberLength = 20;

    private readonly List<InvoiceLine> lines = [];

    private Invoice()
    {
    }

    /// <summary>
    /// Crea un borrador de factura para un cliente. El vencimiento no se recibe: se calcula a partir
    /// del plazo de pago (regla 14).
    /// </summary>
    public Invoice(
        Guid id,
        Guid customerId,
        Guid incomeAccountId,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        CustomerId = DomainRules.RequiredId(customerId, nameof(customerId));
        IncomeAccountId = DomainRules.RequiredId(incomeAccountId, nameof(incomeAccountId));
        IssueDate = RequireDate(issueDate, nameof(issueDate));
        PaymentTerm = DomainRules.DefinedEnum(paymentTerm, nameof(paymentTerm));
        DueDate = CalculateDueDate(PaymentTerm, IssueDate);
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = InvoiceStatus.Draft;
    }

    /// <summary>Identificador técnico estable de la factura.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Consecutivo administrativo (RF-34), asignado solo al confirmar; un borrador no tiene número.
    /// </summary>
    public long? Number { get; private set; }

    /// <summary>Cliente de origen de la factura.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Cuenta de ingreso de la venta: pertenece a la factura, no a cada cobro. El ingreso de contado y
    /// los de cada abono la heredan.
    /// </summary>
    public Guid IncomeAccountId { get; private set; }

    /// <summary>Fecha de negocio de la factura.</summary>
    public DateOnly IssueDate { get; private set; }

    /// <summary>Condición de pago: contado o crédito.</summary>
    public PaymentTerm PaymentTerm { get; private set; }

    /// <summary>Fecha de vencimiento; solo existe cuando la factura es a crédito.</summary>
    public DateOnly? DueDate { get; private set; }

    /// <summary>Estado actual del documento.</summary>
    public InvoiceStatus Status { get; private set; }

    /// <summary>Total antes de descuentos e impuestos.</summary>
    public decimal GrossAmount { get; private set; }

    /// <summary>Total de descuentos aplicados.</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Total antes de impuestos.</summary>
    public decimal NetAmount { get; private set; }

    /// <summary>Total de impuestos conservados por las líneas.</summary>
    public decimal TaxAmount { get; private set; }

    /// <summary>Total final a cobrar.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Instante UTC de confirmación, cuando corresponde.</summary>
    public DateTimeOffset? ConfirmedAtUtc { get; private set; }

    /// <summary>Usuario que confirmó el documento, cuando corresponde.</summary>
    public Guid? ConfirmedByUserId { get; private set; }

    /// <summary>
    /// Clave del comprobante electrónico de Hacienda (bloque 3, RF-89), generada por el sistema al
    /// confirmar. El sistema no la emite, firma ni transmite ante Hacienda.
    /// </summary>
    public string? ElectronicInvoiceKey { get; private set; }

    /// <summary>Consecutivo del comprobante electrónico de Hacienda, generado por el sistema al confirmar (RF-89).</summary>
    public string? ElectronicInvoiceNumber { get; private set; }

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

    /// <summary>Versión de concurrencia administrada por SQL Server.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Líneas que componen la factura, siempre ordenadas por su posición.</summary>
    public IReadOnlyCollection<InvoiceLine> Lines => lines.OrderBy(line => line.Position).ToList().AsReadOnly();

    /// <summary>
    /// Modifica los datos del encabezado mientras la factura es borrador. Valida el conjunto completo
    /// antes de asignar y recalcula el vencimiento a partir del plazo de pago (regla 14); el
    /// vencimiento no se recibe como dato.
    /// </summary>
    public void UpdateHeader(
        Guid customerId,
        Guid incomeAccountId,
        DateOnly issueDate,
        PaymentTerm paymentTerm,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        EnsureDraft();

        Guid validCustomerId = DomainRules.RequiredId(customerId, nameof(customerId));
        Guid validIncomeAccountId = DomainRules.RequiredId(incomeAccountId, nameof(incomeAccountId));
        DateOnly validIssueDate = RequireDate(issueDate, nameof(issueDate));
        PaymentTerm validPaymentTerm = DomainRules.DefinedEnum(paymentTerm, nameof(paymentTerm));

        Touch(updatedAtUtc, updatedByUserId);
        CustomerId = validCustomerId;
        IncomeAccountId = validIncomeAccountId;
        IssueDate = validIssueDate;
        PaymentTerm = validPaymentTerm;
        DueDate = CalculateDueDate(validPaymentTerm, validIssueDate);
    }

    /// <summary>Agrega una línea al final, únicamente mientras la factura es borrador.</summary>
    public void AddLine(InvoiceLine line, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);
        EnsureNewLineId(line);
        Touch(updatedAtUtc, updatedByUserId);
        SortLinesByPosition();
        line.MoveTo(lines.Count + 1);
        lines.Add(line);
        RecalculateTotals();
    }

    /// <summary>
    /// Reemplaza una línea por otra que ocupa su misma posición, únicamente mientras la factura es
    /// borrador. La línea nueva debe tener un identificador distinto de todas las existentes.
    /// </summary>
    public void ReplaceLine(
        Guid lineId,
        InvoiceLine replacement,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(replacement);
        int index = FindLineIndex(lineId);
        EnsureNewLineId(replacement);
        Touch(updatedAtUtc, updatedByUserId);
        replacement.MoveTo(lines[index].Position);
        lines[index] = replacement;
        RecalculateTotals();
    }

    /// <summary>
    /// Quita una línea, únicamente mientras la factura es borrador, y renumera las siguientes para que
    /// las posiciones sigan siendo consecutivas.
    /// </summary>
    public void RemoveLine(Guid lineId, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        EnsureDraft();
        int index = FindLineIndex(lineId);
        Touch(updatedAtUtc, updatedByUserId);
        lines.RemoveAt(index);

        for (int position = index; position < lines.Count; position++)
        {
            lines[position].MoveTo(position + 1);
        }

        RecalculateTotals();
    }

    /// <summary>
    /// Confirma el documento y le asigna su consecutivo (RF-34) y la clave y el consecutivo de su
    /// comprobante electrónico de Hacienda (bloque 3). El número y la clave los calcula quien llama
    /// (una secuencia de SQL Server y <see cref="SistemaFinanciero.Domain.Common.ElectronicDocumentKeyBuilder"/>,
    /// regla 12) porque el dominio no conoce la base de datos; este método solo valida y asigna, y
    /// nunca los vuelve a cambiar. El sistema no emite, firma ni transmite nada ante Hacienda.
    /// </summary>
    public void Confirm(
        DateTimeOffset confirmedAtUtc,
        Guid confirmedByUserId,
        long number,
        string electronicInvoiceKey,
        string electronicInvoiceNumber)
    {
        EnsureDraft();

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("No se puede confirmar una factura sin líneas.");
        }

        if (number <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(number), number, "El número de factura debe ser mayor que cero.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, la factura queda sin cambios.
        string validKey = DomainRules.RequiredDigits(
            electronicInvoiceKey, ElectronicInvoiceKeyLength, nameof(electronicInvoiceKey));
        string validNumber = DomainRules.RequiredDigits(
            electronicInvoiceNumber, ElectronicInvoiceNumberLength, nameof(electronicInvoiceNumber));

        Touch(confirmedAtUtc, confirmedByUserId);

        Number = number;
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = confirmedAtUtc;
        ElectronicInvoiceKey = validKey;
        ElectronicInvoiceNumber = validNumber;
        Status = InvoiceStatus.Confirmed;
    }

    /// <summary>Anula una factura confirmada sin eliminar su historia.</summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != InvoiceStatus.Confirmed)
        {
            throw new InvalidOperationException("Solo se puede anular una factura confirmada.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, la factura sigue confirmada.
        string validReason = DomainRules.RequiredText(reason, 300, nameof(reason));
        Touch(cancelledAtUtc, cancelledByUserId);

        CancellationReason = validReason;
        CancelledByUserId = cancelledByUserId;
        CancelledAtUtc = cancelledAtUtc;
        Status = InvoiceStatus.Cancelled;
    }

    private void RecalculateTotals()
    {
        GrossAmount = DomainRules.RoundMoney(lines.Sum(line => line.GrossAmount));
        DiscountAmount = DomainRules.RoundMoney(lines.Sum(line => line.DiscountAmount));
        NetAmount = DomainRules.RoundMoney(lines.Sum(line => line.NetAmount));
        TaxAmount = DomainRules.RoundMoney(lines.Sum(line => line.TaxAmount));
        TotalAmount = DomainRules.RoundMoney(lines.Sum(line => line.TotalAmount));
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Solo se puede modificar una factura en borrador.");
        }
    }

    // EF Core carga las líneas en el orden que devuelva la base de datos; se ordenan antes de operar
    // por índice para que la posición de cada línea coincida con su lugar en la lista.
    private void SortLinesByPosition() => lines.Sort((left, right) => left.Position.CompareTo(right.Position));

    private int FindLineIndex(Guid lineId)
    {
        SortLinesByPosition();
        int index = lines.FindIndex(line => line.Id == lineId);

        return index >= 0
            ? index
            : throw new InvalidOperationException("La línea indicada no pertenece a la factura.");
    }

    private void EnsureNewLineId(InvoiceLine line)
    {
        if (lines.Any(existing => existing.Id == line.Id))
        {
            throw new InvalidOperationException("Ya existe una línea con ese identificador en la factura.");
        }
    }

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        // Se validan ambos datos antes de asignar para que un error no deje la auditoría a medias.
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        Guid userId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        UpdatedAtUtc = timestamp;
        UpdatedByUserId = userId;
    }

    private static DateOnly RequireDate(DateOnly value, string parameterName) => value != default
        ? value
        : throw new ArgumentException("La fecha de emisión es obligatoria.", parameterName);

    // Regla 14: el vencimiento no se digita, se calcula como la fecha de emisión más los días de
    // crédito del plazo. De contado no tiene vencimiento.
    private static DateOnly? CalculateDueDate(PaymentTerm paymentTerm, DateOnly issueDate) =>
        paymentTerm == PaymentTerm.Cash
            ? null
            : issueDate.AddDays(PaymentTermDays.GetDays(paymentTerm));
}
