using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Application.Invoices;

/// <summary>Estado por el que se filtra el listado de facturas.</summary>
public enum InvoiceStatusFilter
{
    /// <summary>Incluye borradores, confirmadas y anuladas.</summary>
    All,

    /// <summary>Solo borradores.</summary>
    Draft,

    /// <summary>Solo confirmadas.</summary>
    Confirmed,

    /// <summary>Solo anuladas.</summary>
    Cancelled,
}

/// <summary>
/// Define búsqueda, estado y paginación para el listado de facturas. A diferencia de
/// <see cref="CatalogQuery"/>, no tiene baja lógica sino el ciclo Borrador/Confirmada/Anulada, y admite
/// restringir el resultado a las facturas de un usuario (Asistente «ve solo las suyas»).
/// </summary>
public sealed record InvoiceQuery
{
    public InvoiceQuery(
        string? search = null,
        InvoiceStatusFilter status = InvoiceStatusFilter.All,
        Guid? createdByUserId = null,
        DateOnly? issueDateFrom = null,
        DateOnly? issueDateTo = null,
        int page = 1,
        int pageSize = 20)
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Status = status;
        CreatedByUserId = createdByUserId;
        IssueDateFrom = issueDateFrom;
        IssueDateTo = issueDateTo;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional buscado en el número y el nombre del cliente.</summary>
    public string? Search { get; }

    /// <summary>Estado del documento que debe incluirse.</summary>
    public InvoiceStatusFilter Status { get; }

    /// <summary>Fecha de emisión mínima opcional, para la pantalla de Consultas (regla 11).</summary>
    public DateOnly? IssueDateFrom { get; }

    /// <summary>Fecha de emisión máxima opcional, para la pantalla de Consultas (regla 11).</summary>
    public DateOnly? IssueDateTo { get; }

