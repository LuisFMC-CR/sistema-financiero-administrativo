using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Payables;

namespace SistemaFinanciero.Application.Payables;

/// <summary>Estado por el que se filtra el listado de cuentas por pagar.</summary>
public enum PayableStatusFilter
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
/// Define los filtros, uno por columna del listado, y la paginación de las cuentas por pagar.
/// </summary>
public sealed record PayableQuery
{
    public PayableQuery(
        string? expenseCode = null,
        string? supplierName = null,
        decimal? balanceMin = null,
        decimal? balanceMax = null,
        DateOnly? dueDateFrom = null,
        DateOnly? dueDateTo = null,
        PayableStatusFilter status = PayableStatusFilter.All,
        int page = 1,
        int pageSize = 20)
    {
        ExpenseCode = string.IsNullOrWhiteSpace(expenseCode) ? null : expenseCode.Trim();
        SupplierName = string.IsNullOrWhiteSpace(supplierName) ? null : supplierName.Trim();
        BalanceMin = balanceMin;
        BalanceMax = balanceMax;
        DueDateFrom = dueDateFrom;
        DueDateTo = dueDateTo;
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional buscado en el código del gasto.</summary>
    public string? ExpenseCode { get; }

    /// <summary>Texto opcional buscado en el nombre del proveedor.</summary>
    public string? SupplierName { get; }

    /// <summary>Saldo mínimo opcional.</summary>
    public decimal? BalanceMin { get; }

    /// <summary>Saldo máximo opcional.</summary>
    public decimal? BalanceMax { get; }

    /// <summary>Vencimiento mínimo opcional.</summary>
    public DateOnly? DueDateFrom { get; }

    /// <summary>Vencimiento máximo opcional.</summary>
    public DateOnly? DueDateTo { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public PayableStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>Proyección de una cuenta por pagar para consulta.</summary>
public sealed record PayableModel(
    Guid Id,
    Guid ExpenseId,
    string ExpenseCode,
    Guid SupplierId,
    string SupplierName,
    decimal OriginalAmount,
    decimal Balance,
    DateOnly DueDate,
    PayableStatus Status,
    DateTimeOffset CreatedAtUtc,
    string Version);

/// <summary>
/// Casos de uso de consulta de cuentas por pagar. El registro y la anulación de pagos los expone
/// <see cref="ISupplierPaymentService"/>.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>PayableService</c> (Infrastructure/Payables); lo consume el futuro
/// <c>PayablesController</c> (bloque 3 de este mismo paso).
/// <see cref="GetPendingBySupplierAsync"/> es la base del reparto propuesto de un pago a varias
/// cuentas por pagar (regla 2): de la más antigua a la más nueva, editable antes de registrar.
/// </remarks>
public interface IPayableService
{
    /// <summary>Busca cuentas por pagar con filtros y paginación.</summary>
    public Task<PagedResult<PayableModel>> SearchAsync(
        PayableQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una cuenta por pagar por identificador.</summary>
    public Task<PayableModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las cuentas por pagar pendientes o parcialmente pagadas de un proveedor, de la más
    /// antigua a la más nueva por fecha de vencimiento.
    /// </summary>
    public Task<IReadOnlyList<PayableModel>> GetPendingBySupplierAsync(
        Guid supplierId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume las cuentas por pagar vencidas y las próximas a vencer en los próximos
    /// <paramref name="alertDays"/> días (regla 9), para el indicador de inicio y de Consultas.
    /// </summary>
    public Task<OverdueSummary> GetOverdueSummaryAsync(
        int alertDays,
        CancellationToken cancellationToken = default);
}

/// <summary>Monto a aplicar a una cuenta por pagar dentro de un mismo pago.</summary>
public sealed record SupplierPaymentAllocationCommand(Guid PayableId, decimal Amount);

/// <summary>
/// Datos para registrar un pago a una o varias cuentas por pagar del mismo proveedor (regla 2). Todas
/// las asignaciones comparten proveedor, cuenta de efectivo, referencia y fecha. A diferencia de un
/// abono, no lleva categoría ni retención: no genera ningún ingreso ni documento equivalente, y "los
/// pagos a proveedores no llevan retención" (regla 3).
/// </summary>
public sealed record RegisterSupplierPaymentCommand(
    Guid SupplierId,
    Guid CashAccountId,
    string Reference,
    DateOnly PaymentDate,
    IReadOnlyList<SupplierPaymentAllocationCommand> Allocations);

/// <summary>Proyección de un pago para consulta.</summary>
public sealed record SupplierPaymentModel(
    Guid Id,
    Guid PayableId,
    Guid GroupId,
    decimal Amount,
    Guid CashAccountId,
    string CashAccountName,
    string Reference,
    DateOnly PaymentDate,
    SupplierPaymentStatus Status,
    string? CancellationReason,
    string Version);

/// <summary>
/// Resultado de registrar un pago: la operación, el identificador de cada fila creada (una por cuenta
/// por pagar) y si la referencia ya se había usado antes en la misma cuenta de efectivo (regla 10:
/// advertencia, no bloquea).
/// </summary>
public sealed record RegisterSupplierPaymentResult(
    CatalogOperationResult Operation,
    IReadOnlyList<Guid> SupplierPaymentIds,
    bool DuplicateReferenceWarning);

/// <summary>
/// Casos de uso del registro y la anulación de pagos, incluido el reparto a varias cuentas por pagar
/// con un <c>GroupId</c> común (regla 2). Espejo de <c>IReceivablePaymentService</c>, del lado de
/// ingresos, sin retención.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>SupplierPaymentService</c> (Infrastructure/Payables);
/// <see cref="RegisterAsync"/> valida el límite de autorización (regla 7: solo Gerencia registra pagos
/// sobre el límite, indicado por el parámetro <c>actorIsManagement</c> de quien llama) contra la suma
/// de los montos de todo el pago. <see cref="CancelAsync"/> anula una sola fila sin afectar a las
/// demás de su mismo <c>GroupId</c> (regla 2: "cada uno se anula de forma individual").
/// </remarks>
public interface ISupplierPaymentService
{
    /// <summary>Obtiene un pago por identificador.</summary>
    public Task<SupplierPaymentModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista los pagos de una cuenta por pagar, del más reciente al más antiguo.</summary>
    public Task<IReadOnlyList<SupplierPaymentModel>> SearchByPayableAsync(
        Guid payableId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra un pago a una o varias cuentas por pagar del mismo proveedor, en una sola transacción:
    /// crea una fila por cuenta por pagar.
    /// </summary>
    public Task<RegisterSupplierPaymentResult> RegisterAsync(
        RegisterSupplierPaymentCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula un pago: restituye el saldo de su cuenta por pagar, sin afectar a los demás pagos del
    /// mismo <c>GroupId</c>. Solo Gerencia.
    /// </summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
