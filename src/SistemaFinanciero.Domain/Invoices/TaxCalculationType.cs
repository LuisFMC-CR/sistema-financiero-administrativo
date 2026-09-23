namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Forma en que se calcula un impuesto conservado en una línea de factura.</summary>
public enum TaxCalculationType
{
    /// <summary>La tarifa corresponde a un porcentaje de la base gravable.</summary>
    Percentage = 1,

    /// <summary>La tarifa corresponde a un monto fijo por unidad facturada.</summary>
    FixedAmountPerUnit = 2,
}
