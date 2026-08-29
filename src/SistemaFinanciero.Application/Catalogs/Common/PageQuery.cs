namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Define búsqueda y paginación para registros que no poseen baja lógica.
/// </summary>
public sealed record PageQuery
{
    /// <summary>Inicializa una consulta normalizada con un máximo de cien filas por página.</summary>
    public PageQuery(string? search = null, int page = 1, int pageSize = 20)
    {
        Search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
    }

    /// <summary>Texto opcional que se buscará en los campos principales.</summary>
    public string? Search { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }
}
