# ADR-0052: Consulta de facturas y gastos por Asistente

- **Fecha:** 2026-11-06
- **Estado:** Aceptado

## Contexto

El Asistente tenía restringidos por propietario los listados y detalles de facturas y gastos. Esto
impedía consultar documentos creados por otros usuarios, aunque el perfil debe conservar la consulta
operativa de esos módulos. El usuario aprobó ampliar la lectura sin conceder edición sobre documentos
ajenos.

## Decisión

- Asistente puede consultar facturas y gastos de cualquier usuario, en cualquier estado.
- Asistente solo puede editar sus propios borradores.
- Las políticas de confirmación y anulación permanecen sin cambios: Asistente no confirma ni anula
  documentos.
- Finanzas y Gerencia conservan la consulta y las acciones que ya permiten sus políticas.
- Los filtros por propietario siguen disponibles en los servicios para restringir las operaciones de
  edición; los listados y detalles autorizados para consulta no aplican ese filtro.

## Consecuencias

- `InvoicesController` y `ExpensesController` consultan listados, detalles y salidas de lectura sin
  límite por propietario, y mantienen `OwnerRestriction` en las rutas de edición de borradores.
- Los servicios siguen ocultando un documento ajeno cuando una edición proporciona
  `restrictToOwnerUserId`, respondiendo como si no existiera.
- `SqlServerInvoiceServiceScenarioTests` y `SqlServerExpenseServiceScenarioTests` verifican lectura
  entre propietarios y bloqueo de edición ajena.
- `CLAUDE.md`, `docs/02-requisitos-trazabilidad.md` y `docs/06-pruebas-evidencias.md` documentan el
  alcance y sus pruebas.
