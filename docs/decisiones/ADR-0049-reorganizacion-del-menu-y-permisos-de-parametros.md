# ADR-0049: Reorganización del menú lateral y permisos de parámetros

- **Fecha:** 2026-11-04
- **Estado:** Aceptado

## Contexto

El menú lateral había crecido por incrementos: tenía las secciones "Facturación", "Gastos",
"Catálogos", "Presupuesto", "Consultas", "Reportes", "Mantenimiento" y "Administración", y una
pantalla de Consultas con filtros combinados que, en la práctica, duplicaba los filtros que cada
listado ya tiene. El usuario pidió agrupar el menú por ciclo de trabajo, retirar Consultas por
completo y dar a Finanzas el permiso de administrar los parámetros del sistema, con una condición: la
fecha de cierre sigue siendo exclusiva de Gerencia (regla 16 de `CLAUDE.md`).

## Decisiones

### 1. Menú agrupado en cinco bloques

El menú lateral queda así: **Dashboard** (antes "Inicio"); **Ciclo Operativo Diario** (facturas,
cuentas por cobrar, ingresos, gastos y cuentas por pagar); **Control Financiero Contable**
(presupuesto, asientos contables, reportes y bitácora de operaciones); **Catálogos** (clientes,
proveedores, productos y servicios, categorías financieras con *cuentas contables* como subelemento, e
impuestos y retenciones); y **Configuración** (parámetros del sistema). La sección **Administración**
(usuarios y bitácora de seguridad) se mantiene para el Administrador, que no tiene acceso financiero.

"Bitácora de operaciones" no estaba en la lista del usuario; se ubica en **Control Financiero
Contable** por ser una consulta de auditoría de documentos financieros y seguir siendo exclusiva de
Gerencia.

Cada enlace conserva la política que ya tenía su pantalla (`canViewInvoices`,
`canViewFinancialReports`, `canViewCatalogs`, `canViewAuditTrail`, `canManageUsers`), y "Cuentas
contables" se muestra como subelemento de "Categorías financieras" con la clase
`.sidebar-link--sub`.

### 2. Se elimina la pantalla de Consultas

Se retiran el `QueriesController`, `Views/Queries/{Index,_Pagination}.cshtml`, `Models/Queries/*` y
`QueriesAuthorizationTests`; cada listado conserva sus propios filtros. Esto sustituye la regla 11 en
lo relativo a la pantalla única de Consultas (ADR-0033 pasa a "Sustituido"). La pantalla de inicio
(Dashboard) conserva el indicador de cuentas vencidas y próximas a vencer.

### 3. Finanzas administra los parámetros; la fecha de cierre sigue siendo de Gerencia

`SystemPolicies.ManageSystemParameters` pasa a admitir **Gerencia y Finanzas** (límite de
autorización, días de alerta y datos de la empresa). La fecha de cierre (regla 16) se separa en una
política nueva, `SystemPolicies.ManageClosingDate`, exclusiva de **Gerencia**, que exige
`SystemParametersController.ChangeClosingDate`. La vista oculta el formulario de fecha de cierre a
quien no tiene esa política.

Los cambios de parámetros ya quedan registrados en su historial propio (`SystemParameterChange`,
`ClosingDateChange`, `CompanyDataChange`), con el valor anterior, el nuevo, el usuario y la fecha; no
se modificó ese mecanismo.

## Consecuencias

- Nuevo: `SystemPolicies.ManageClosingDate`.
- Modificado: `AuthorizationExtensions.cs` (roles de `ManageSystemParameters` y nueva política),
  `SystemParametersController.cs` (política de `ChangeClosingDate` y comentario),
  `Views/SystemParameters/Index.cshtml` (formulario de fecha de cierre condicionado),
  `_Layout.cshtml` (menú nuevo), `site.css` (`.sidebar-link--sub`),
  `CatalogAuthorizationTests.cs` (políticas de parámetros y fecha de cierre) y
  `OperationAuditController.cs` (comentario).
- Retirado: módulo de Consultas (`QueriesController`, `Views/Queries/*`, `Models/Queries/*` y
  `QueriesAuthorizationTests`).
- Documentación: `docs/02-requisitos-trazabilidad.md` (RN-CFG-002, RF-CON-002 y la nueva matriz
  RF-MEN-001 a RF-MEN-003), `docs/06-pruebas-evidencias.md` (PR-CON-001 y PR-CON-002),
  `docs/01-vision-alcance-glosario.md`, ADR-0033 (sustituido) y el índice de ADR.
- Verificación: la solución compila sin advertencias ni errores. Las pruebas automatizadas no pudieron
  ejecutarse en este equipo (el host de pruebas recibe «Acceso denegado» al abrir el proceso padre);
  quedan para el entorno del usuario, incluida la prueba nueva
  `SystemParametersClosingDateEndpoint_RequiresManageClosingDate`.
