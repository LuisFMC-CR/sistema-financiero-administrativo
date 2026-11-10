using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Parameters;

/// <summary>Datos para configurar o actualizar los parámetros del sistema.</summary>
/// <remarks>
/// Conexiones: lo recibe <c>ISystemParametersService.CreateAsync</c>/<c>UpdateAsync</c>, implementado
/// por <c>SystemParametersService</c> (Infrastructure/Parameters); lo consume
/// <c>SystemParametersController</c> (Web, ruta <c>/parametros</c>).
/// </remarks>
public sealed record SaveSystemParametersCommand(decimal AuthorizationLimitCrc, int OverdueAlertDays);

/// <summary>Datos de la empresa (RF-55), para el encabezado de la factura interna (regla 15).</summary>
public sealed record SaveCompanyDataCommand(
    string Name,
    string LegalId,
    string? Address,
    string? Phone,
    string? Email,
    string? ActivityCode,
    string? TradeName,
    string? ProvinceCode,
    string? CantonCode,
    string? DistrictCode,
    string? BranchCode,
    string? TerminalCode);

/// <summary>Proyección administrativa de los parámetros del sistema.</summary>
public sealed record SystemParametersModel(
    decimal AuthorizationLimitCrc,
    int OverdueAlertDays,
    DateOnly ClosingDate,
    string? CompanyName,
    string? CompanyLegalId,
    string? CompanyAddress,
    string? CompanyPhone,
    string? CompanyEmail,
    string? CompanyActivityCode,
    string? CompanyTradeName,
    string? CompanyProvinceCode,
    string? CompanyCantonCode,
    string? CompanyDistrictCode,
    string? CompanyBranchCode,
    string? CompanyTerminalCode,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedByUserName,
    string Version,
    DateOnly? FiscalPeriodStartDate = null,
    DateOnly? FiscalPeriodEndDate = null,
    SistemaFinanciero.Application.IncomeTax.IncomeTaxScaleModel? IncomeTaxScale = null);

/// <summary>Proyección de un período anterior que Gerencia reabrió manualmente.</summary>
public sealed record ReopenedAccountingPeriodModel(
    int Year,
    int Month,
    string Reason,
    DateTimeOffset ReopenedAtUtc,
    string ReopenedByUserName,
    string Version);

/// <summary>
/// Casos de uso de los parámetros únicos del sistema: el límite de autorización (regla de negocio 7) y
/// los días de alerta de vencimiento (regla de negocio 9). Solo Gerencia los define.
/// </summary>
public interface ISystemParametersService
{
    /// <summary>Obtiene los parámetros vigentes, o nulo si todavía no se han configurado.</summary>
    public Task<SystemParametersModel?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Configura los parámetros por primera vez.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        SaveSystemParametersCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los parámetros existentes y deja un registro del cambio con los valores anteriores.
    /// </summary>
    public Task<CatalogOperationResult> UpdateAsync(
        SaveSystemParametersCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Define o cambia el período fiscal de doce meses con motivo opcional.</summary>
    public Task<CatalogOperationResult> UpdateFiscalPeriodAsync(
        DateOnly startDate,
        DateOnly endDate,
        string? reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Consulta las excepciones actualmente abiertas por Gerencia.</summary>
    public Task<IReadOnlyList<ReopenedAccountingPeriodModel>> GetReopenedAccountingPeriodsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Reabre un mes pasado hasta que Gerencia vuelva a cerrarlo.</summary>
    public Task<CatalogOperationResult> ReopenAccountingPeriodAsync(
        int year,
        int month,
        string reason,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Cierra de nuevo un mes que Gerencia había reabierto.</summary>
    public Task<CatalogOperationResult> CloseAccountingPeriodAsync(
        int year,
        int month,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza los datos de la empresa (RF-55). Solo Gerencia.</summary>
    public Task<CatalogOperationResult> UpdateCompanyAsync(
        SaveCompanyDataCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
