namespace SistemaFinanciero.Domain.Invoices;

/// <summary>
/// Datos de la tarifa de IVA que una línea de factura aplica y conserva como fotografía.
/// </summary>
/// <remarks>
/// Conexiones: el llamador de <c>InvoiceLine</c> construye este registro, normalmente a partir de
/// <c>TaxTypeOption</c> (lo que devuelve <c>ITaxTypeService.GetActiveOptionsAsync</c>, en
/// Application/Catalogs/TaxTypes), y lo pasa al constructor de <c>InvoiceLine</c>. La línea lo usa para
/// crear su <c>InvoiceLineTax</c>. Como todavía no hay pantalla de facturas, hoy solo lo construyen
/// directamente las pruebas.
/// </remarks>
/// <param name="Code">Código de la tarifa de IVA.</param>
/// <param name="Name">Nombre visible de la tarifa de IVA.</param>
/// <param name="Rate">Porcentaje de IVA.</param>
/// <param name="TaxTypeId">Tipo de impuesto del catálogo de origen, cuando se usó uno.</param>
public sealed record InvoiceTaxSpecification(
    string Code,
    string Name,
    decimal Rate,
    Guid? TaxTypeId = null);
