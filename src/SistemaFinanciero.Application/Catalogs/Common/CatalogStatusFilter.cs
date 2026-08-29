namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Estados que puede incluir una consulta administrativa de catálogos.
/// </summary>
public enum CatalogStatusFilter
{
    /// <summary>Incluye únicamente registros activos.</summary>
    Active,

    /// <summary>Incluye únicamente registros inactivos.</summary>
    Inactive,

    /// <summary>Incluye registros activos e inactivos.</summary>
    All,
}
