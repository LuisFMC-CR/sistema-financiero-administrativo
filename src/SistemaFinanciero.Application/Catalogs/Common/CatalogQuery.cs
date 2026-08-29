namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Define búsqueda, estado y paginación para un catálogo con baja lógica.
/// </summary>
public sealed record CatalogQuery
{
    /// <summary>
    /// Inicializa una consulta normalizada y limita el tamaño de página para proteger la base de datos.
    /// </summary>
    public CatalogQuery(
        string? search = null,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        int pageSize = 20)
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional que se buscará en los campos principales.</summary>
    public string? Search { get; }

    /// <summary>Estado lógico que debe incluirse.</summary>
    public CatalogStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}
