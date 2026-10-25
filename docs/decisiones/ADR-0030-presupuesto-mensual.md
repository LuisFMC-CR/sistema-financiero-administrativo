# ADR-0030: Presupuesto mensual por categoría de gasto

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Primer bloque del paso 4 de `CLAUDE.md`: el presupuesto mensual por categoría de gasto de último
nivel y su historial de cambios (regla de negocio 6). No existía ningún código de `Budget` ni
`BudgetChange` en el repositorio.

## Decisión

- **Lo ejecutado nunca se guarda**: se calcula en caliente sobre `finanzas.Gastos`, filtrando
  `Status = Confirmado` y agrupando por la categoría y por el mes/año de `IssueDate` (no el de
  confirmación). Así, anular un gasto después de confirmado lo saca de inmediato de lo ejecutado sin
  tocar ninguna fila de presupuesto (regla 6: "sin borradores ni anulados; los pagos no lo cambian").
- **Solo se presupuestan categorías de gasto de último nivel** (regla 6: "las superiores solo suman").
  `BudgetService` reutiliza la misma consulta que `FinancialCategoryService` ya usa para bloquear la
  desactivación de una categoría con hijas. La suma de las categorías superiores (rollup del árbol)
  queda para el futuro reporte "presupuesto contra lo ejecutado" del paso 5.
- **Una sola fila por categoría y mes (`Budget`), con su propio historial (`BudgetChange`)**, mismo
  patrón que `SystemParameters`/`SystemParameterChange`: `BudgetChange` guarda el monto anterior (nulo
  en la primera vez), el nuevo, quién y cuándo. `FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated`
  se amplió para protegerla igual que las otras tres bitácoras de solo inserción.
- **`IBudgetService.SetAmountAsync` es un upsert único**, a diferencia de
  `CreateAsync`/`UpdateAsync` separados de `SystemParameters`: crea la fila si no existe (sin monto
  anterior) o la corrige con control de concurrencia si ya existe. Un monto de 0 es válido; solo se
  rechaza un monto negativo.
- **Disponible, variación y variación % son campos calculados en `BudgetModel`**, nunca columnas; sin
  presupuesto configurado quedan todos `null` (la pantalla indicará "sin presupuesto" en el bloque 2);
  con presupuesto en 0, la variación % también queda `null` (división por cero).

## Bug encontrado y corregido

Al probar la anulación de un gasto de contado confirmado (para verificar que lo ejecutado lo excluye
de inmediato), la base de datos rechazó la operación: `CK_Gastos_CashConfirmation` (creado en el
bloque 1 del paso 3) solo permitía `CashAccountId`/`Reference` no nulos cuando `Status = Confirmado`,
pero un gasto anulado (`Status = Anulado`) debe conservar esos datos como historial, igual que una
factura anulada conserva su número (regla 4). Se corrigió el `CHECK` para admitirlos vigentes
("Status IN (Confirmado, Anulado)") en vez de solo "Confirmado" (migración
`FixExpenseCashConfirmationCheckAfterCancel`).

## Consecuencias

- Nuevo: `Domain/Budgets/{Budget,BudgetChange}.cs`, `Application/Budgets/BudgetContracts.cs`,
  `Infrastructure/Budgets/BudgetService.cs`, `BudgetConfiguration.cs`, `BudgetChangeConfiguration.cs`.
- Modificado: `ExpenseContracts`/`ExpenseService` (nuevo `CheckBudgetAsync`, todavía sin pantalla que
  lo consuma), `FinancialDbContext` (`DbSet<Budget>`/`DbSet<BudgetChange>`),
  `DependencyInjection.AddInfrastructure`, `ExpenseConfiguration` (`CK_Gastos_CashConfirmation`
  corregido).
- Migraciones: `AddBudgets` (tablas `Presupuestos`/`PresupuestoCambios`) y
  `FixExpenseCashConfirmationCheckAfterCancel`.
- Verificación: `BudgetTests`, `BudgetChangeTests` (dominio) y `SqlServerBudgetServiceScenarioTests`
  (primera configuración sin monto anterior, corrección con monto anterior, rechazo de una categoría
  con hijas, cálculo de lo ejecutado excluyendo borradores y que un gasto anulado después de
  confirmado deja de contar, y `ExpenseService.CheckBudgetAsync` sin presupuesto / dentro / fuera del
  disponible). 269 pruebas unitarias y 236 de integración pasan contra SQL Server real.
- Pendiente (bloque 2): pantallas (`BudgetsController`), el banner de alerta en
  `ExpensesController.Confirm` y el menú lateral.
