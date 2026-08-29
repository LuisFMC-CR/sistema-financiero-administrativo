using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>
/// Modelo de listado para catálogos que admiten activación y baja lógica.
/// </summary>
public sealed record CatalogIndexViewModel<T>(
    PagedResult<T> Results,
    string? Search,
    CatalogStatusFilter Status);

/// <summary>
/// Modelo de listado para registros paginados sin estado activo o inactivo.
/// </summary>
public sealed record PagedIndexViewModel<T>(
    PagedResult<T> Results,
    string? Search);

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

