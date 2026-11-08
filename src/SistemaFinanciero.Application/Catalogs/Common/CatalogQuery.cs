using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Define filtros por campo, estado y paginación para un catálogo con baja lógica.
/// </summary>
/// <remarks>
/// Conexiones: la construye cada controlador de catálogo en su acción <c>Index</c> (vía
/// <c>CatalogControllerBase.NormalizeSearch</c>/<c>NormalizeStatus</c>) y la recibe
/// <c>SearchAsync</c> de cada <c>I...Service</c> de Catalogs (clientes, proveedores, productos,
/// categorías, cuentas contables, tipos de impuesto, tipos de retención).
/// </remarks>
public sealed record CatalogQuery
{
    /// <summary>
    /// Inicializa una consulta normalizada y limita el tamaño de página para proteger la base de datos.
    /// </summary>
    public CatalogQuery(
        string? search = null,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        int pageSize = 20,
        string? code = null,
        string? name = null,
        string? identification = null,
        string? contact = null,
        CatalogItemType? itemType = null,
        string? cabysCode = null,
        UnitOfMeasureCode? unitOfMeasure = null,
        decimal? referencePrice = null,
        string? taxTypeName = null,
        string? haciendaRateCode = null,
        decimal? rate = null,
        decimal? minimumBase = null)
    {
        Search = Normalize(search, 100);
        Status = status;
        Page = Math.Max(1, page);
        PageSize = Math.Clamp(pageSize, 1, 100);
        Code = Normalize(code, 30);
        Name = Normalize(name, 150);
        Identification = Normalize(identification, 30);
        Contact = Normalize(contact, 254);
        ItemType = itemType;
        CabysCode = Normalize(cabysCode, 20);
        UnitOfMeasure = unitOfMeasure;
        ReferencePrice = referencePrice;
        TaxTypeName = Normalize(taxTypeName, 120);
        HaciendaRateCode = Normalize(haciendaRateCode, 2);
        Rate = rate;
        MinimumBase = minimumBase;
    }

    /// <summary>Texto opcional que se buscará en los campos principales.</summary>
    public string? Search { get; }

    /// <summary>Estado lógico que debe incluirse.</summary>
    public CatalogStatusFilter Status { get; }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }

    /// <summary>Cantidad máxima de registros por página.</summary>
    public int PageSize { get; }

    public string? Code { get; }

    public string? Name { get; }

    public string? Identification { get; }

    public string? Contact { get; }

    public CatalogItemType? ItemType { get; }

    public string? CabysCode { get; }

    public UnitOfMeasureCode? UnitOfMeasure { get; }

    public decimal? ReferencePrice { get; }

    public string? TaxTypeName { get; }

    public string? HaciendaRateCode { get; }

    public decimal? Rate { get; }

    public decimal? MinimumBase { get; }

    private static string? Normalize(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }
}
