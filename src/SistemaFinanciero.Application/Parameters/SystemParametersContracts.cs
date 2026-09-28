using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Parameters;

/// <summary>Datos para configurar o actualizar los parámetros del sistema.</summary>
public sealed record SaveSystemParametersCommand(decimal AuthorizationLimitCrc, int OverdueAlertDays);

/// <summary>Proyección administrativa de los parámetros del sistema.</summary>
public sealed record SystemParametersModel(
    decimal AuthorizationLimitCrc,
    int OverdueAlertDays,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string UpdatedByUserName,
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
}
