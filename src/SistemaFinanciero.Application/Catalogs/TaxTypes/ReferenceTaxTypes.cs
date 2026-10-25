namespace SistemaFinanciero.Application.Catalogs.TaxTypes;

/// <summary>
/// Tarifas de IVA precargadas que la regla de negocio 3 define como punto de partida: 13 % y Exento.
/// </summary>
/// <remarks>
/// Finanzas puede registrar otra tarifa de IVA o actualizar un porcentaje; estas dos son solo el punto
/// de partida, no un límite. Se cargan solo cuando Finanzas lo solicita y pueden modificarse o
/// desactivarse como cualquier otra tarifa.
///
/// Conexiones: <see cref="All"/> la lee <c>TaxTypeService.LoadReferenceTaxTypesAsync</c>, que la llama
/// <c>TaxTypesController.LoadReference</c> cuando Finanzas pulsa «Cargar tarifas de referencia» en
/// <c>/catalogos/tipos-impuesto</c>.
/// </remarks>
public static class ReferenceTaxTypes
{
    /// <summary>Tarifas de IVA de referencia, identificadas por su código.</summary>
    public static IReadOnlyList<CreateTaxTypeCommand> All { get; } =
    [
        new("IVA-13", "IVA tarifa general 13 %", 13m, null),
        new("IVA-EXENTO", "IVA exento", 0m, null),
    ];
}
