# ADR-0028: Pagos a proveedores

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Segundo bloque del paso 3 de `CLAUDE.md`: `SupplierPayment`, el pago a una o varias cuentas por pagar
del mismo proveedor (regla 2), espejo de `ReceivablePayment` del lado de ingresos (ADR-0024).

Antes de empezar se detectó una posible contradicción: el modelo de datos de `CLAUDE.md` decía
"Pagos.ExpenseId siempre y PayableId nulo en gastos de contado", lo que sugería que un gasto de
contado también debería generar una fila en `Pagos` (como `Ingresos` lo hace para una factura de
contado). Esto habría exigido rehacer el bloque 1, ya aceptado y probado. Se confirmó con el usuario
que el diseño del bloque 1 es el correcto: un gasto de contado sigue guardando su cuenta de efectivo y
su referencia directamente en el propio `Expense`, sin generar ningún `Pago`; la frase del modelo de
datos se corrigió en `CLAUDE.md` para reflejarlo.

## Decisión

- **`SupplierPayment` solo existe para pagos a crédito**: siempre referencia un `Payable`
  (`PayableId` fijo, sin columna `ExpenseId`); no hay ninguna fila de `Pagos` para un gasto de contado.
- **Sin retención ni categoría.** A diferencia de `RegisterReceivablePaymentCommand`, el comando de
  registro de un pago no lleva `CategoryId` (no genera ningún ingreso ni documento equivalente: el
  sistema no modela un "egreso" aparte) ni retención ("los pagos a proveedores no llevan retención",
  regla 3).
- **Mismo criterio de límite de autorización que los abonos**, con el mismo parámetro
  `actorIsManagement` en `ISupplierPaymentService.RegisterAsync`; el total se compara contra la suma
  de montos de todo el pago (sin retención que sumar, a diferencia de un abono).
- **La advertencia de referencia duplicada (regla 10) ahora es de tres vías.** `ReceivablePaymentService`
  solo comparaba contra `Abonos` e `Ingresos`; al no existir todavía `Pagos`, nunca pudo detectar una
  referencia repetida contra un pago a proveedor. Se amplió para comparar también contra
  `SupplierPayment`, y el nuevo `SupplierPaymentService` compara contra las tres tablas
  (`Pagos`, `Abonos`, `Ingresos`), cumpliendo la regla 10 completa: "un ingreso, abono o pago".
- **La bitácora de operaciones sí registra cada `SupplierPayment`** (a diferencia de `Payable`, que no
  tiene entrada propia, igual que `Receivable`): se amplió `OperationLogEntityType` con
  `SupplierPayment = 7` y el `CHECK` de `seguridad.BitacoraOperaciones` de `BETWEEN 1 AND 6` a
  `BETWEEN 1 AND 7`.
- **`IPayableService` solo tiene `GetAsync` y `GetPendingBySupplierAsync`** por ahora; `SearchAsync`
  se agrega en el bloque 3 junto con la pantalla que lo necesite, mismo criterio que
  `IReceivableService` en el paso 2.

## Consecuencias

- Nuevo: `Domain/Payables/{SupplierPaymentStatus,SupplierPayment}.cs`,
  `Application/Payables/PayableContracts.cs` (`IPayableService`, `ISupplierPaymentService` y sus
  comandos/modelos), `Infrastructure/Payables/{PayableService,SupplierPaymentService}.cs`,
  `SupplierPaymentConfiguration.cs`.
- Modificado: `OperationLogEntityType`, `OperationLogEntryConfiguration`, `FinancialDbContext`
  (`DbSet<SupplierPayment>`), `DependencyInjection.AddInfrastructure`,
  `ReceivablePaymentService.HasDuplicateReferenceAsync` (ahora también compara contra `SupplierPayment`),
  `CLAUDE.md` (corregida la frase sobre `Pagos.ExpenseId`).
- Migración: `AddSupplierPayments` (tabla `Pagos`, ampliación del `CHECK` de la bitácora).
- `docs/02-requisitos-trazabilidad.md` agrega RF-GAS-005 a RF-GAS-007 y RN-GAS-006 a RN-GAS-008.
- Verificación: `SupplierPaymentTests` (dominio) y `SqlServerSupplierPaymentServiceScenarioTests`
  (pago a una cuenta por pagar, pago a varias con `GroupId` común y anulación individual, límite de
  autorización —rechazado para Finanzas, permitido para Gerencia— y advertencia de referencia
  duplicada). 258 pruebas unitarias y 223 de integración (219 previas + 4 nuevas, estas últimas solo
  con `SISTEMA_FINANCIERO_RUN_SQL_TESTS=1`) pasan contra SQL Server real.
- Pendiente: pantallas web (`ExpensesController`, `PayablesController`, `SupplierPaymentsController`,
  bloque 3 de este mismo paso), con `ISupplierCatalogService.GetActiveOptionsAsync` todavía por
  agregar para el selector de proveedor.
