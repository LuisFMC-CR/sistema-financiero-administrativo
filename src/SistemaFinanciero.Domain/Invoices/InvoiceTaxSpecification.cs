namespace SistemaFinanciero.Domain.Invoices;

/// <summary>
/// Datos de un tipo de impuesto que una línea de factura aplica y conserva como fotografía.
/// </summary>
/// <param name="Id">Identificador técnico de la fotografía que se guardará en la línea.</param>
/// <param name="Code">Código del tipo de impuesto.</param>
/// <param name="Name">Nombre visible del tipo de impuesto.</param>
/// <param name="CalculationType">Método de cálculo del impuesto.</param>
/// <param name="Rate">Porcentaje o monto fijo por unidad, según el método.</param>
/// <param name="TaxTypeId">Tipo de impuesto del catálogo de origen, cuando se usó uno.</param>
public sealed record InvoiceTaxSpecification(
    Guid Id,
    string Code,
    string Name,
    TaxCalculationType CalculationType,
    decimal Rate,
    Guid? TaxTypeId = null);
