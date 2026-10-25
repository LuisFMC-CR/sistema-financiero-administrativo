using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Modelo de listado para catálogos que admiten activación y baja lógica.
/// </summary>
/// <remarks>
/// Conexiones: lo construye la acción <c>Index</c> de cada controlador de catálogo a partir del
/// <see cref="PagedResult{T}"/> que devuelve su servicio; lo consumen las vistas <c>Index.cshtml</c>
/// junto con <c>_CatalogFilters.cshtml</c> y <c>_CatalogPagination.cshtml</c>.
/// </remarks>
public sealed record CatalogIndexViewModel<T>(
    PagedResult<T> Results,
    string? Search,
    CatalogStatusFilter Status);

/// <summary>
/// Datos compartidos por el formulario de filtros de los catálogos.
/// </summary>
public sealed record CatalogFilterViewModel(
    string? Search,
    CatalogStatusFilter? Status = null);

/// <summary>
/// Datos necesarios para conservar filtros al navegar entre páginas.
/// </summary>
public sealed record CatalogPaginationViewModel(
    int CurrentPage,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    string? Search,
    CatalogStatusFilter? Status = null);