    /// <summary>
    /// Cuando no es nulo, limita el resultado a las facturas creadas por este usuario (Asistente).
    /// </summary>
    public Guid? CreatedByUserId { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>Datos para crear el encabezado de un borrador de factura. Las líneas se agregan después.</summary>
public sealed record CreateInvoiceCommand(
    Guid CustomerId,
    DateOnly IssueDate,
    PaymentTerm PaymentTerm);

/// <summary>
/// Datos editables del encabezado mientras la factura es borrador. El vencimiento no se incluye: el
/// dominio lo recalcula a partir de la fecha de emisión y el plazo (regla 14).
/// </summary>
public sealed record UpdateInvoiceHeaderCommand(
    Guid CustomerId,
    DateOnly IssueDate,
    PaymentTerm PaymentTerm);

/// <summary>
/// Datos de una línea para agregar o para reemplazar una existente; la forma es la misma en ambos
/// casos, así que un solo tipo sirve para las dos operaciones.
/// </summary>
public sealed record InvoiceLineInputCommand(
    Guid? CatalogItemId,
    string Description,
    string UnitOfMeasure,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountAmount,
    Guid TaxTypeId);

/// <summary>Resultado de crear una factura: la operación y el identificador del borrador nuevo.</summary>
public sealed record InvoiceCreationResult(CatalogOperationResult Operation, Guid? InvoiceId);

/// <summary>
/// Datos que solo se piden al confirmar una factura de contado (regla 10): la cuenta de efectivo de
/// destino, el medio de pago, la referencia y la categoría del ingreso que se generará. En una
/// factura a crédito se ignoran: confirmar solo crea la cuenta por cobrar (regla 2).
/// </summary>
public sealed record ConfirmInvoiceCommand(
    Guid? CashAccountId,
    PaymentMethod? PaymentMethod,
    string? Reference,
    Guid? CategoryId);

/// <summary>Proyección de una línea de factura para la pantalla de edición y el detalle.</summary>
public sealed record InvoiceLineModel(
    Guid Id,
    int Position,
    Guid? CatalogItemId,
    string Description,
    string UnitOfMeasure,
    decimal Quantity,
    decimal UnitPrice,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    string TaxCode,
    string TaxName,
    decimal TaxRate,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal TotalAmount);

/// <summary>Proyección completa de una factura, con sus líneas, para el detalle y la edición.</summary>
public sealed record InvoiceModel(
    Guid Id,
    long? Number,
    Guid CustomerId,
    string CustomerName,
    DateOnly IssueDate,
    PaymentTerm PaymentTerm,
    DateOnly? DueDate,
    InvoiceStatus Status,
    decimal GrossAmount,
    decimal DiscountAmount,
    decimal NetAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTimeOffset? ConfirmedAtUtc,
    string? CancellationReason,
    DateTimeOffset? CancelledAtUtc,
    Guid CreatedByUserId,
    string CreatedByUserName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version,
    IReadOnlyList<InvoiceLineModel> Lines);

/// <summary>Proyección resumida de una factura para el listado.</summary>
public sealed record InvoiceSummaryModel(
    Guid Id,
    long? Number,
    string CustomerName,
    DateOnly IssueDate,
    PaymentTerm PaymentTerm,
    DateOnly? DueDate,
    InvoiceStatus Status,
    decimal TotalAmount,
    string CreatedByUserName);

/// <summary>
/// Casos de uso del ciclo de vida de la factura administrativa: crear y editar el borrador, agregar,
/// reemplazar y quitar líneas, confirmar y anular.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>InvoiceService</c> (Infrastructure/Invoices); lo consume
/// <c>InvoicesController</c> (Web, ruta <c>/facturacion/facturas</c>). Confirmar genera la cuenta por
/// cobrar (crédito) o el ingreso (contado) de la factura; anular la anula en cascada (regla 2/4),
/// siempre que no tenga abonos vigentes.
///
/// El parámetro <c>restrictToOwnerUserId</c> que reciben la lectura y las ediciones de borrador aplica
/// la regla de perfiles «Asistente ve solo las suyas»: cuando no es nulo, cualquier factura de otro
/// usuario se trata como inexistente (se responde igual que un identificador que no existe, sin
/// distinguir un caso del otro). Confirmar y anular no lo reciben porque son exclusivos de Finanzas y
/// Gerencia respectivamente, que no tienen esa restricción.
/// </remarks>
public interface IInvoiceService
{
    /// <summary>Busca facturas con filtros y paginación.</summary>
    public Task<PagedResult<InvoiceSummaryModel>> SearchAsync(
        InvoiceQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una factura con sus líneas por identificador.</summary>
    public Task<InvoiceModel?> GetAsync(
        Guid id,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Crea el encabezado de un borrador de factura, sin líneas.</summary>
    public Task<InvoiceCreationResult> CreateAsync(
        CreateInvoiceCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza el encabezado de un borrador.</summary>
    public Task<CatalogOperationResult> UpdateHeaderAsync(
        Guid id,
        UpdateInvoiceHeaderCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Agrega una línea a un borrador.</summary>
    public Task<CatalogOperationResult> AddLineAsync(
        Guid invoiceId,
        InvoiceLineInputCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Reemplaza una línea existente de un borrador, conservando su posición.</summary>
    public Task<CatalogOperationResult> ReplaceLineAsync(
        Guid invoiceId,
        Guid lineId,
        InvoiceLineInputCommand command,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Quita una línea de un borrador y renumera las siguientes.</summary>
    public Task<CatalogOperationResult> RemoveLineAsync(
        Guid invoiceId,
        Guid lineId,
        string version,
        Guid actorId,
        Guid? restrictToOwnerUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirma la factura: le asigna el número consecutivo (RF-34, secuencia de SQL Server), fija sus
    /// totales y genera su cuenta por cobrar (crédito) o su ingreso (contado, con los datos de
    /// <paramref name="command"/>). Solo Finanzas.
    /// </summary>
    public Task<CatalogOperationResult> ConfirmAsync(
        Guid id,
        ConfirmInvoiceCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Anula una factura confirmada con motivo. Solo Gerencia.</summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
