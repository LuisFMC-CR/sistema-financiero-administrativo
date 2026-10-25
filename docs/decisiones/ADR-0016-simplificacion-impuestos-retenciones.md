# ADR-0016: Simplificación de impuestos y retenciones

- **Fecha:** 2026-10-11
- **Estado:** Aceptado
- **Actualiza:** [ADR-0011](ADR-0011-catalogo-de-tipos-de-impuesto.md)
- **Complementa:** [ADR-0013](ADR-0013-catalogo-de-tipos-de-retencion.md)

## Contexto

El ADR-0011 diseñó `TaxType` con dos métodos de cálculo (porcentaje y monto fijo por unidad), una
colección de tarifas por línea de factura (`InvoiceLine.Taxes` / `InvoiceLineTax`) y ocho tarifas de
referencia. Al revisar la regla de negocio 3 de `CLAUDE.md` con el usuario, el alcance real es más
angosto: el sistema solo administra IVA porcentual y una retención de renta que algunos clientes
aplican por ley al pagar. Mantener el método "monto fijo por unidad" y una colección de impuestos por
línea no correspondía a ningún caso de uso aprobado.

## Decisión

### Impuestos

- Se elimina `TaxCalculationType` por completo, junto con el método "monto fijo por unidad"
  (`FixedAmountPerUnit`). Todo impuesto es un porcentaje sobre el neto de la línea.
- `TaxType` pierde `CalculationType`; la tarifa sigue sin ser negativa, con cuatro decimales y tope de
  100 %.
- `ReferenceTaxTypes` se reduce a las dos tarifas que de verdad se precargan: `IVA-13` (13 %) e
  `IVA-EXENTO` (0 %). Las demás tarifas que mencionaba el ADR-0011 no correspondían al alcance
  aprobado.
- `InvoiceLine.Taxes` (colección) se reemplaza por `InvoiceLine.Tax` (una sola tarifa, obligatoria
  incluso cuando es Exento 0 %). `InvoiceLineTax` deja de tener su propio `Id`: se mapea como un tipo
  owned de EF Core (columnas `Tax_Code`, `Tax_Name`, `Tax_Rate`, `Tax_TaxableAmount`, `Tax_Amount`,
  `TaxTypeId` directamente en `finanzas.FacturaLineas`), en vez de una tabla hija
  `FacturaLineaImpuestos`.

### Retenciones

- Se confirma el alcance de ADR-0013: solo la retención que un cliente obligado por ley aplica al
  pagar una factura (por ejemplo, el 2 % de renta de una institución pública). Los tipos de retención
  no se precargan; la empresa define nombre y porcentaje.
- Un pago a proveedor nunca lleva retención.

## Consecuencias

- Migración `SimplifyTaxesAndWithholdings`: elimina `TiposImpuesto.CalculationType` y la tabla
  `FacturaLineaImpuestos`; agrega las columnas `Tax_*`/`TaxTypeId` a `FacturaLineas`.
- Las pantallas de tipos de impuesto (`TaxTypesController` y sus vistas) se simplifican, pero conservan
  el título "Nuevo/Editar tipo de impuesto" en vez de "tarifa de IVA", por consistencia con la pantalla
  de listado y con una prueba de integración existente que verifica ese texto.
- Las facturas con varias tarifas de IVA en la misma línea ya no son posibles: cada línea que necesite
  dos tarifas distintas debe capturarse como dos líneas.
- `docs/02-requisitos-trazabilidad.md` corrige `RN-INV-003`, `RN-IMP-002` y `RN-IMP-003` para dejar de
  mencionar el método de cálculo retirado y el nombre de tabla/FK anterior, y agrega `RN-IMP-005` para
  documentar la tarifa única por línea.
