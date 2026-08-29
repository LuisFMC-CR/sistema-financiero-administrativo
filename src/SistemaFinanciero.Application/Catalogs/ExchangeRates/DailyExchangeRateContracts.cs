using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Catalogs.ExchangeRates;

/// <summary>Datos editables de un tipo de cambio diario.</summary>
public sealed record SaveDailyExchangeRateCommand(
    DateOnly EffectiveDate,
    decimal CrcPerUsd,
    string Source,
    string? Notes);

/// <summary>Proyección administrativa de una tasa CRC por USD.</summary>
public sealed record DailyExchangeRateModel(
    Guid Id,
    DateOnly EffectiveDate,
    decimal CrcPerUsd,
    string Source,
    string? Notes,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Casos de uso de tipos de cambio diarios capturados manualmente.</summary>
public interface IDailyExchangeRateService
{
    /// <summary>Busca tasas por fecha o fuente con paginación.</summary>
    public Task<PagedResult<DailyExchangeRateModel>> SearchAsync(
        PageQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una tasa diaria por identificador.</summary>
    public Task<DailyExchangeRateModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Registra una tasa operativa para una fecha.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        SaveDailyExchangeRateCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Corrige una tasa diaria conservando auditoría y control de concurrencia.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        SaveDailyExchangeRateCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
