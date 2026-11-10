using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.IncomeTax;

namespace SistemaFinanciero.Application.IncomeTax;

/// <summary>Estado por el que se filtra el listado de pagos de impuesto sobre la renta.</summary>
public enum IncomeTaxPaymentStatusFilter
{
    /// <summary>Incluye todos los estados.</summary>
    All,

    /// <summary>Solo vigentes.</summary>
    Active,

    /// <summary>Solo anulados.</summary>
    Cancelled,
}

/// <summary>Filtros del listado de pagos de impuesto sobre la renta y su paginación.</summary>
public sealed record IncomeTaxPaymentQuery
{
    public IncomeTaxPaymentQuery(
        string? code = null,
        DateOnly? fiscalPeriodStartDate = null,
        DateOnly? paymentDateFrom = null,
        DateOnly? paymentDateTo = null,
        Guid? cashAccountId = null,
        IncomeTaxPaymentStatusFilter status = IncomeTaxPaymentStatusFilter.All,
        int page = 1,
        int pageSize = 20)
    {
        Code = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        FiscalPeriodStartDate = fiscalPeriodStartDate;
        PaymentDateFrom = paymentDateFrom;
        PaymentDateTo = paymentDateTo;
        CashAccountId = cashAccountId;
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional buscado en el código.</summary>
    public string? Code { get; }

    /// <summary>Período fiscal opcional (su primer día) al que se aplican los pagos.</summary>
    public DateOnly? FiscalPeriodStartDate { get; }

    /// <summary>Fecha inicial opcional del pago.</summary>
    public DateOnly? PaymentDateFrom { get; }

    /// <summary>Fecha final opcional del pago.</summary>
    public DateOnly? PaymentDateTo { get; }

    /// <summary>Cuenta de efectivo opcional de origen.</summary>
    public Guid? CashAccountId { get; }

    /// <summary>Estado que debe incluirse.</summary>
    public IncomeTaxPaymentStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}

/// <summary>Proyección de un pago de impuesto sobre la renta para consulta.</summary>
public sealed record IncomeTaxPaymentModel(
    Guid Id,
    string Code,
    DateOnly FiscalPeriodStartDate,
    DateOnly FiscalPeriodEndDate,
    Guid CashAccountId,
    string CashAccountName,
    PaymentMethod PaymentMethod,
    decimal Amount,
    string Reference,
    string? Description,
    DateOnly PaymentDate,
    IncomeTaxPaymentStatus Status,
    string? CancellationReason,
    DateTimeOffset CreatedAtUtc,
    string Version);

/// <summary>
/// Datos para registrar un pago a Hacienda. El último día del período fiscal se calcula a partir de
/// <see cref="FiscalPeriodStartDate"/> (doce meses).
/// </summary>
public sealed record RegisterIncomeTaxPaymentCommand(
    DateOnly FiscalPeriodStartDate,
    Guid CashAccountId,
    PaymentMethod PaymentMethod,
    decimal Amount,
    string Reference,
    string? Description,
    DateOnly PaymentDate);

/// <summary>
/// Resultado de registrar un pago: la operación, el identificador creado y si la referencia ya se
/// había usado en la misma cuenta de efectivo (regla 10: advertencia, no bloquea).
/// </summary>
public sealed record RegisterIncomeTaxPaymentResult(
    CatalogOperationResult Operation,
    Guid? PaymentId,
    bool DuplicateReferenceWarning);

/// <summary>Escala del impuesto sobre la renta vigente, con el token de concurrencia de los parámetros.</summary>
public sealed record IncomeTaxScaleModel(
    decimal Bracket1UpperLimit,
    decimal Bracket2UpperLimit,
    decimal Bracket3UpperLimit,
    decimal Bracket1Rate,
    decimal Bracket2Rate,
    decimal Bracket3Rate,
    decimal Bracket4Rate,
    decimal GrossIncomeThreshold,
    decimal GeneralRate,
    string Version);

/// <summary>Valores nuevos de la escala del impuesto sobre la renta.</summary>
public sealed record SaveIncomeTaxScaleCommand(
    decimal Bracket1UpperLimit,
    decimal Bracket2UpperLimit,
    decimal Bracket3UpperLimit,
    decimal Bracket1Rate,
    decimal Bracket2Rate,
    decimal Bracket3Rate,
    decimal Bracket4Rate,
    decimal GrossIncomeThreshold,
    decimal GeneralRate);

/// <summary>Un pago vigente incluido en la estimación.</summary>
public sealed record IncomeTaxEstimatePaymentLine(
    string Code,
    DateOnly PaymentDate,
    string Reference,
    decimal Amount);

/// <summary>
/// Resultado del reporte "Estimación del impuesto sobre la renta" (RF-99) de un período fiscal:
/// renta bruta y neta del estado de resultados, tramos aplicados, retenciones, pagos y saldo estimado.
/// <see cref="HasData"/> es falso cuando el período no tiene movimientos ni pagos (regla 8: mensaje y
/// no se genera archivo).
/// </summary>
public sealed record IncomeTaxEstimateReportResult(
    ReportHeader Header,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IncomeTaxScaleModel Scale,
    decimal GrossIncome,
    decimal TotalExpense,
    decimal NetIncome,
    decimal TaxableIncome,
    IncomeTaxRegime Regime,
    IReadOnlyList<IncomeTaxBracketLine> Brackets,
    decimal EstimatedTax,
    decimal Withholdings,
    IReadOnlyList<IncomeTaxEstimatePaymentLine> Payments,
    decimal PaymentsTotal,
    decimal Balance,
    decimal AmountDue,
    decimal AmountInFavor,
    bool HasData);

/// <summary>
/// Casos de uso del impuesto sobre la renta (RF-99): pagos hechos a Hacienda, escala de tramos y
/// estimación del impuesto de un período fiscal.
/// </summary>
/// <remarks>
/// Conexiones: lo implementa <c>IncomeTaxService</c> (Infrastructure/IncomeTax).
/// <see cref="RegisterPaymentAsync"/> valida el límite de autorización (regla 7: solo Gerencia sobre el
/// límite, según <c>actorIsManagement</c>) y el cierre mensual (<c>ClosingDateGuard</c>).
/// <see cref="CancelPaymentAsync"/> y <see cref="UpdateScaleAsync"/> son exclusivos de Gerencia.
/// </remarks>
public interface IIncomeTaxService
{
    /// <summary>Obtiene un pago por identificador.</summary>
    public Task<IncomeTaxPaymentModel?> GetPaymentAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Busca pagos con filtros y paginación.</summary>
    public Task<PagedResult<IncomeTaxPaymentModel>> SearchPaymentsAsync(
        IncomeTaxPaymentQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Registra un pago a Hacienda con su asiento, en una sola operación.</summary>
    public Task<RegisterIncomeTaxPaymentResult> RegisterPaymentAsync(
        RegisterIncomeTaxPaymentCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);

    /// <summary>Anula un pago y genera la reversión de su asiento. Solo Gerencia.</summary>
    public Task<CatalogOperationResult> CancelPaymentAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reemplaza la escala y deja el cambio en su historial. Solo Gerencia. La escala vigente se lee
    /// en <c>SystemParametersModel.IncomeTaxScale</c>.
    /// </summary>
    public Task<CatalogOperationResult> UpdateScaleAsync(
        SaveIncomeTaxScaleCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimación del impuesto del período fiscal que inicia en <paramref name="fiscalPeriodStartDate"/>;
    /// nula si los parámetros del sistema aún no están configurados.
    /// </summary>
    public Task<IncomeTaxEstimateReportResult?> GetEstimateAsync(
        DateOnly fiscalPeriodStartDate,
        string generatedByUserName,
        CancellationToken cancellationToken = default);
}
