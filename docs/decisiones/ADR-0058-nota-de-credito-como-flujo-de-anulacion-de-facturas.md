# ADR-0058: Nota de crédito como flujo de anulación de facturas

- **Fecha:** 2026-11-08
- **Estado:** Aceptado
- **Reemplaza parcialmente:** ADR-0009 y ADR-0023, solo respecto a la anulación de facturas.

## Contexto

La anulación directa de una factura confirmada no documentaba de forma suficiente la corrección de
una venta real ni permitía registrar una devolución de efectivo. Las notas de crédito deben conservar
la factura, sus líneas, sus abonos y la trazabilidad contable, sin modificar ni borrar el asiento
original.

## Decisión

- Finanzas y Gerencia registran notas de crédito desde «Notas de crédito» en el menú lateral o desde
  el detalle de una factura confirmada. La interfaz web no expone una acción directa para anular la
  factura.
- La nota es total. En una operación a crédito aplica todo el saldo pendiente, devuelve en efectivo
  únicamente los abonos recibidos y revierte las retenciones en su cuenta de activo; los abonos y sus
  referencias permanecen como historial. En una factura de contado devuelve el total facturado.
- Si la devolución supera el límite de autorización, solo Gerencia puede registrarla. Toda devolución
  exige una cuenta de efectivo y una referencia; una referencia repetida se advierte, pero no bloquea.
- La nota y sus líneas son inmutables y no se anulan. En una sola transacción el sistema guarda la
  nota, anula la factura y su ingreso de contado (si aplica), liquida la cuenta por cobrar (si aplica),
  registra la bitácora y crea el asiento contable de la nota.
- El asiento original no se modifica ni se reversa directamente: el asiento de nota acredita las
  cuentas de efectivo, cuentas por cobrar y retenciones, y debita devoluciones y el IVA por pagar.
- La nota no se registra si la cuenta por cobrar tiene ajustes de saldo o si el saldo, los abonos y
  las retenciones no concilian con el total original. Los ajustes deben corregirse primero mediante
  otro ajuste en sentido contrario.

## Consecuencias

- La factura conserva el número y el motivo de anulación; la nota conserva el motivo, el desglose de
  las líneas, la aplicación al saldo, la devolución y la referencia.
- `CreditNoteService.RegisterAsync` implementa el flujo y genera un asiento de origen
  `JournalEntrySourceType.CreditNote`. `InvoicesController` ya no publica las rutas de anulación
  directa.
- La consulta contable conserva el asiento original y muestra el asiento de la nota como movimiento
  independiente.
- Se agregan `NotasCredito` y `NotaCreditoLineas`, con índice único por factura y una secuencia
  `NCR-000001`.
