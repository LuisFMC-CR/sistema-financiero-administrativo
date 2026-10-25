using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Application.Catalogs.Items;

/// <summary>
/// Datos para crear un producto o servicio facturable. El código no se incluye porque lo asigna el
/// sistema según <paramref name="Type"/> (regla 12).
/// </summary>
public sealed record CreateCatalogItemCommand(
    string Name,
    CatalogItemType Type,
    string? Description,
    string UnitOfMeasure,
    decimal? ReferencePriceCrc,
    Guid? DefaultIncomeCategoryId,
    Guid? DefaultTaxTypeId);

/// <summary>
/// Datos editables de un producto o servicio existente. Ni el código ni el tipo se incluyen: el
/// código codifica el tipo (<c>PRD-</c>/<c>SRV-</c>) y ambos quedan fijos después de crear (regla 12).
/// </summary>
public sealed record UpdateCatalogItemCommand(
    string Name,
    string? Description,
    string UnitOfMeasure,
    decimal? ReferencePriceCrc,
    Guid? DefaultIncomeCategoryId,
    Guid? DefaultTaxTypeId);

/// <summary>
/// Opción activa para el selector de artículo al agregar una línea de factura.
/// <paramref name="DefaultTaxTypeId"/> permite proponer la tarifa de IVA del producto o servicio
/// (RF-52) sin una segunda consulta; la línea sigue permitiendo cambiarla.
/// </summary>
public sealed record CatalogItemOption(
    Guid Id,
    string Code,
    string Name,
    CatalogItemType Type,
    Guid? DefaultTaxTypeId);

/// <summary>Proyección administrativa de un producto o servicio.</summary>
public sealed record CatalogItemModel(
    Guid Id,
    string Code,
    string Name,
    CatalogItemType Type,
    string? Description,
    string UnitOfMeasure,
    decimal? ReferencePriceCrc,
    Guid? DefaultIncomeCategoryId,
    string? DefaultIncomeCategoryName,
    Guid? DefaultTaxTypeId,
    string? DefaultTaxTypeName,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Casos de uso del catálogo unificado de productos y servicios.</summary>
/// <remarks>
/// Conexiones: lo implementa <c>CatalogItemService</c> (Infrastructure/Catalogs/Items); lo consume
/// <c>CatalogItemsController</c> (Web, ruta <c>/catalogos/productos-servicios</c>).
/// <c>DefaultIncomeCategoryId</c> debe apuntar a una <c>FinancialCategory</c> activa de tipo Ingreso.
/// <c>DefaultTaxTypeId</c> (RF-52) debe apuntar a un <c>TaxType</c> activo; es la tarifa de IVA que se
/// propondrá al agregar el artículo a una línea de factura, editable en la línea misma.
/// </remarks>
public interface ICatalogItemService
{
    /// <summary>Busca productos y servicios con filtros y paginación.</summary>
    public Task<PagedResult<CatalogItemModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un producto o servicio por identificador.</summary>
    public Task<CatalogItemModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista los productos y servicios activos para el selector de una línea de factura.</summary>
    public Task<IReadOnlyList<CatalogItemOption>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Crea un producto o servicio.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateCatalogItemCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza un producto o servicio. El tipo no cambia.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateCatalogItemCommand command,
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
