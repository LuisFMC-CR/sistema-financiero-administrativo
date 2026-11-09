using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.VatSettlements;

namespace SistemaFinanciero.Application.VatSettlements;

/// <summary>Estado por el que se filtra el listado de liquidaciones de IVA.</summary>
public enum VatSettlementStatusFilter
{
    /// <summary>Incluye todos los estados.</summary>
    All,

    /// <summary>Solo vigentes.</summary>
    Active,

    /// <summary>Solo anuladas.</summary>
    Cancelled,
}

/// <summary>
/// Define los filtros del listado de liquidaciones de IVA (año, estado; paquete v8, bloque 2) y su
/// paginación.
/// </summary>
public sealed record VatSettlementQuery
{
    public VatSettlementQuery(
        int? periodYear = null,
        VatSettlementStatusFilter status = VatSettlementStatusFilter.All,
        int page = 1,
        int pageSize = 20)
    {
        PeriodYear = periodYear;
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Año opcional del período.</summary>
    public int? PeriodYear { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public VatSettlementStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>Proyección de una liquidación de IVA para consulta.</summary>
public sealed record VatSettlementModel(
    Guid Id,
    string Code,
    int PeriodYear,
    int PeriodMonth,
    DateOnly SettlementDate,
    decimal VatPayableAmount,
    decimal VatCreditApplied,
    decimal PaymentAmount,
    decimal CreditCarriedForward,
    Guid? CashAccountId,
    string? CashAccountName,
    string? Reference,
    VatSettlementStatus Status,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    string Version);

/// <summary>
/// Vista previa del cálculo de una liquidación antes de registrarla (paquete v8, bloque 2, punto 7):
/// descompone el IVA por pagar y el acreditable entre lo propio del período y lo arrastrado de
/// períodos anteriores, para que se entienda de dónde sale el monto final.
/// </summary>
public sealed record VatSettlementPreview(
    int PeriodYear,
    int PeriodMonth,
    decimal VatPayableThisPeriod,
    decimal VatPayableCarriedFromPreviousPeriods,
    decimal VatPayableAmount,
    decimal VatCreditableThisPeriod,
    decimal PreviousCreditCarriedForward,
    decimal VatCreditAvailable,
    decimal VatCreditApplied,
    decimal PaymentAmount,
    decimal CreditCarriedForward,
    bool CanRegister,
    string? BlockedMessage);

/// <summary>Datos para registrar una liquidación mensual del IVA (paquete v8, bloque 2).</summary>
public sealed record RegisterVatSettlementCommand(
    int PeriodYear,
    int PeriodMonth,
    DateOnly SettlementDate,
    Guid? CashAccountId,
    string? Reference);

/// <summary>
/// Resultado de registrar una liquidación: la operación, el identificador creado y una advertencia
/// opcional (sin bloquear) cuando el período todavía tiene días sin cerrar.
/// </summary>
public sealed record RegisterVatSettlementResult(
    CatalogOperationResult Operation,
    Guid? VatSettlementId,
    bool OpenPeriodWarning);

/// <summary>
/// Casos de uso del cálculo, el registro, la consulta y la anulación de liquidaciones mensuales del
/// IVA (paquete v8, bloque 2).
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>VatSettlementService</c> (Infrastructure/VatSettlements).
/// <see cref="PreviewAsync"/> no escribe nada; <see cref="RegisterAsync"/> valida el límite de
/// autorización (regla 7: solo Gerencia registra pagos sobre el límite) contra
/// <c>PaymentAmount</c> y la fecha de cierre (<c>ClosingDateGuard</c>) contra
/// <c>SettlementDate</c>. <see cref="CancelAsync"/> exige que sea la liquidación vigente más
/// reciente y es exclusivo de Gerencia.
/// </remarks>
public interface IVatSettlementService
{
    /// <summary>Calcula, sin registrar nada, lo que resultaría de liquidar el período indicado.</summary>
    public Task<VatSettlementPreview> PreviewAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una liquidación por identificador.</summary>
    public Task<VatSettlementModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca liquidaciones con filtros y paginación.</summary>
    public Task<PagedResult<VatSettlementModel>> SearchAsync(
        VatSettlementQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Registra la liquidación de un período, en una sola operación.</summary>
    public Task<RegisterVatSettlementResult> RegisterAsync(
        RegisterVatSettlementCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Anula la liquidación vigente más reciente y genera la reversión de su asiento contable.
    /// Solo Gerencia.
    /// </summary>
    public Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
