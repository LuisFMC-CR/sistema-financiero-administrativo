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

    /// <summary>Crea un borrador de factura para un cliente en una única moneda.</summary>
    public Invoice(
        Guid id,
        Guid customerId,
        DateOnly issueDate,
        CurrencyCode currency,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        CustomerId = DomainRules.RequiredId(customerId, nameof(customerId));
        IssueDate = RequireDate(issueDate, nameof(issueDate));
        Currency = DomainRules.DefinedEnum(currency, nameof(currency));
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

    /// <summary>Líneas que componen la factura.</summary>
    public IReadOnlyCollection<InvoiceLine> Lines => lines.AsReadOnly();

    /// <summary>Agrega una línea únicamente mientras la factura es borrador.</summary>
    public void AddLine(InvoiceLine line, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(line);
        lines.Add(line);
        RecalculateTotals();
        Touch(updatedAtUtc, updatedByUserId);
    }

    /// <summary>Confirma el documento y fija la tasa histórica requerida por la moneda.</summary>
    public void Confirm(decimal? crcPerUsd, DateTimeOffset confirmedAtUtc, Guid confirmedByUserId)
    {
        EnsureDraft();

        if (lines.Count == 0)
        {
            throw new InvalidOperationException("No se puede confirmar una factura sin líneas.");
        }

        ConfirmedCrcPerUsd = Currency == CurrencyCode.USD
            ? RequirePositiveRate(crcPerUsd)
            : null;
        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(confirmedAtUtc, nameof(confirmedAtUtc), UpdatedAtUtc);
        ConfirmedByUserId = DomainRules.RequiredId(confirmedByUserId, nameof(confirmedByUserId));
        ConfirmedAtUtc = timestamp;
        Status = InvoiceStatus.Confirmed;
        Touch(timestamp, ConfirmedByUserId.Value);
    }

    /// <summary>Anula una factura confirmada sin eliminar su historia.</summary>
    public void Cancel(string reason, DateTimeOffset cancelledAtUtc, Guid cancelledByUserId)
    {
        if (Status != InvoiceStatus.Confirmed)
        {
            throw new InvalidOperationException("Solo se puede anular una factura confirmada.");
        }

        DateTimeOffset timestamp = DomainRules.RequiredUtcTimestamp(cancelledAtUtc, nameof(cancelledAtUtc), UpdatedAtUtc);
        CancellationReason = DomainRules.RequiredText(reason, 300, nameof(reason));
        CancelledByUserId = DomainRules.RequiredId(cancelledByUserId, nameof(cancelledByUserId));
        CancelledAtUtc = timestamp;
        Status = InvoiceStatus.Cancelled;
        Touch(timestamp, CancelledByUserId.Value);
    }

    private void RecalculateTotals()
    {
        GrossAmount = Round(lines.Sum(line => line.GrossAmount));
        DiscountAmount = Round(lines.Sum(line => line.DiscountAmount));
        NetAmount = Round(lines.Sum(line => line.NetAmount));
        TaxAmount = Round(lines.Sum(line => line.TaxAmount));
        TotalAmount = Round(lines.Sum(line => line.TotalAmount));
    }

    private void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
        {
            throw new InvalidOperationException("Solo se puede modificar una factura en borrador.");
        }
    }

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        UpdatedAtUtc = DomainRules.RequiredUtcTimestamp(updatedAtUtc, nameof(updatedAtUtc), UpdatedAtUtc);
        UpdatedByUserId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
    }

    private static DateOnly RequireDate(DateOnly value, string parameterName) => value != default
        ? value
        : throw new ArgumentException("La fecha de emisión es obligatoria.", parameterName);

    private static decimal RequirePositiveRate(decimal? value)
    {
        if (value is null || value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "La factura USD requiere una tasa positiva.");
        }

        return Round(value.Value);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
