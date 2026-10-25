# ADR-0031: Pantalla de presupuesto y alerta al confirmar un gasto

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Segundo y último bloque del paso 4 de `CLAUDE.md`. El bloque anterior (ADR-0030) dejó listos
`IBudgetService` e `IExpenseService.CheckBudgetAsync`; faltaba construir la pantalla de presupuesto y
la alerta no bloqueante al confirmar un gasto (regla de negocio 6).

## Decisión

- **La alerta es un banner informativo en `ExpensesController.Confirm`**, no una condición que cambie
  el resultado de `ConfirmAsync`: confirmado con el usuario antes de este bloque (el UML de
  `CLAUDE.md` lista `CheckBudgetAsync` como un método propio, separado de `ConfirmAsync`). Se llama al
  mostrar la pantalla (GET) y de nuevo cada vez que se vuelve a renderizar (POST con errores de
  validación o con un resultado fallido, p. ej. límite de autorización): el formulario nunca se
  bloquea por presupuesto.
- **`BudgetsController` es una sola política (`Reportes.Ver`) para ver y editar**, a diferencia de
  `SystemParametersController` (exclusivo de Gerencia): la tabla de perfiles da el presupuesto a
  Finanzas igual que a Gerencia.
- **La edición es por categoría y mes, llegando siempre desde el listado** (`Edit(categoryId, year,
  month)`), sin selector de categoría propio: evita que se edite una categoría que no sea de último
  nivel por error de formulario, porque el listado solo ofrece categorías hoja.
- **El menú lateral agrega "Presupuesto" como módulo propio**, no dentro de "Mantenimiento": la
  Tabla 4 de `CLAUDE.md` lo lista como módulo independiente, junto a Facturación, Gastos, etc.

## Consecuencias

- Nuevo: `BudgetsController` (`/presupuesto`), `Views/Budgets/{Index,Edit}.cshtml`,
  `Models/Budgets/BudgetViewModels.cs`.
- Modificado: `ExpenseConfirmViewModel` (campos informativos `HasBudget`, `BudgetedAmount`,
  `AvailableAmountBeforeThisExpense`, `ExceedsAvailableBudget`), `ExpensesController.Confirm`
  (GET/POST, agrega `PopulateBudgetCheckAsync`), `Views/Expenses/Confirm.cshtml` (banner), menú
  lateral (`_Layout.cshtml`).
- `docs/02-requisitos-trazabilidad.md` agrega RF-PRE-001 a RF-PRE-003.
- Verificación: `BudgetsAuthorizationTests` (sin SQL Server). 269 pruebas unitarias y 238 de
  integración pasan contra SQL Server real.
- Verificación manual: se confirmó que la aplicación arranca y que `/presupuesto` y
  `/presupuesto/{categoryId}/editar` exigen inicio de sesión. No se hizo un recorrido completo en el
  navegador con una cuenta real de Gerencia o Finanzas, misma razón documentada desde ADR-0026.
- Con esto cierra el paso 4 completo del roadmap de `CLAUDE.md`. El siguiente paso es el 5
  (Consultas, indicador de vencidas y reportes con exportación).
