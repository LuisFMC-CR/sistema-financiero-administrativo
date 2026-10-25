# ADR-0023: Cuentas por cobrar, ingresos y bitácora de operaciones

- **Fecha:** 2026-10-22
- **Estado:** Aceptado

## Contexto

`CLAUDE.md` ordena construir, después de Facturación (ya completa), "cuentas por cobrar, abonos
(incluido a varias facturas) e ingresos" (paso 2 del "Orden de construcción"). Este ADR cubre el
primer bloque de ese paso: `Receivable` (cuenta por cobrar) e `Income` (ingreso), enganchados a
`Invoice.Confirm`/`Cancel`. Los abonos (`ReceivablePayment`), la retención y el reparto a varias
facturas quedan para el bloque siguiente.

Al revisar el estado real del código se encontraron dos vacíos que afectaban directamente este
bloque:

1. Ninguna bitácora de operaciones (`BitacoraOperaciones`) existe todavía: ni siquiera confirmar o
   anular una factura queda registrado ahí (regla 4/16), solo los campos `CreatedBy`/`UpdatedBy` de
   cada documento. La política `SystemPolicies.ViewAuditTrail` ya existe pero no la usa ningún
   controlador.
2. `InvoiceService.ConfirmAsync`/`CancelAsync` no abrían ninguna transacción explícita, algo que
   pasa a ser necesario en cuanto una sola operación debe guardar varias filas relacionadas de forma
   atómica (factura + cuenta por cobrar/ingreso + bitácora).

## Decisión

- **Bitácora de operaciones ahora, no diferida.** Se agrega `OperationLogEntry` (Domain/Audit),
  inmutable igual que `SecurityAuditEvent`/`SystemParameterChange`, y se retrofita
  `InvoiceService.ConfirmAsync`/`CancelAsync` para que escriban ahí además de sus propios campos de
  auditoría. Se eligió hacerlo ahora (en vez de diferirlo) porque `Receivable`/`ReceivablePayment`/
  `Income` también confirman y anulan, y era mejor empezar consistente que retrofitar dos veces.
- **Transacción explícita en confirmar/anular.** `CatalogPersistence.BeginTransactionIfNeededAsync`
  abre una transacción nueva solo si el `DbContext` no está ya dentro de una (las pruebas de
  integración envuelven el escenario completo en su propia transacción de reversión); evita que
  `ConfirmAsync`/`CancelAsync` intenten anidar una transacción EF Core no soportada.
- **`Receivable` sin `Adjust` todavía.** Tiene `ApplyPayment`, `RevertPayment` y `Cancel`, pero no el
  ajuste de saldo de la regla 4: el "Orden de construcción" lo ubica, junto con la validación de
  fecha de cierre, en el paso 6, después de Presupuesto y Consultas. Adelantarlo habría mezclado dos
  bloques del roadmap sin necesidad.
- **`Income` siempre con categoría y cuenta de efectivo**, sin excepción según el origen (regla 2).
  El medio de pago solo se exige en el origen de factura de contado y en el manual (regla 10 solo lo
  pide ahí); un ingreso de abono no lo indica por separado porque ya quedó en la cuenta de efectivo y
  la referencia del abono.
- **Confirmar una factura de contado ahora pide datos nuevos** (cuenta de efectivo, medio de pago,
  referencia, categoría — regla 10) que antes no se pedían porque `Income` no existía. La pantalla de
  confirmar pasó de un botón con un campo oculto a un formulario propio (`GET`/`POST`
  `/facturacion/facturas/{id}/confirmar`); a crédito no pide nada adicional.
- **Anular una factura arrastra su cuenta por cobrar o su ingreso** (regla 4) dentro de la misma
  transacción; si la cuenta por cobrar tiene abonos vigentes, la anulación se rechaza sin tocar nada
  (regla 4: "No se anula un documento con abonos... vigentes sin anular antes esos movimientos").

## Consecuencias

- Nuevas tablas `finanzas.CuentasPorCobrar` e `finanzas.Ingresos` (migración
  `AddReceivablesAndIncomes`) y `seguridad.BitacoraOperaciones` (migración `AddOperationLog`).
  `Ingresos.ReceivablePaymentId` queda sin su clave foránea hasta que exista la tabla `Abonos`.
- `IInvoiceService.ConfirmAsync` cambia de firma: recibe un `ConfirmInvoiceCommand` con los datos de
  contado (todos opcionales, ignorados a crédito).
- `docs/02-requisitos-trazabilidad.md` actualiza RF-INV-003, RF-INV-004, RN-INV-005, RN-INV-006 y
  agrega RF-INV-008 y RN-INV-013.
- Pendiente del bloque siguiente: `ReceivablePayment`, `Withholding`, el reparto a varias facturas con
  `GroupId` común, el ingreso manual y el límite de autorización.
