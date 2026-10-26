# ADR-0042: Advertencia de referencia repetida completa (ingreso manual y factura de contado)

- **Fecha:** 2026-10-26
- **Estado:** Aceptado

## Contexto

La regla de negocio 10 dice literalmente "advertir, sin bloquear, si un ingreso, abono o pago repite
la referencia en la misma cuenta de efectivo." Ya estaba implementada para abonos
(`IReceivablePaymentService.RegisterAsync`) y pagos a proveedores
(`ISupplierPaymentService.RegisterAsync`), cada uno con su propia copia casi idéntica de la consulta
contra `Abonos`, `Pagos` e `Ingresos`. Faltaban los otros dos orígenes de un ingreso: el ingreso
manual (`IncomeService.CreateManualAsync`) y la factura de contado al confirmarse (que genera un
ingreso igual que un abono, pero por un camino distinto, `InvoiceService.ConfirmAsync`).

## Decisión

- **`DuplicateReferenceGuard`** (nuevo, `Infrastructure/Common`): la consulta contra las tres tablas
  de movimientos de efectivo (`Abonos`, `Pagos`, `Ingresos`), extraída de las dos copias ya
  existentes en `ReceivablePaymentService` y `SupplierPaymentService`, que ahora la reutilizan en
  vez de repetirla. Es el cuarto lugar que la necesita (con el ingreso manual y la factura de
  contado) lo que justifica el único punto compartido, igual criterio que `ClosingDateGuard`.
- **Ingreso manual**: `IncomeCreationResult` gana `DuplicateReferenceWarning`, igual patrón que los
  resultados de abono y pago; `IncomesController.Create` lo muestra en el mensaje de éxito. Solo se
  comprueba cuando el ingreso trae referencia, porque a diferencia de abonos y pagos, la referencia
  del ingreso manual es opcional.
- **Factura de contado**: cambiar el tipo de retorno de `ConfirmAsync` para llevar la advertencia
  habría tocado los más de quince lugares que ya lo usan (controlador y pruebas de otros módulos que
  confirman una factura solo como paso previo de su propio escenario), un cambio desproporcionado
  para una advertencia informativa. En su lugar, `IInvoiceService` gana un método de solo consulta,
  `HasDuplicateCashReferenceAsync`, que `InvoicesController.Confirm` llama con los mismos datos del
  formulario justo antes de confirmar, sin tocar la firma de `ConfirmAsync` ni ninguno de sus demás
  usos.

## Consecuencias

- Nuevo: `Infrastructure/Common/DuplicateReferenceGuard.cs`.
- Modificado: `ReceivablePaymentService.cs` y `SupplierPaymentService.cs` (usan el guard compartido,
  se elimina su copia privada de la consulta); `IncomeContracts.cs`
  (`IncomeCreationResult.DuplicateReferenceWarning`), `IncomeService.cs`, `IncomesController.cs`;
  `InvoiceContracts.cs` (`IInvoiceService.HasDuplicateCashReferenceAsync`), `InvoiceService.cs`,
  `InvoicesController.cs` (acción `Confirm`).
- `docs/02-requisitos-trazabilidad.md` actualiza RN-INV-015 y RN-GAS-007: ya cubren los cuatro
  orígenes, no solo abono y pago.
- Verificación: 2 pruebas SQL reales nuevas (ingreso manual con referencia repetida advierte sin
  bloquear; `HasDuplicateCashReferenceAsync` detecta la referencia de un ingreso de factura de
  contado y no confunde una referencia distinta). 292 pruebas unitarias y 306 de integración pasan.
- Verificación manual: la app arranca y las rutas de ingresos y facturación siguen exigiendo inicio
  de sesión.
