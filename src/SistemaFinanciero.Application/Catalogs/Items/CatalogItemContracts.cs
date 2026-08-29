using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Application.Catalogs.Items;

/// <summary>Datos editables de un producto o servicio facturable.</summary>
public sealed record SaveCatalogItemCommand(
    string Code,
    string Name,
    CatalogItemType Type,
    string? Description,
    string UnitOfMeasure,
    decimal? ReferencePriceCrc,
    decimal? ReferencePriceUsd,
    Guid? DefaultIncomeCategoryId);

/// <summary>Proyección administrativa de un producto o servicio.</summary>
public sealed record CatalogItemModel(
    Guid Id,
    string Code,
    string Name,
    CatalogItemType Type,
    string? Description,
    string UnitOfMeasure,
    decimal? ReferencePriceCrc,
    decimal? ReferencePriceUsd,
    Guid? DefaultIncomeCategoryId,
    string? DefaultIncomeCategoryName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Casos de uso del catálogo unificado de productos y servicios.</summary>
public interface ICatalogItemService
{
    /// <summary>Busca productos y servicios con filtros y paginación.</summary>
    public Task<PagedResult<CatalogItemModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto o servicio por identificador.</summary>
    public Task<CatalogItemModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea un producto o servicio.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        SaveCatalogItemCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza un producto o servicio.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        SaveCatalogItemCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva un producto o servicio.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
