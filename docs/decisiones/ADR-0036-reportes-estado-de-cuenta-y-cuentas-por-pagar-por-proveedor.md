# ADR-0036: Reportes de estado de cuenta de cliente y cuentas por pagar por proveedor

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Bloque 6 del paso 5 de `CLAUDE.md` (regla de negocio 8), el último: los dos reportes que faltaban.
Ninguno de `IReceivableService`, `IReceivablePaymentService` ni `IPayableService` expone exactamente
lo que cada reporte necesita (todo el historial de un cliente; el saldo agrupado por proveedor), así
que `ReportService` sigue el mismo criterio que el resto del módulo: consulta `FinancialDbContext`
directamente en vez de agregar métodos nuevos a esos servicios.

## Decisión

- **Estado de cuenta de cliente**: se elige un cliente (`ICustomerCatalogService.GetActiveOptionsAsync`,
  mismo patrón que el selector de categoría del bloque 4). El reporte junta en una sola línea de
  tiempo cronológica las `Receivable` de ese cliente (cualquier estado: regla 4, "sigue visible en
  consultas e historial") y sus `ReceivablePayment`, cada una marcada si está anulada.
  **No calcula un saldo que corre línea por línea** (un libro contable): eso está explícitamente fuera
  del alcance del sistema (partida doble y libros, sección "Fuera del sistema" de `CLAUDE.md`). En su
  lugar, muestra el historial y un solo saldo actual: la suma de `Balance` de las cuentas por cobrar
  no anuladas de ese cliente. "Sin datos": el cliente no tiene ninguna factura a crédito.
- **Cuentas por pagar por proveedor**: sin parámetros, una fotografía del momento de todas las cuentas
  por pagar vigentes (Pendiente o Parcialmente pagada), agrupadas por proveedor: cantidad, saldo total
  y la fecha de vencimiento más próxima. "Sin datos": no hay ninguna cuenta por pagar vigente.
- Ambos siguen el mismo patrón que los reportes anteriores: pantalla con filtros y resultado, más
  `/pdf` y `/excel`; sin datos, no se genera archivo y se vuelve a la pantalla con mensaje (regla 8).

## Consecuencias

- Nuevo en `Application/Reports/ReportContracts.cs`: `CustomerStatementLine`,
  `CustomerStatementReportResult`, `PayableBySupplierReportLine`, `PayableBySupplierReportResult`, y
  los dos métodos nuevos de `IReportService`.
- Nuevo: `Web/Reporting/{CustomerStatementReportPdfDocument,PayableBySupplierReportPdfDocument}.cs`,
  dos métodos nuevos en `ReportExcelExporter`,
  `Views/Reports/{CustomerStatement,PayableBySupplier}.cshtml`.
- Modificado: `Infrastructure/Reports/ReportService.cs` (los dos métodos nuevos, consultando
  `Receivable`, `ReceivablePayment`, `Withholding`, `Invoice`, `Customer`, `Payable` y `Supplier`
  directamente), `Web/Controllers/ReportsController.cs` (seis rutas nuevas, y la dependencia nueva
  `ICustomerCatalogService` para el selector de cliente), `Views/Reports/Index.cshtml` (dos enlaces
  nuevos), `Web/Models/Reports/ReportViewModels.cs`.
- `docs/02-requisitos-trazabilidad.md` agrega RF-CON-007 y RF-CON-008.
- Verificación: cinco pruebas nuevas en `SqlServerReportServiceScenarioTests` (historial con factura y
  abono y su saldo actual, factura anulada visible en el historial pero fuera del saldo, cliente sin
  facturas, cuentas por pagar agrupadas excluyendo las pagadas, sin cuentas por pagar vigentes) y
  cuatro en `ReportExportTests` (PDF y Excel de cada reporte con datos sintéticos). 269 pruebas
  unitarias y 266 de integración pasan.
- Verificación manual: la app arranca y `/reportes/estado-cuenta-cliente` y
  `/reportes/cuentas-por-pagar-proveedor` exigen inicio de sesión.
- Con este bloque termina completo el paso 5 de `CLAUDE.md`. Sigue el paso 6 (anulaciones y ajustes de
  saldo, y validación de la fecha de cierre en todas las operaciones), que se diseñará aparte porque
  modifica flujos ya construidos en varios módulos a la vez.
