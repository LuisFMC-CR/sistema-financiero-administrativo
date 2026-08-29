namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Tipos de elementos que pueden incluirse en un documento de venta.
/// </summary>
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
