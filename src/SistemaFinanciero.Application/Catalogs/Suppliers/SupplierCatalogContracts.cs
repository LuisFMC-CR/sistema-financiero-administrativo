using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Catalogs.Suppliers;

/// <summary>Datos editables de un proveedor.</summary>
public sealed record SaveSupplierCommand(
    string Code,
    string Name,
    string? Identification,
    string? Email,
    string? Phone,
    string? Address);

/// <summary>Proyección administrativa de un proveedor.</summary>
public sealed record SupplierModel(
    Guid Id,
    string Code,
    string Name,
    string? Identification,
    string? Email,
    string? Phone,
    string? Address,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Casos de uso disponibles para el catálogo de proveedores.</summary>
public interface ISupplierCatalogService
{
    /// <summary>Busca proveedores mediante filtros y paginación en SQL Server.</summary>
    public Task<PagedResult<SupplierModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un proveedor por identificador.</summary>
    public Task<SupplierModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea un proveedor y registra al usuario responsable.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        SaveSupplierCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza un proveedor aplicando concurrencia optimista.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        SaveSupplierCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva un proveedor sin eliminarlo.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
