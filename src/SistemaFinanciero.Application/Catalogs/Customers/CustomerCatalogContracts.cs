using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Catalogs.Customers;

/// <summary>Datos editables de un cliente.</summary>
public sealed record SaveCustomerCommand(
    string Code,
    string Name,
    string? Identification,
    string? Email,
    string? Phone,
    string? Address);

/// <summary>Proyección administrativa de un cliente.</summary>
public sealed record CustomerModel(
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

/// <summary>Casos de uso disponibles para el catálogo de clientes.</summary>
public interface ICustomerCatalogService
{
    /// <summary>Busca clientes mediante filtros y paginación en SQL Server.</summary>
    public Task<PagedResult<CustomerModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un cliente por identificador.</summary>
    public Task<CustomerModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea un cliente y registra al usuario responsable.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        SaveCustomerCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza un cliente aplicando concurrencia optimista.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        SaveCustomerCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva un cliente sin eliminarlo.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
