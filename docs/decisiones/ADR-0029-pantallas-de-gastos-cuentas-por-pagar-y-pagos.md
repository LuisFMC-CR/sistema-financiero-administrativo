# ADR-0029: Pantallas de gastos, cuentas por pagar y pagos

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Tercer y último bloque del paso 3 de `CLAUDE.md`. Los bloques anteriores (ADR-0027, ADR-0028) dejaron
listos `IExpenseService`, `IPayableService` e `ISupplierPaymentService`; faltaba construir las
pantallas sobre ellos: Gastos, Cuentas por Pagar y el registro de pagos (con reparto a varias cuentas),
espejo del cierre del paso 2 (ADR-0026).

## Decisión

- **`Expense` no tiene un asistente de líneas**: a diferencia de `InvoicesController`, `Create` y
  `EditHeader` capturan todo el encabezado de una sola vez (proveedor, categoría, descripción, fecha,
  plazo, monto neto, tarifa de IVA y medio de pago), porque un gasto no tiene líneas (regla 2).
- **`Expense` aplica la restricción de perfiles de Asistente** ("ve solo los suyos", igual que
  facturas): se agregó `restrictToOwnerUserId` a `IExpenseService.GetAsync`/`UpdateHeaderAsync` y
  `ExpenseQuery.CreatedByUserId`, que no existían en el bloque 1 porque todavía no había pantalla que
  lo necesitara (mismo criterio que `IReceivableService.SearchAsync` se agregó hasta el bloque 5 del
  paso 2).
- **`ISupplierCatalogService.GetActiveOptionsAsync`**, identificado como brecha al planear el paso 3,
  se agrega ahora junto con la pantalla que lo necesita, copiando `ICustomerCatalogService`.
- **`ExpenseConfirmViewModel` no pide medio de pago ni categoría**, a diferencia de
  `InvoiceConfirmViewModel`: el medio de pago ya quedó fijo desde el borrador (regla 7) y un gasto de
  contado no genera ningún ingreso que necesite categoría.
- **Las pantallas nuevas (Gastos, Cuentas por Pagar) usan filtros por columna desde el inicio**, el
  mismo patrón que Cuentas por Cobrar e Ingresos adoptaron en el paso 2 tras el pedido explícito del
  usuario; no se construyeron con el patrón antiguo de una sola fila (`_Filters`) que todavía usan
  Facturas y los catálogos, para no tener que rehacerlas después.
- **El registro de un pago es un asistente de dos pasos**, igual criterio que
  `ReceivablePaymentsController` pero sin retención ni categoría: `SupplierPaymentsController.Start`
  (GET) pide proveedor, monto total, cuenta de efectivo, referencia y fecha; `Start` (POST) consulta
  `IPayableService.GetPendingBySupplierAsync` y propone el reparto de la más antigua a la más nueva
  (regla 2); `Register` (ruta `aplicar`, no `registrar`, por la misma prueba de seguridad que ya
  obligó el cambio en abonos) lo registra.
- **Cuentas por Pagar es de solo Gerencia y Finanzas** (política `Reportes.Ver`), igual criterio que
  Cuentas por Cobrar; Gastos usa `Movimientos.Ver`/`Movimientos.CrearBorrador`, igual criterio que
  Facturas, porque Asistente sí participa en el borrador.

## Consecuencias

- Controladores nuevos: `ExpensesController` (`/gastos`), `PayablesController` (`/cuentas-por-pagar`),
  `SupplierPaymentsController` (`/cuentas-por-pagar/pagos`).
- Menú lateral: se agregan "Gastos" y "Cuentas por pagar" junto a "Facturas"/"Cuentas por cobrar"/
  "Ingresos".
- `docs/02-requisitos-trazabilidad.md` agrega RF-GAS-008 y RN-GAS-009, y amplía RF-GAS-001, RF-GAS-002,
  RF-GAS-004, RF-GAS-005 y RF-GAS-006 con sus pantallas.
- Verificación: `ExpensesAuthorizationTests` y `PayablesAuthorizationTests` (sin SQL Server) exigen
  exactamente las políticas de la matriz de perfiles en cada ruta, mismo patrón que
  `InvoiceAuthorizationTests`/`ReceivablesAuthorizationTests`. 258 pruebas unitarias y 230 de
  integración (223 previas + 7 nuevas de autorización) pasan.
- Verificación manual: se confirmó que la aplicación arranca y que las rutas nuevas
  (`/gastos`, `/gastos/crear`, `/cuentas-por-pagar`, `/cuentas-por-pagar/pagos/nuevo`) exigen inicio de
  sesión. No se hizo un recorrido completo en el navegador con una cuenta de Gerencia o Finanzas real,
  por la misma razón documentada en ADR-0026: el entorno de este asistente no tiene esas credenciales y
  modificarlas en la base de datos de desarrollo habría alterado cuentas reales. Queda pendiente que el
  usuario pruebe en el navegador, con sus propias credenciales, el ciclo completo de un gasto (borrador
  → confirmación de contado y a crédito → anulación) y el asistente de pagos a varias cuentas.
- Con esto cierra el paso 3 completo del roadmap de `CLAUDE.md`. El siguiente paso es el 4
  (Presupuesto y alerta), que leerá `Expense.Status == Confirmado` para calcular lo ejecutado.
