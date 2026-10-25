# ADR-0019: Numeración de la factura al confirmar

- **Fecha:** 2026-10-15
- **Estado:** Aceptado
- **Complementa:** [ADR-0009](ADR-0009-facturacion-administrativa.md),
  [ADR-0010](ADR-0010-condicion-de-pago-redondeo-y-edicion-de-borradores.md),
  [ADR-0018](ADR-0018-codigos-automaticos-por-secuencia.md)

## Contexto

`Invoice.Number` era una columna identity: SQL Server le asignaba un valor al insertar el borrador. La
regla de negocio 12 y el requisito RF-34 del documento académico exigen que el consecutivo se asigne
solo al confirmar, para que un borrador descartado no deje huecos en la numeración visible y para que
la numeración se comporte igual que los demás códigos automáticos del sistema (ADR-0018).

## Decisión

- `Invoice.Number` pasa de `long` a `long?`. Un borrador no tiene número.
- `Invoice.Confirm` recibe un tercer parámetro, `number`, que valida mayor que cero y asigna de forma
  permanente; el dominio no lo vuelve a cambiar ni lo recalcula. El dominio no conoce la base de datos,
  así que no genera el número por sí mismo: quien llame a `Confirm` (hoy las pruebas, luego
  `IInvoiceService`) debe obtenerlo antes.
- La columna deja de ser identity: SQL Server no permite quitar esa propiedad con `ALTER COLUMN`, así
  que la migración suelta la columna `Number` y la vuelve a crear como `bigint NULL` (no había facturas
  en la base real, así que no hay datos que perder).
- El índice único `UX_Facturas_Number` pasa a ser filtrado (`WHERE [Number] IS NOT NULL`), igual que
  `UX_Clientes_Identification` antes de volverse obligatoria: varios borradores sin número conviven sin
  violar la unicidad.
- Se agrega la secuencia `finanzas.Seq_FacturaNumero` (arranca en 1: no había facturas previas) con el
  mismo mecanismo de `CatalogPersistence.GenerateSequentialCodeAsync` (comando ADO.NET directo, porque
  SQL Server no permite `NEXT VALUE FOR` dentro de una subconsulta). Como todavía no existe
  `IInvoiceService`, las pruebas de integración obtienen el número con una copia local de ese mismo
  patrón (`SqlServerInvoiceScenarioTests.GetNextInvoiceNumberAsync`), documentada para que el futuro
  servicio la reemplace por la versión compartida.

## Consecuencias

- Migración `AddInvoiceNumberSequence`: crea la secuencia, suelta y recrea la columna `Number` sin
  identity, y reemplaza el índice único por su versión filtrada.
- `InvoiceConfirmationTests` e `InvoiceDraftEditingTests` pasan un número explícito a `Confirm` y cubren
  el caso de número no positivo. `SqlServerInvoiceScenarioTests` agrega un escenario que confirma dos
  borradores y comprueba números distintos y consecutivos.
- Cuando se construya `IInvoiceService`, debe generar el número con el mismo mecanismo (secuencia +
  comando ADO.NET directo) en vez de con el `SqlQueryRaw` que falla en subconsultas.
- `docs/02-requisitos-trazabilidad.md` agrega `RF-34` a la matriz de facturación.
