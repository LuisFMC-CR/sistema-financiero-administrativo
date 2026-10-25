# ADR-0034: Infraestructura de reportes, y reportes de ingresos y de gastos por categoría

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Bloques 3 y 4 del paso 5 de `CLAUDE.md`: la infraestructura común de reportes (regla de negocio 8:
PDF, Excel y pantalla, con nombre, parámetros, fecha y hora y usuario en cada uno) y los dos primeros
reportes, de ingresos por período y de gastos por período y categoría. Se combinan en un solo bloque
porque la infraestructura sola no deja nada demostrable ni probable: cada pieza común (licencia de
QuestPDF, encabezado del reporte, manejo de "sin datos") solo se puede verificar construyendo al menos
un reporte completo.

Antes de escribir cualquier reporte fue necesario resolver un bloqueo de compilación, descrito abajo.

## Decisión: NuGet Audit y las dos versiones fijadas de seguridad

Al agregar `ClosedXML` y `QuestPDF` a `Directory.Packages.props`, `dotnet restore`/`build` empezó a
fallar con `NU1902`/`NU1903` (este repositorio trata las advertencias de seguridad de NuGet como
error). Se diagnosticaron dos problemas distintos, confirmados con `dotnet nuget why`:

- **`System.IO.Packaging` 6.0.0** (CVE-2024-43484), causado por este cambio:
  `ClosedXML → DocumentFormat.OpenXml → System.IO.Packaging`, y también `ClosedXML → System.IO.Packaging`
  directamente. Microsoft confirma que la serie 6.0.x lo corrige en 6.0.1.
- **`System.Security.Cryptography.Pkcs` 9.0.11**, dos CVE de denegación de servicio publicados el
  2026-10-13 (muy recientes). **Confirmado como previo y ajeno a este cambio**: restaurar
  `SistemaFinanciero.Infrastructure.csproj` solo (sin ninguna referencia a ClosedXML/QuestPDF) falla
  igual. La cadena real es `Microsoft.EntityFrameworkCore.SqlServer → Microsoft.Data.SqlClient →
  System.Security.Cryptography.Pkcs`; bloqueaba todo el repositorio porque no existe
  `packages.lock.json` y cada `restore` vuelve a consultar la base de vulnerabilidades de NuGet en vivo.

Se consultó al usuario antes de fijar ninguna versión (CLAUDE.md: no se agregan paquetes ni versiones
sin preguntar). Decisión aprobada: fijar ambas versiones de una vez, en vez de suprimir la auditoría
temporalmente.

- `Directory.Packages.props` agrega `<CentralPackageTransitivePinningEnabled>true</...>` para poder
  fijar la versión de un paquete transitivo (ninguno de los dos se referencia directamente en ningún
  proyecto) sin agregar un `PackageReference` artificial.
- `System.IO.Packaging` queda en **6.0.1** (la versión que Microsoft confirma que corrige la CVE, en
  la misma serie que ya traía ClosedXML).
- `System.Security.Cryptography.Pkcs` queda en **10.0.14**: no se encontró un aviso que confirme una
  versión "parchada" exacta para estas dos CVE de octubre de 2026, así que se eligió la serie del
  propio `net10.0` del proyecto (coherente con que EF Core ya está fijado en 10.0.11), más reciente que
  la 9.0.11 que arrastraba `Microsoft.Data.SqlClient`.

Con los dos overrides, `dotnet restore`/`build`/`test` vuelven a pasar limpios (0 advertencias, 0
errores; 269 pruebas unitarias y 250 de integración, incluidas las que corren contra SQL Server real).

## Decisión: infraestructura común de reportes

- `Program.cs` fija `QuestPDF.Settings.License = LicenseType.Community;` una sola vez al iniciar.
- `Application/Reports/ReportContracts.cs`: `ReportHeader(ReportName, ParametersDescription,
  GeneratedAtUtc, GeneratedByUserName)` es el encabezado común que exige la regla 8; cada resultado de
  reporte lo incluye. `IReportService` tiene un método por reporte; cada uno construye su propio
  `ReportHeader` con `TimeProvider` (igual criterio que el resto de servicios de Infrastructure) y con
  el nombre de usuario que le pasa el controlador.
