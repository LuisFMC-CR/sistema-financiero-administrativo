using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Receivables;

namespace SistemaFinanciero.Application.Receivables;

/// <summary>Proyección de una cuenta por cobrar para consulta.</summary>
/// <remarks>
/// <see cref="InvoiceNetAmount"/> es el neto (sin IVA) de la factura que la originó: es la base que
/// se propone por defecto para una retención al registrar un abono (regla 3), editable.
/// </remarks>
public sealed record ReceivableModel(
    Guid Id,
    Guid InvoiceId,
    long? InvoiceNumber,
    decimal InvoiceNetAmount,
    Guid CustomerId,
    string CustomerName,
    decimal OriginalAmount,
    decimal Balance,
    DateOnly DueDate,
    ReceivableStatus Status,
    DateTimeOffset CreatedAtUtc,
    string Version);

/// <summary>Estado por el que se filtra el listado de cuentas por cobrar.</summary>
public enum ReceivableStatusFilter
{
    /// <summary>Incluye todos los estados.</summary>
    All,

    /// <summary>Solo pendientes.</summary>
    Pending,

    /// <summary>Solo parcialmente pagadas.</summary>
    PartiallyPaid,

    /// <summary>Solo pagadas.</summary>
    Paid,

    /// <summary>Solo anuladas.</summary>
    Cancelled,
}

/// <summary>
/// Define los filtros, uno por columna del listado, y la paginación de las cuentas por cobrar.
/// </summary>
public sealed record ReceivableQuery
{
    public ReceivableQuery(
        string? invoiceNumber = null,
        string? customerName = null,
        decimal? balanceMin = null,
        decimal? balanceMax = null,
        DateOnly? dueDateFrom = null,
        DateOnly? dueDateTo = null,
        ReceivableStatusFilter status = ReceivableStatusFilter.All,
        int page = 1,
        int pageSize = 20)
    {
        InvoiceNumber = string.IsNullOrWhiteSpace(invoiceNumber) ? null : invoiceNumber.Trim();
        CustomerName = string.IsNullOrWhiteSpace(customerName) ? null : customerName.Trim();
        BalanceMin = balanceMin;
        BalanceMax = balanceMax;
        DueDateFrom = dueDateFrom;
        DueDateTo = dueDateTo;
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional buscado en el número de factura.</summary>
    public string? InvoiceNumber { get; }

    /// <summary>Texto opcional buscado en el nombre del cliente.</summary>
    public string? CustomerName { get; }

    /// <summary>Saldo mínimo opcional.</summary>
    public decimal? BalanceMin { get; }

    /// <summary>Saldo máximo opcional.</summary>
    public decimal? BalanceMax { get; }

    /// <summary>Vencimiento mínimo opcional.</summary>
    public DateOnly? DueDateFrom { get; }

    /// <summary>Vencimiento máximo opcional.</summary>
    public DateOnly? DueDateTo { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public ReceivableStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>
/// Casos de uso de consulta de cuentas por cobrar. El registro y la anulación de abonos los expone
/// <see cref="IReceivablePaymentService"/>.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>ReceivableService</c> (Infrastructure/Receivables); lo consume
/// <c>ReceivablesController</c> (Web, ruta <c>/cuentas-por-cobrar</c>).
/// <see cref="GetPendingByCustomerAsync"/> es la base del reparto propuesto de un abono a varias
/// facturas (regla 2): de la más antigua a la más nueva, editable antes de registrar.
/// </remarks>
public interface IReceivableService
{
    /// <summary>Busca cuentas por cobrar con filtros y paginación.</summary>
    public Task<PagedResult<ReceivableModel>> SearchAsync(
        ReceivableQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una cuenta por cobrar por identificador.</summary>
    public Task<ReceivableModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las cuentas por cobrar pendientes o parcialmente pagadas de un cliente, de la más
    /// antigua a la más nueva por fecha de vencimiento.
    /// </summary>
    public Task<IReadOnlyList<ReceivableModel>> GetPendingByCustomerAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume las cuentas por cobrar vencidas y las próximas a vencer en los próximos
    /// <paramref name="alertDays"/> días (regla 9), para el indicador de inicio y de Consultas.
    /// </summary>
    public Task<OverdueSummary> GetOverdueSummaryAsync(
        int alertDays,
        CancellationToken cancellationToken = default);
}

/// <summary>Retención a aplicar en una línea del abono (regla 3).</summary>
public sealed record WithholdingInputCommand(Guid WithholdingTypeId, decimal Base);

/// <summary>Monto a aplicar a una cuenta por cobrar dentro de un mismo abono.</summary>
public sealed record ReceivablePaymentAllocationCommand(
    Guid ReceivableId,
    decimal Amount,
    WithholdingInputCommand? Withholding);

/// <summary>
/// Datos para registrar un abono a una o varias facturas del mismo cliente (regla 2). Todas las
/// asignaciones comparten cliente, cuenta de efectivo, referencia, fecha y categoría del ingreso que
/// generarán.
/// </summary>
public sealed record RegisterReceivablePaymentCommand(
    Guid CustomerId,
    Guid CashAccountId,
    Guid CategoryId,
    string Reference,
    DateOnly PaymentDate,
    IReadOnlyList<ReceivablePaymentAllocationCommand> Allocations);

/// <summary>Proyección de un abono para consulta.</summary>
public sealed record ReceivablePaymentModel(
    Guid Id,
    Guid ReceivableId,
    Guid GroupId,
    decimal Amount,
    decimal? WithholdingAmount,
    Guid CashAccountId,
    string CashAccountName,
    string Reference,
    DateOnly PaymentDate,
    ReceivablePaymentStatus Status,
    string? CancellationReason,
    string Version);

/// <summary>
/// Resultado de registrar un abono: la operación, el identificador de cada fila creada (una por
/// factura) y si la referencia ya se había usado antes en la misma cuenta de efectivo (regla 10:
/// advertencia, no bloquea).
/// </summary>
public sealed record RegisterReceivablePaymentResult(
    CatalogOperationResult Operation,
    IReadOnlyList<Guid> ReceivablePaymentIds,
    bool DuplicateReferenceWarning);

/// <summary>
/// Casos de uso del registro y la anulación de abonos, incluido el reparto a varias facturas con un
/// <c>GroupId</c> común (regla 2).
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>ReceivablePaymentService</c> (Infrastructure/Receivables);
/// <see cref="RegisterAsync"/> valida el límite de autorización (regla 7: solo Gerencia registra
/// abonos sobre el límite, indicado por el parámetro <c>actorIsManagement</c> de quien llama) contra
/// la suma de montos más retenciones de todo el abono. <see cref="CancelAsync"/> anula una sola fila
/// sin afectar a las demás de su mismo <c>GroupId</c> (regla 2: "cada uno se anula de forma
/// individual").
/// </remarks>
public interface IReceivablePaymentService
{
    /// <summary>Obtiene un abono por identificador.</summary>
    public Task<ReceivablePaymentModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista los abonos de una cuenta por cobrar, del más reciente al más antiguo.</summary>
    public Task<IReadOnlyList<ReceivablePaymentModel>> SearchByReceivableAsync(
        Guid receivableId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un abono a una o varias cuentas por cobrar del mismo cliente, en una sola
    /// transacción: crea una fila por factura (con su retención opcional) y el ingreso de cada una.
    /// </summary>
    public Task<RegisterReceivablePaymentResult> RegisterAsync(
        RegisterReceivablePaymentCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula un abono: restituye el saldo de su cuenta por cobrar y anula su ingreso, sin afectar a
    /// los demás abonos del mismo <c>GroupId</c>. Solo Gerencia.
    /// </summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
