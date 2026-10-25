namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Tipos de elementos que pueden incluirse en un documento de venta.
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>CatalogItem.Type</c>; <c>CatalogDisplayExtensions.ToDisplayName</c>
/// (Web) lo traduce a «Producto» o «Servicio» en las pantallas.
/// </remarks>
public enum CatalogItemType
{
    /// <summary>
    /// Bien facturable sin control de inventario.
    /// </summary>
    Product = 1,

    /// <summary>
    /// Servicio facturable.
    /// </summary>
    Service = 2,
}