- `Infrastructure/Reports/ReportService.cs` consulta `FinancialDbContext` directamente, sin pasar por
  los `SearchAsync` de cada módulo: un reporte agrega datos de varias tablas a la vez, no es una
  operación de un solo módulo (mismo criterio que `BudgetService`). Siempre excluye anulados (regla 4);
  el de gastos, además, excluye borradores (solo `Confirmado`, regla 6).
- `Web/Reporting/` tiene un documento QuestPDF (`IDocument`) y un método ClosedXML por reporte, más
  `ReportDocumentStyles` con el encabezado y el estilo de tabla comunes. Las clases de documento y el
  exportador de Excel son públicas (no `internal`, a diferencia de los `*Service` de Infrastructure)
  para que las pruebas de integración los invoquen directamente sin pasar por HTTP ni por el DOM de
  una vista.
- `ReportsController` (`/reportes`), política `Reportes.Ver` (mismo criterio que Presupuesto y
  Consultas: Asistente no entra). Cada reporte tiene una pantalla con filtros y resultado, y dos
  rutas de exportación; cuando no hay datos para los parámetros indicados, no se genera ningún archivo:
  se vuelve a la pantalla con un mensaje (regla 8).

## Decisión: los dos reportes de este bloque

- **Ingresos por período**: todos los ingresos vigentes emitidos entre dos fechas, sin filtrar por
  categoría ni cuenta de efectivo (regla 8 no lo pide); el origen se muestra con el mismo
  `IncomeOrigin.ToDisplayName()` que ya usa la pantalla de Ingresos.
- **Gastos por período y categoría**: gastos confirmados entre dos fechas, agrupados por categoría;
  un selector opcional limita el resultado a una sola categoría (reutiliza
  `IFinancialCategoryService.GetActiveOptionsAsync` para la lista, igual que `ExpensesController`).

## Consecuencias

- Nuevo: `Application/Reports/ReportContracts.cs`, `Infrastructure/Reports/ReportService.cs`,
  `Web/Reporting/{ReportDocumentStyles,IncomeReportPdfDocument,ExpenseByCategoryReportPdfDocument,
  ReportExcelExporter}.cs`, `Web/Controllers/ReportsController.cs`,
  `Web/Models/Reports/ReportViewModels.cs`, `Views/Reports/{Index,Income,ExpenseByCategory}.cshtml`.
- Modificado: `Directory.Packages.props` (QuestPDF, ClosedXML y los dos overrides de seguridad),
  `SistemaFinanciero.Web.csproj` (referencias a los paquetes), `Program.cs` (licencia de QuestPDF),
  `DependencyInjection.cs` (registra `IReportService`), menú lateral (`_Layout.cshtml`).
- `docs/02-requisitos-trazabilidad.md` agrega RF-CON-003 y RF-CON-004.
- Verificación: `SqlServerReportServiceScenarioTests` (agregación real contra SQL Server, incluido el
  caso "sin datos" de cada reporte) y `ReportExportTests` (genera un PDF y un Excel reales con datos
  sintéticos, sin base de datos, para comprobar que la licencia Community de QuestPDF y ClosedXML
  funcionan). 269 pruebas unitarias y 250 de integración pasan.
- Verificación manual: la app arranca sin error de licencia y las cinco rutas nuevas
  (`/reportes`, `/reportes/ingresos`, `/reportes/gastos-por-categoria` y sus `/pdf` y `/excel`) exigen
  inicio de sesión.
- Pendiente: los cuatro reportes restantes (bloques 5 y 6 del paso 5): presupuesto contra lo ejecutado,
  comparativo mensual, estado de cuenta de cliente y cuentas por pagar por proveedor.
