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
/// Desde ADR-0061 solo se eligen <see cref="Unit"/> y <see cref="Hour"/>; los valores 3 y 4 quedan
/// retirados: se conservan únicamente para mostrar artículos antiguos (la restricción CHECK de la base
/// no cambia, porque el sistema congelado no admite migraciones).
/// </remarks>
public enum UnitOfMeasureCode
{
    /// <summary>Unidad.</summary>
    Unit = 1,

    /// <summary>Hora.</summary>
    Hour = 2,

    /// <summary>Servicios profesionales (retirada, ADR-0061: ya no se elige).</summary>
    ProfessionalService = 3,

    /// <summary>Otros servicios (retirada, ADR-0061: ya no se elige).</summary>
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
    /// <summary>Unidades que se pueden elegir al crear o editar un artículo (ADR-0061).</summary>
    public static IReadOnlyList<UnitOfMeasureCode> Selectable { get; } =
        [UnitOfMeasureCode.Unit, UnitOfMeasureCode.Hour];

    /// <summary>Indica si la unidad se puede elegir en un artículo nuevo o editado.</summary>
    public static bool IsSelectable(this UnitOfMeasureCode code) =>
        code is UnitOfMeasureCode.Unit or UnitOfMeasureCode.Hour;

    public static string ToLabel(this UnitOfMeasureCode code) => code switch
    {
        UnitOfMeasureCode.Unit => "Unidad",
        UnitOfMeasureCode.Hour => "Hora",
        UnitOfMeasureCode.ProfessionalService => "Servicios profesionales",
        UnitOfMeasureCode.OtherService => "Otros servicios",
        _ => throw new ArgumentOutOfRangeException(nameof(code), code, "Unidad de medida desconocida."),
    };
}
