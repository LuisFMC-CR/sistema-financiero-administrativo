# ADR-0048: Consulta general de asientos contables y retiro del relleno manual

- **Fecha:** 2026-11-04
- **Estado:** Aceptado

## Contexto

El bloque 5 del paquete de mejoras finales ya generaba los asientos contables de partida doble dentro
de la misma transacción de cada operación (`JournalPostingService`), y la pantalla
`/contabilidad/asientos` solo ofrecía un comando explícito —"Generar asientos pendientes"— para
documentos anteriores a ese bloque, visible únicamente para Gerencia
(`SystemPolicies.ManageSystemParameters`).

Esa pantalla daba la impresión de que los asientos no eran automáticos: mostraba un botón de
generación y ninguna lista. El usuario pidió reemplazarla por una consulta general de los movimientos
contables, con el estilo de un mockup (dos métricas de cabecera, filtros de período y estado, y una
lista de asientos con su monto y su estado), y confirmó que las operaciones que deben generar el
asiento automáticamente son: facturas de venta, compras, pagos, notas de crédito y reversiones.

Las notas de crédito aún no existen como módulo (bloque 7 del paquete v7), así que su asiento no puede
generarse todavía.

## Decisiones

### 1. La generación automática se confirma; no se agrega ningún comando nuevo

Se verificó que `InvoiceService.ConfirmAsync`, `ExpenseService.ConfirmAsync`,
`ReceivablePaymentService.RegisterAsync`, `IncomeService.CreateManualAsync`,
`SupplierPaymentService.RegisterAsync` y `BalanceAdjustmentService` invocan
`JournalPostingService` antes de guardar, dentro de la misma transacción, y que sus anulaciones
invocan `JournalPostingService.ReverseAsync`. No se cambia esa lógica: el cambio es de pantalla y de
alcance del comando de relleno.

### 2. La pantalla pasa a ser una consulta general por mes y estado

`JournalEntriesController.Index` recibe el mes (`periodYear`/`periodMonth`) y el estado
(`JournalEntryStateFilter`: todos, contabilizados o reversiones) y muestra, para el mes elegido:

- el conteo de asientos registrados y el total de movimientos del período (la suma de los débitos);
- cada asiento con su número de presentación `ASI-000000`, fecha, descripción, monto y estado
  (Contabilizado o Reversión);
- las líneas de cada asiento (cuenta, débito y crédito) dentro de un bloque expandible, para que la
  consulta muestre las transacciones que afectan el asiento sin abrir otro documento.

La consulta se implementa en `IJournalEntryService.SearchAsync`, que lee `JournalEntry` con sus líneas
y resuelve los nombres de cuenta; no reutiliza `IReportService.GetJournalBookReportAsync` porque ese
reporte no expone el estado de reversión ni el identificador del documento de origen.

### 3. Se retira el comando de relleno de documentos previos

Se eliminan el botón, la acción `JournalEntriesController.Backfill`, el método
`IJournalEntryService.BackfillAsync`, el record `JournalEntryBackfillResult` y la prueba
`SqlServerJournalEntryScenarioTests.BackfillAsync_RegeneratesAMissingEntryOnceAndStaysIdempotent`. El
usuario confirmó que la pantalla queda solo como consulta y que, si aparecieran datos antiguos sin
asiento, se decide aparte.

### 4. Acceso: Gerencia y Finanzas

La pantalla deja de exigir `SystemPolicies.ManageSystemParameters` y pasa a
`SystemPolicies.ViewFinancialReports` (Gerencia y Finanzas), la misma política que los reportes
contables, porque muestra cuentas y saldos. En el menú, el enlace "Asientos contables" sale de
"Mantenimiento" y se ubica bajo "Control Financiero Contable", visible cuando `canViewFinancialReports` es verdadero.

### 5. Notas de crédito: su asiento queda para su propio bloque

Como el módulo de notas de crédito no existe todavía (bloque 7 del paquete v7), no se agrega ahora su
tipo de origen ni su regla de contabilización. Cuando se construya, deberá registrar su asiento con
`JournalPostingService` en la misma transacción, igual que los demás documentos, y agregar
`JournalEntrySourceType.CreditNote` (el 7 ya está reservado en el modelo aprobado).

## Consecuencias

- Nuevo: `JournalEntriesIndexViewModel`, `Views/JournalEntries/Index.cshtml` rediseñada,
  `JournalEntryStateFilter`, `JournalEntryListItem`, `JournalEntryListResult`,
  `JournalEntryListLine`.
- Modificado: `IJournalEntryService` y `JournalEntryService` (nueva `SearchAsync`, sin
  `BackfillAsync`), `JournalEntriesController` (consulta con período y estado, sin `Backfill`),
  `_Layout.cshtml` (enlace bajo Control Financiero Contable), `site.css` (estilo de las filas expandibles).
- Retirado: `JournalEntryBackfillResult`, la acción `Backfill` y su prueba de escenario.
- Pruebas: `JournalEntriesAuthorizationTests` ahora exige `Reportes.Ver` para la consulta y ya no
  incluye la ruta `generar-pendientes`; `SqlServerJournalEntryScenarioTests` reemplaza la prueba del
  relleno por `SearchAsync_ReturnsTheConfirmedInvoiceEntryWithItsLinesTotalAndPeriodSummary`.
- `docs/02-requisitos-trazabilidad.md` agrega la matriz del cambio (RF-ASI-001 a RF-ASI-003).
