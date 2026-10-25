using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;

namespace SistemaFinanciero.Application.Incomes;

/// <summary>Proyección de un ingreso para consulta.</summary>
public sealed record IncomeModel(
    Guid Id,
    string Code,
    IncomeOrigin Origin,
    Guid? InvoiceId,
    long? InvoiceNumber,
    Guid? ReceivablePaymentId,
    Guid CategoryId,
    string CategoryName,
    Guid CashAccountId,
    string CashAccountName,
    decimal Amount,
    DateOnly IncomeDate,
    PaymentMethod? PaymentMethod,
    string? Reference,
    string? Description,
    IncomeStatus Status,
    string? CancellationReason,
    string Version);

/// <summary>Estado por el que se filtra el listado de ingresos.</summary>
public enum IncomeStatusFilter
{
    /// <summary>Incluye vigentes y anulados.</summary>
    All,

    /// <summary>Solo vigentes.</summary>
    Active,

    /// <summary>Solo anulados.</summary>
    Cancelled,
}

/// <summary>Origen por el que se filtra el listado de ingresos.</summary>
public enum IncomeOriginFilter
{
    /// <summary>Incluye los tres orígenes.</summary>
    All,

    /// <summary>Solo los de factura de contado.</summary>
    InvoiceCash,

    /// <summary>Solo los de abono.</summary>
    ReceivablePayment,

    /// <summary>Solo los manuales.</summary>
    Manual,
}

/// <summary>Define los filtros, uno por columna del listado, y la paginación de los ingresos.</summary>
public sealed record IncomeQuery
{
    public IncomeQuery(
        DateOnly? incomeDateFrom = null,
        DateOnly? incomeDateTo = null,
        IncomeOriginFilter origin = IncomeOriginFilter.All,
        string? categoryName = null,
        string? cashAccountName = null,
        string? referenceOrDescription = null,
        decimal? amountMin = null,
        decimal? amountMax = null,
        IncomeStatusFilter status = IncomeStatusFilter.Active,
        int page = 1,
        int pageSize = 20)
    {
        IncomeDateFrom = incomeDateFrom;
        IncomeDateTo = incomeDateTo;
        Origin = origin;
        CategoryName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName.Trim();
        CashAccountName = string.IsNullOrWhiteSpace(cashAccountName) ? null : cashAccountName.Trim();
        ReferenceOrDescription = string.IsNullOrWhiteSpace(referenceOrDescription) ? null : referenceOrDescription.Trim();
        AmountMin = amountMin;
        AmountMax = amountMax;
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Fecha mínima opcional.</summary>
    public DateOnly? IncomeDateFrom { get; }

    /// <summary>Fecha máxima opcional.</summary>
    public DateOnly? IncomeDateTo { get; }

    /// <summary>Origen que debe incluirse.</summary>
    public IncomeOriginFilter Origin { get; }

    /// <summary>Texto opcional buscado en el nombre de la categoría.</summary>
    public string? CategoryName { get; }

    /// <summary>Texto opcional buscado en el nombre de la cuenta de efectivo.</summary>
    public string? CashAccountName { get; }

    /// <summary>Texto opcional buscado en la referencia y la descripción.</summary>
    public string? ReferenceOrDescription { get; }

    /// <summary>Monto mínimo opcional.</summary>
    public decimal? AmountMin { get; }

    /// <summary>Monto máximo opcional.</summary>
    public decimal? AmountMax { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public IncomeStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>
/// Datos para registrar un ingreso manual: otras entradas de la operación, como reintegros o
/// intereses que acredite el banco, nunca préstamos ni aportes (regla 2).
/// </summary>
public sealed record CreateManualIncomeCommand(
    Guid CategoryId,
    Guid CashAccountId,
    decimal Amount,
    DateOnly IncomeDate,
    PaymentMethod PaymentMethod,
    string? Reference,
    string Description);

/// <summary>Resultado de crear un ingreso manual: la operación y el identificador del nuevo registro.</summary>
public sealed record IncomeCreationResult(CatalogOperationResult Operation, Guid? IncomeId);

/// <summary>
/// Casos de uso de consulta y registro manual de ingresos. Los ingresos generados automáticamente
/// (factura de contado, abono) los crean <c>IInvoiceService</c> y <c>IReceivablePaymentService</c>
/// respectivamente.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>IncomeService</c> (Infrastructure/Incomes).
/// <see cref="CancelAsync"/> solo anula ingresos manuales (regla 4: "lo generado automáticamente
/// solo se anula desde su origen"); un ingreso de factura o de abono se anula cancelando el documento
/// que lo generó.
/// </remarks>
public interface IIncomeService
{
    /// <summary>Busca ingresos con filtros y paginación.</summary>
    public Task<PagedResult<IncomeModel>> SearchAsync(
        IncomeQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un ingreso por identificador.</summary>
    public Task<IncomeModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra un ingreso manual.</summary>
    public Task<IncomeCreationResult> CreateManualAsync(
        CreateManualIncomeCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula un ingreso manual. Rechaza anular uno generado automáticamente: debe anularse desde su
    /// factura o su abono.
    /// </summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
