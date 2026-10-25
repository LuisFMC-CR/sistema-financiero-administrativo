namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Estados que puede incluir una consulta administrativa de catálogos.
/// </summary>
/// <remarks>
/// Conexiones: es el filtro de <see cref="CatalogQuery"/>; todo controlador de catálogo (p. ej.
/// <c>CustomersController</c>, <c>TaxTypesController</c>) lo recibe de la URL (<c>?status=</c>) y lo pasa
/// al servicio correspondiente.
/// </remarks>
public enum CatalogStatusFilter
{
    /// <summary>Incluye únicamente registros activos.</summary>
    Active,

    /// <summary>Incluye únicamente registros inactivos.</summary>
    Inactive,

    /// <summary>Incluye registros activos e inactivos.</summary>
    All,
}
