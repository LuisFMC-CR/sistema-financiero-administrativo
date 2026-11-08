# ADR-0056: Abonos individuales y cobros repartidos desde Ingresos

- **Fecha:** 2026-10-28
- **Estado:** Aceptado
- **Reemplaza parcialmente:** ADR-0026, solo respecto al flujo de registro de abonos.

## Contexto

El detalle de una cuenta por cobrar iniciaba el asistente general de abonos, que volvía a pedir el
cliente y permitía repartir el monto entre varias facturas. Esto podía confundir el abono de una
cuenta seleccionada con el cobro distribuido entre varias cuentas.

El ingreso manual tiene una finalidad contable distinta y no debe reutilizarse para aplicar pagos a
facturas: los abonos reducen cuentas por cobrar y generan sus ingresos y asientos relacionados.

## Decisión

- Desde el detalle de una cuenta por cobrar, registrar un abono dirigido exclusivamente a esa cuenta.
  El servidor carga la cuenta desde el identificador de la ruta y deriva de ella el cliente; el
  formulario conserva monto, cuenta de efectivo, fecha, referencia y retención aplicable.
- En Ingresos, ofrecer a Finanzas y Gerencia una acción separada para distribuir un cobro entre
  varias cuentas por cobrar del mismo cliente. Se conserva la propuesta editable de la más antigua a
  la más nueva, la retención por línea, la transacción única, la referencia compartida, el `GroupId`,
  el límite aplicado al total y la anulación individual.
- Mantener sin cambios el ingreso manual, sus políticas y su contabilización. La acción de cobro
  múltiple requiere `Movimientos.Confirmar`; no se concede esa capacidad al Asistente por el hecho de
  que pueda consultar o registrar ingresos manuales.
- Publicar ambos recorridos bajo `/ingresos/abonos`; la selección de una cuenta se expone en
  `/ingresos/abonos/cuenta/{id}` y la distribución múltiple desde `/ingresos/abonos/nuevo`.
- Se mantienen los contratos, validaciones y lógica transaccional de `IReceivablePaymentService`;
  el recorrido individual envía una única asignación, construida con la cuenta cargada por el
  servidor.

## Consecuencias

- El botón de abono en el detalle ya no solicita elegir cliente ni propone repartir.
- La pantalla de Ingresos presenta una acción distinta para distribuir cobros, sin mezclarla con
  «Nuevo ingreso manual».
- Las rutas de autorización para los abonos individuales y múltiples requieren
  `Movimientos.Confirmar`; las anulaciones continúan requiriendo `Movimientos.Anular`.
- ADR-0026 conserva su valor histórico; esta decisión sustituye su descripción de un asistente único
  para todos los abonos.
- La trazabilidad se actualiza en RF-INV-006 y RF-INV-009.
