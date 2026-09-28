using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Application.Catalogs.TaxTypes;

/// <summary>
/// Tarifas de IVA publicadas por el Ministerio de Hacienda que el sistema ofrece como punto de partida.
/// </summary>
/// <remarks>
/// Son datos de referencia y no reglas de facturación (ADR-0009). Su aplicabilidad depende de la
/// actividad de la empresa y debe validarla su asesoría contable; por eso se cargan solo cuando
/// Finanzas lo solicita y pueden modificarse o desactivarse como cualquier otro tipo de impuesto.
/// </remarks>
public static class ReferenceTaxTypes
{
    private const string ReviewNote =
        "Tarifa de referencia según el Ministerio de Hacienda; validar su uso con la asesoría contable.";

    /// <summary>Tipos de impuesto de referencia, identificados por su código.</summary>
    public static IReadOnlyList<CreateTaxTypeCommand> All { get; } =
    [
        Percentage("IVA-13", "IVA tarifa general 13 %", 13m),
        Percentage("IVA-4", "IVA tarifa reducida 4 %", 4m),
        Percentage("IVA-2", "IVA tarifa reducida 2 %", 2m),
        Percentage("IVA-1", "IVA tarifa reducida 1 %", 1m),
        Percentage("IVA-0.5", "IVA tarifa reducida 0,5 %", 0.5m),
        Percentage("IVA-0", "IVA tarifa 0 %", 0m),
        Percentage("IVA-EXENTO", "IVA exento", 0m),
        Percentage("IVA-0-SIN-CREDITO", "IVA 0 % sin derecho a crédito", 0m),
    ];

    private static CreateTaxTypeCommand Percentage(string code, string name, decimal rate) =>
        new(code, name, TaxCalculationType.Percentage, rate, ReviewNote);
}
