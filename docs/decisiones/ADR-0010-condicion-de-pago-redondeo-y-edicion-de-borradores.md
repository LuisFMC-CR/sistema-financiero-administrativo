# ADR-0010: Condición de pago, redondeo, base gravable y edición de borradores de factura

- **Fecha:** 2026-09-26
- **Estado:** Aceptado
- **Complementa:** [ADR-0009](ADR-0009-facturacion-administrativa.md)

## Contexto

El dominio y la persistencia de facturas del ADR-0009 quedaron incompletos para el flujo del dinero:
no distinguían una venta de contado de una a crédito, redondeaban a cuatro decimales, aceptaban los
datos de un impuesto desde fuera sin conservar su base y solo permitían agregar líneas. Antes de
construir los servicios y las pantallas de facturación era necesario cerrar estas reglas.

## Decisión

### Condición de pago y vencimiento

- Toda factura es de contado (`Cash`) o a crédito (`Credit`).
- Una factura de contado no tiene fecha de vencimiento; se rechaza en lugar de ignorarse, para no
  guardar datos contradictorios.
- Una factura a crédito exige fecha de vencimiento y esta no puede ser anterior a la de emisión.
- La regla vive en el dominio y se repite en la base con `CK_Facturas_PaymentTerm` y
  `CK_Facturas_DueDate`.

### Redondeo

- Importes de línea, impuestos, totales y descuentos se redondean a **2 decimales** con
  `MidpointRounding.AwayFromZero`.
- Cantidades, precios unitarios y tarifas de impuesto conservan **4 decimales**, porque existen tarifas
  como 0,5 %. La tasa CRC por USD conserva **6**.
- El redondeo se define una sola vez en `DomainRules`. Las columnas de importes son `decimal(18,2)`.
- El total de una factura es la suma de sus líneas ya redondeadas.

### Base gravable de los impuestos

- La base gravable de un impuesto es el neto de su línea (bruto menos descuento) y la calcula la
  propia línea. Ningún código externo puede indicarla.
- La línea recibe los datos del tipo de impuesto mediante `InvoiceTaxSpecification` (código, nombre,
  método y tarifa) y conserva la fotografía en `InvoiceLineTax`, junto con la base y el monto. Así el
  dominio no depende de una entidad de catálogo.
- Ambos métodos de cálculo guardan el neto como base; el de monto fijo por unidad calcula su importe con
  la cantidad.
- La fotografía se toma al crear la línea, mientras la factura es borrador. Cuando exista el catálogo de
  impuestos se agregará una referencia opcional `TaxTypeId` sin cambiar estas reglas.

### Edición de borradores

- Mientras la factura es borrador se puede modificar el encabezado con un único método que valida el
  conjunto completo (cliente, fecha de emisión, moneda, condición de pago y vencimiento) antes de
  asignar nada. Esto evita estados intermedios inválidos, como un vencimiento anterior a la nueva
  emisión.
- **La moneda no se puede cambiar cuando la factura ya tiene líneas**, porque sus precios están
  expresados en la moneda original.
- La **fecha de emisión es editable en borrador** y queda inmutable al confirmar. Un borrador no genera
  ingresos, cuentas por cobrar ni reportes, de modo que corregirla no produce inconsistencias.
- Las líneas se agregan, se reemplazan y se quitan. La línea nueva de un reemplazo tiene un
  identificador distinto: la línea es inmutable y así se evitan conflictos del seguimiento de cambios
  de EF Core.
- Cada línea tiene una **posición** consecutiva desde 1 (`FacturaLineas.Position`), única por factura.
  La factura la administra: agregar asigna la siguiente, reemplazar conserva la posición de la línea
  sustituida y quitar renumera las siguientes. La colección `Lines` siempre se entrega ordenada por
  posición, porque EF Core carga las líneas en el orden que devuelva la base de datos.
- Las líneas y sus impuestos declaran `ValueGeneratedNever` en su `Id`. El dominio asigna los
  identificadores y, sin esa declaración, EF interpreta una línea nueva agregada a una factura ya
  guardada como existente e intenta actualizarla en lugar de insertarla.
- Confirmadas o anuladas, las facturas rechazan cualquier edición.
- Las operaciones validan la auditoría (instante UTC y usuario) antes de modificar, para no dejar
  cambios sin registrar.

### Tasa de cambio

- La tasa CRC por USD se fija al confirmar y se guarda en la factura. Actualizaciones posteriores de la
  tasa diaria no alteran facturas confirmadas.
- El servicio de confirmación usará la tasa vigente el día en que se confirma, no la de la fecha de
  emisión. Un borrador con fecha anterior conserva, entonces, la tasa del día de su confirmación.
- La conexión con una fuente externa para actualizar la tasa cada día no forma parte de esta decisión
  y requerirá su propio ADR.

## Alternativas consideradas

- **Fecha de emisión inmutable desde la creación.** Se descartó: los documentos financieros no se
  eliminan y solo se anula lo confirmado, por lo que un borrador con la fecha mal digitada no tendría
  corrección posible. Exigiría además una anulación de borradores, que cambiaría la regla de estados.
- **Métodos separados por campo del encabezado.** Se descartaron porque permiten combinaciones
  inválidas durante la edición.
- **Recibir la base gravable desde fuera.** Se descartó porque permitía guardar una base distinta al
  neto de la línea.

## Consecuencias

- Tres migraciones modifican `finanzas.Facturas`, `FacturaLineas` y `FacturaLineaImpuestos`: precisión
  de importes, condición de pago y base gravable. Como aún no había facturas guardadas, los valores por
  defecto solo sirven para filas antiguas: contado para la condición de pago y cero para la base.
- Los impuestos de una línea de borrador no se actualizan solos si el catálogo cambia después. Para
  refrescarlos se reemplaza la línea. Si el servicio de confirmación debe revalidarlos contra el
  catálogo vigente queda pendiente de decidir cuando se construya.
- Las reglas se prueban en el dominio (`InvoicePaymentTermTests`, `InvoiceRoundingTests`,
  `InvoiceLineTaxTests`, `InvoiceConfirmationTests` e `InvoiceDraftEditingTests`). Contra SQL Server,
  `SqlServerInvoiceScenarioTests` verifica que reemplazar y quitar líneas no deja filas huérfanas, que
  el encabezado y la confirmación se guardan, y que las restricciones `CHECK` rechazan datos inválidos.
- Una migración adicional agrega `Position`, con `CHECK` mayor que cero e índice único por factura.
  Numera por factura las líneas que ya existieran. Quitar una línea actualiza las posiciones de las
  siguientes dentro del índice único; `SqlServerInvoiceScenarioTests` lo verifica contra SQL Server.
- El índice `IX_FacturaLineas_InvoiceId` queda redundante frente al índice único por
  `(InvoiceId, Position)`. Se conserva para no eliminar objetos existentes sin necesidad, y puede
  retirarse en una limpieza posterior.
- `Confirm` y `Cancel` validan todos sus datos antes de asignar, de modo que un dato inválido deja el
  documento en su estado anterior.
