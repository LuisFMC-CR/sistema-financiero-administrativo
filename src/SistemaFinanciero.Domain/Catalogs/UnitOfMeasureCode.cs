namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Lista fija de unidades de medida para <see cref="CatalogItem"/> (bloque 3 del paquete de mejoras
/// finales), en vez de un texto libre.
/// </summary>
/// <remarks>
/// Son los cuatro valores de uso común que el propio bloque 3 cita como ejemplo ("unidad, hora,
/// servicios profesionales, otros servicios"); no se confirmó contra el catálogo oficial "Unidades de
/// Medida" del Anexo 4.4 de Hacienda (TRIBU-CR), así que queda pendiente de validar frente a ese
/// documento antes de usarse para un envío real.
/// </remarks>
public enum UnitOfMeasureCode
{
    /// <summary>Unidad.</summary>
    Unit = 1,

    /// <summary>Hora.</summary>
    Hour = 2,

    /// <summary>Servicios profesionales.</summary>
    ProfessionalService = 3,

    /// <summary>Otros servicios.</summary>
    OtherService = 4,
}

/// <summary>
/// Etiqueta en español de cada <see cref="UnitOfMeasureCode"/>; es el texto que
/// <c>InvoiceService.AddLineAsync</c>/<c>ReplaceLineAsync</c> congela en <c>InvoiceLine.UnitOfMeasure</c>
/// (una fotografía de texto, igual que la descripción), así que vive en Domain y no en Web: tanto
/// Infrastructure como Web la necesitan.
/// </summary>
public static class UnitOfMeasureCodeExtensions
{
    public static string ToLabel(this UnitOfMeasureCode code) => code switch
    {
        UnitOfMeasureCode.Unit => "Unidad",
        UnitOfMeasureCode.Hour => "Hora",
        UnitOfMeasureCode.ProfessionalService => "Servicios profesionales",
        UnitOfMeasureCode.OtherService => "Otros servicios",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unidad de medida desconocida."),
    };
}
