# ADR-0035: Reportes de presupuesto contra lo ejecutado y comparativo mensual

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Bloque 5 del paso 5 de `CLAUDE.md` (regla de negocio 8): los dos reportes restantes que reutilizan
directamente servicios ya construidos en vez de agregar datos nuevos.

## Decisión

- **Presupuesto contra lo ejecutado** reutiliza `IBudgetService.SearchByPeriodAsync(year, month)` tal
  cual: una fila por categoría de gasto de último nivel, con o sin presupuesto configurado para ese
  mes. `ReportService` gana una dependencia nueva, `IBudgetService`, distinta del resto de reportes
  (que consultan `FinancialDbContext` directamente) porque este caso ya tiene exactamente el cálculo
  que el reporte necesita. El reporte solo agrega el `ReportHeader` y los totales
  (`TotalBudgeted`/`TotalExecuted`, sumando `BudgetedAmount ?? 0`).
- **Comparativo mensual de ingresos y gastos** toma un rango de meses (año/mes inicial y final, no un
  rango de fechas) y construye una fila por cada mes del rango, **incluidos los meses sin
  movimientos** (con 0 en ambos totales): omitirlos rompería la serie comparativa. Por eso, a
  diferencia de los demás reportes, "sin datos" no es `Lines.Count == 0` sino
  `GrandTotalIncome == 0 && GrandTotalExpense == 0`: ningún movimiento real en todo el rango, aunque
  `Lines` tenga una fila en 0 por cada mes.
- Ambos siguen el mismo patrón que los reportes del bloque 4: pantalla con filtros y resultado, más
  `/pdf` y `/excel`; sin datos, no se genera archivo y se vuelve a la pantalla con mensaje (regla 8).

## Consecuencias

- Nuevo en `Application/Reports/ReportContracts.cs`: `BudgetVsExecutedReportResult`,
  `MonthlyComparisonLine`, `MonthlyComparisonReportResult`, y los dos métodos nuevos de
  `IReportService`.
- Nuevo: `Web/Reporting/{BudgetVsExecutedReportPdfDocument,MonthlyComparisonReportPdfDocument}.cs`,
  dos métodos nuevos en `ReportExcelExporter`, `Views/Reports/{BudgetVsExecuted,MonthlyComparison}.cshtml`.
- Modificado: `Infrastructure/Reports/ReportService.cs` (dependencia de `IBudgetService` y los dos
  métodos), `Web/Controllers/ReportsController.cs` (cuatro rutas nuevas), `Views/Reports/Index.cshtml`
  (dos enlaces nuevos), `Web/Models/Reports/ReportViewModels.cs`.
- `docs/02-requisitos-trazabilidad.md` agrega RF-CON-005 y RF-CON-006.
- Verificación: tres pruebas nuevas en `SqlServerReportServiceScenarioTests` (ejecutado desde un gasto
  confirmado real, comparativo que rellena meses sin movimientos, comparativo sin datos en todo el
  rango) y cuatro en `ReportExportTests` (PDF y Excel de cada reporte con datos sintéticos). 269
  pruebas unitarias y 257 de integración pasan.
- Verificación manual: la app arranca y `/reportes/presupuesto-vs-ejecutado` y
  `/reportes/comparativo-mensual` exigen inicio de sesión.
- Pendiente: los dos reportes del bloque 6 (estado de cuenta de cliente y cuentas por pagar por
  proveedor), último bloque del paso 5.
