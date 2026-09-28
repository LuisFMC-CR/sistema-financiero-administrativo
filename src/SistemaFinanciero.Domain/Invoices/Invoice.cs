using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Factura administrativa que conserva sus líneas y valores al confirmarse.</summary>
public sealed class Invoice
{
    private readonly List<InvoiceLine> lines = [];

    private Invoice()
    {
    }

    /// <summary>
    /// Crea un borrador de factura para un cliente en una única moneda. Una factura a crédito exige
    /// fecha de vencimiento; una de contado no la admite.
    /// </summary>
    public Invoice(
        Guid id,
        Guid customerId,
        DateOnly issueDate,
        CurrencyCode currency,
        PaymentTerm paymentTerm,
        DateOnly? dueDate,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        CustomerId = DomainRules.RequiredId(customerId, nameof(customerId));
        IssueDate = RequireDate(issueDate, nameof(issueDate));
        Currency = DomainRules.DefinedEnum(currency, nameof(currency));
        PaymentTerm = DomainRules.DefinedEnum(paymentTerm, nameof(paymentTerm));
        DueDate = RequireDueDate(PaymentTerm, IssueDate, dueDate, nameof(dueDate));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = InvoiceStatus.Draft;
    }

    /// <summary>Identificador técnico estable de la factura.</summary>
    public Guid Id { get; private set; }

    /// <summary>Consecutivo administrativo generado al guardar la factura.</summary>
    public long Number { get; private set; }

    /// <summary>Cliente de origen de la factura.</summary>
    public Guid CustomerId { get; private set; }

    /// <summary>Fecha de negocio de la factura.</summary>
    public DateOnly IssueDate { get; private set; }

    /// <summary>Moneda única usada por todos los importes de la factura.</summary>
    public CurrencyCode Currency { get; private set; }

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

    /// <summary>Tasa CRC por USD conservada al confirmar una factura USD.</summary>
    public decimal? ConfirmedCrcPerUsd { get; private set; }

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

    /// <summary>Versión de concurrencia administrada por SQL Server.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Líneas que componen la factura, siempre ordenadas por su posición.</summary>
    public IReadOnlyCollection<InvoiceLine> Lines => lines.OrderBy(line => line.Position).ToList().AsReadOnly();

    /// <summary>
    /// Modifica los datos del encabezado mientras la factura es borrador. Valida el conjunto completo
    /// antes de asignar, y no permite cambiar la moneda cuando ya existen líneas.
    /// </summary>
    public void UpdateHeader(
        Guid customerId,
        DateOnly issueDate,
        CurrencyCode currency,
        PaymentTerm paymentTerm,
        DateOnly? dueDate,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        EnsureDraft();

        Guid validCustomerId = DomainRules.RequiredId(customerId, nameof(customerId));
        DateOnly validIssueDate = RequireDate(issueDate, nameof(issueDate));
        CurrencyCode validCurrency = DomainRules.DefinedEnum(currency, nameof(currency));
        PaymentTerm validPaymentTerm = DomainRules.DefinedEnum(paymentTerm, nameof(paymentTerm));
        DateOnly? validDueDate = RequireDueDate(validPaymentTerm, validIssueDate, dueDate, nameof(dueDate));

        if (validCurrency != Currency && lines.Count > 0)
        {
            throw new InvalidOperationException("No se puede cambiar la moneda de una factura que ya tiene líneas.");
        }

        Touch(updatedAtUtc, updatedByUserId);
        CustomerId = validCustomerId;
        IssueDate = validIssueDate;
        Currency = validCurrency;
        PaymentTerm = validPaymentTerm;
        DueDate = validDueDate;
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

    /// <summary>Confirma el documento y fija la tasa histórica requerida por la moneda.</summary>
    public void Confirm(decimal? crcPerUsd, DateTimeOffset confirmedAtUtc, Guid confirmedByUserId)
    {
        EnsureDraft();

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("No se puede confirmar una factura sin líneas.");
        }

        // Todo se valida antes de asignar: si algún dato es inválido, la factura sigue siendo borrador.
        decimal? confirmedRate = Currency == CurrencyCode.USD
            ? RequirePositiveRate(crcPerUsd)
            : null;
        Touch(confirmedAtUtc, confirmedByUserId);

        ConfirmedCrcPerUsd = confirmedRate;
        ConfirmedByUserId = confirmedByUserId;
        ConfirmedAtUtc = confirmedAtUtc;
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

    private static DateOnly? RequireDueDate(
        PaymentTerm paymentTerm,
        DateOnly issueDate,
        DateOnly? dueDate,
        string parameterName)
    {
        if (paymentTerm == PaymentTerm.Cash)
        {
            return dueDate is null
                ? null
                : throw new ArgumentException("Una factura de contado no tiene fecha de vencimiento.", parameterName);
        }

        if (dueDate is null)
        {
            throw new ArgumentException("Una factura a crédito requiere fecha de vencimiento.", parameterName);
        }

        return dueDate.Value >= issueDate
            ? dueDate
            : throw new ArgumentOutOfRangeException(
                parameterName,
                dueDate,
                "El vencimiento no puede ser anterior a la fecha de emisión.");
    }

    private static decimal RequirePositiveRate(decimal? value)
    {
        if (value is null || value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "La factura USD requiere una tasa positiva.");
        }

        return DomainRules.Round(value.Value, ExchangeRate.RateDecimalPlaces);
    }
}
