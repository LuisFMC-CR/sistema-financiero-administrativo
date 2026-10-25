# ADR-0020: Plazos de pago fijos y vencimiento calculado

- **Fecha:** 2026-10-16
- **Estado:** Aceptado
- **Complementa:** [ADR-0009](ADR-0009-facturacion-administrativa.md),
  [ADR-0010](ADR-0010-condicion-de-pago-redondeo-y-edicion-de-borradores.md)

## Contexto

`PaymentTerm` solo distinguía `Cash` y `Credit`, y `Invoice.DueDate` lo recibía quien llamara al
constructor o a `UpdateHeader`, validando únicamente que no fuera anterior a la fecha de emisión. La
regla de negocio 14 exige una lista fija de cuatro plazos (Contado, Crédito 30, 60 y 90 días), sin
pantalla de mantenimiento, y que el vencimiento no se digite: el sistema lo calcula como la fecha del
documento más los días del plazo.

## Decisión

- `PaymentTerm` pasa de `{ Cash = 1, Credit = 2 }` a `{ Cash = 1, Credit30 = 2, Credit60 = 3,
  Credit90 = 4 }`. No es un catálogo: sigue siendo un enum fijo sin tabla ni mantenimiento, tal como
  pide la regla 14.
- `PaymentTermDays`, una clase interna nueva en `Domain.Invoices`, traduce cada plazo a sus días de
  crédito (0, 30, 60 o 90). Solo la usa `Invoice`.
- El constructor de `Invoice` y `UpdateHeader` ya no reciben `dueDate`: lo calculan internamente con
  `CalculateDueDate(paymentTerm, issueDate)`, que para `Cash` devuelve `null` y para los demás suma los
  días del plazo a la fecha de emisión. Cualquier dato que el llamador pasara antes para el vencimiento
  deja de tener efecto porque el parámetro ya no existe.
- Como consecuencia, `UpdateHeader` recalcula siempre el vencimiento con la fecha de emisión y el plazo
  nuevos, incluso si ya existía uno: no puede quedar desactualizado.
- `CK_Facturas_PaymentTerm` pasa a `[PaymentTerm] IN (1, 2, 3, 4)` y `CK_Facturas_DueDate` exige una
  igualdad exacta por plazo con `DATEADD` (por ejemplo, `[PaymentTerm] = 2 AND [DueDate] =
  DATEADD(DAY, 30, [IssueDate])`), en vez de solo exigir que no fuera anterior a la emisión. La base
  repite la misma fórmula que el dominio para que ningún dato que no pase por `Invoice` pueda violar la
  regla.

## Alternativas consideradas

- **Guardar los días de cada plazo en una tabla de catálogo.** Se descartó: la regla 14 dice
  explícitamente que la lista es fija y no lleva pantalla de mantenimiento; una tabla sugeriría lo
  contrario y añadiría una dependencia que el dominio no necesita.
- **Seguir recibiendo `dueDate` y solo validar que coincida con el cálculo.** Se descartó: dejaría la
  responsabilidad de calcular bien la fecha en cada llamador (hoy las pruebas, luego
  `IInvoiceService`), en vez de que el dominio la garantice siempre.

## Consecuencias

- Migración `AdjustPaymentTermToFixedTerms`: reemplaza `CK_Facturas_PaymentTerm` y
  `CK_Facturas_DueDate` por las versiones de cuatro plazos. No había facturas en la base real
  (`SELECT COUNT(*) FROM finanzas.Facturas` devolvió 0 antes de aplicarla), así que no hay datos que
  migrar ni perder.
- `InvoicePaymentTermTests` se reescribió: ya no prueba que el dominio rechace una fecha de vencimiento
  inconsistente (ese parámetro no existe), sino que calcule correctamente el vencimiento de cada uno de
  los cuatro plazos. `InvoiceDraftEditingTests` perdió la prueba que validaba un vencimiento antiguo
  contra una nueva emisión (ya no aplica) y ganó una que confirma el recálculo automático.
  `SqlServerInvoiceScenarioTests` usa `PaymentTerm.Credit30` en vez del `Credit` genérico.
- Cuando se construya `IInvoiceService` y sus pantallas, el selector de plazo de pago debe mostrar las
  cuatro opciones fijas (por ejemplo "Contado", "Crédito 30 días", "Crédito 60 días", "Crédito 90
  días") y nunca pedir ni mostrar un campo editable de vencimiento: solo se muestra el resultado del
  cálculo.
- `docs/02-requisitos-trazabilidad.md` actualiza `RN-INV-007` y `RN-INV-010` para describir la lista
  fija y el cálculo automático del vencimiento.
