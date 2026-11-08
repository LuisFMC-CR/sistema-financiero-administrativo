using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.CashTransfers;

namespace SistemaFinanciero.Application.CashTransfers;

/// <summary>Estado por el que se filtra el listado de traspasos.</summary>
public enum CashTransferStatusFilter
{
    /// <summary>Incluye todos los estados.</summary>
    All,

    /// <summary>Solo vigentes.</summary>
    Active,

    /// <summary>Solo anulados.</summary>
    Cancelled,
}

/// <summary>
/// Define los filtros del listado de traspasos (código, fecha, cuenta de origen o destino, estado;
/// paquete v8, bloque 1, punto 5) y su paginación.
/// </summary>
public sealed record CashTransferQuery
{
    public CashTransferQuery(
        string? code = null,
        DateOnly? transferDateFrom = null,
        DateOnly? transferDateTo = null,
        Guid? cashAccountId = null,
        CashTransferStatusFilter status = CashTransferStatusFilter.All,
        int page = 1,
        int pageSize = 20)
    {
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        TransferDateFrom = transferDateFrom;
        TransferDateTo = transferDateTo;
        CashAccountId = cashAccountId;
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional buscado en el código.</summary>
    public string? Code { get; }

    /// <summary>Fecha inicial opcional del traspaso.</summary>
    public DateOnly? TransferDateFrom { get; }

    /// <summary>Fecha final opcional del traspaso.</summary>
    public DateOnly? TransferDateTo { get; }

    /// <summary>Cuenta de efectivo opcional, de origen o de destino.</summary>
    public Guid? CashAccountId { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public CashTransferStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>Proyección de un traspaso para consulta.</summary>
public sealed record CashTransferModel(
    Guid Id,
    string Code,
    Guid SourceCashAccountId,
    string SourceCashAccountName,
    Guid DestinationCashAccountId,
    string DestinationCashAccountName,
    decimal Amount,
    string Reference,
    string Description,
    DateOnly TransferDate,
    CashTransferStatus Status,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    string Version);

/// <summary>
/// Datos para registrar un traspaso entre dos cuentas de efectivo propias (paquete v8, bloque 1).
/// </summary>
public sealed record RegisterCashTransferCommand(
    Guid SourceCashAccountId,
    Guid DestinationCashAccountId,
    decimal Amount,
    string Reference,
    string Description,
    DateOnly TransferDate);

/// <summary>
/// Resultado de registrar un traspaso: la operación, el identificador creado y si la referencia ya se
/// había usado antes en alguna de las dos cuentas de efectivo (regla 10: advertencia, no bloquea).
/// </summary>
public sealed record RegisterCashTransferResult(
    CatalogOperationResult Operation,
    Guid? CashTransferId,
    bool DuplicateReferenceWarning);

/// <summary>
/// Casos de uso del registro, la consulta y la anulación de traspasos entre cuentas de efectivo
/// propias (paquete v8, bloque 1). Espejo de <c>ISupplierPaymentService</c>, sin agrupación: un
/// traspaso siempre es una sola fila.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>CashTransferService</c> (Infrastructure/CashTransfers).
/// <see cref="RegisterAsync"/> valida el límite de autorización (regla 7: solo Gerencia registra
/// traspasos sobre el límite, indicado por el parámetro <c>actorIsManagement</c> de quien llama) y la
/// fecha de cierre (<c>ClosingDateGuard</c>). <see cref="CancelAsync"/> es exclusivo de Gerencia.
/// </remarks>
public interface ICashTransferService
{
    /// <summary>Obtiene un traspaso por identificador.</summary>
    public Task<CashTransferModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca traspasos con filtros y paginación.</summary>
    public Task<PagedResult<CashTransferModel>> SearchAsync(
        CashTransferQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Registra un traspaso entre dos cuentas de efectivo propias, en una sola operación.</summary>
    public Task<RegisterCashTransferResult> RegisterAsync(
        RegisterCashTransferCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);

    /// <summary>Anula un traspaso y genera la reversión de su asiento contable. Solo Gerencia.</summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
