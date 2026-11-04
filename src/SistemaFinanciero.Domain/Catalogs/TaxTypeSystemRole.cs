namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Identifica las tarifas de IVA que las reglas del sistema buscan por su rol, nunca por código ni
/// nombre (mismo criterio que <see cref="LedgerAccountSystemRole"/>, regla de negocio 1), porque el
/// código y el nombre de una tarifa son editables. Solo la siembra inicial (bloque 1 del paquete v7)
/// asigna un rol; ninguna tarifa agregada a mano tiene uno.
/// </summary>
public enum TaxTypeSystemRole
{
    /// <summary>IVA tarifa general 13 %: la condición Normal la usa cuando el artículo no tiene una
    /// tarifa predeterminada propia.</summary>
    General13 = 1,

    /// <summary>IVA exento (0 %).</summary>
    Exempt = 2,

    /// <summary>IVA tarifa reducida 2 %: la condición "Institución estatal de educación superior" la
    /// fuerza en todas las líneas.</summary>
    Reduced2 = 3,

    /// <summary>No sujeto: la condición "No sujeto" la fuerza en todas las líneas.</summary>
    NotSubject = 4,
}
