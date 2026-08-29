namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Resultado inmutable de una consulta paginada ejecutada en el servidor.
/// </summary>
/// <typeparam name="T">Tipo de fila devuelta.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>Inicializa una página y calcula la cantidad total de páginas.</summary>
    public PagedResult(IReadOnlyList<T> items, int page, int pageSize, int totalCount)
    {
        ArgumentNullException.ThrowIfNull(items);

        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCount);

        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalCount = totalCount;
    }

    /// <summary>Filas pertenecientes a la página solicitada.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Número de la página actual.</summary>
    public int Page { get; }

    /// <summary>Tamaño máximo de la página.</summary>
    public int PageSize { get; }

    /// <summary>Cantidad total de filas que cumplen el filtro.</summary>
    public int TotalCount { get; }

    /// <summary>Cantidad total de páginas.</summary>
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>Indica si existe una página anterior.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Indica si existe una página posterior.</summary>
    public bool HasNextPage => Page < TotalPages;
}
