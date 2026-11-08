# ADR-0053: Presupuestos sin períodos pasados y reloj común

- **Fecha:** 2026-11-07
- **Estado:** Aceptado

## Contexto

La pantalla de presupuesto permitía crear y corregir presupuestos de meses anteriores. La fecha se
consultaba con `DateTime.Now` en algunos controladores, mientras los servicios financieros ya
recibían `TimeProvider` para leer el reloj del sistema y registrar instantes UTC.

## Decisión

- No se permite crear ni modificar presupuestos para meses anteriores al mes actual.
- Los períodos anteriores siguen disponibles para consulta e informes, pero la interfaz oculta sus
  acciones de alta y edición.
- La regla se valida en `BudgetService.SetAmountAsync`, además de proteger las rutas de la interfaz,
  por lo que también aplica a llamadas directas al servicio.
- Se reutiliza el `TimeProvider` común registrado como `TimeProvider.System`; las fechas de negocio se
  obtienen con `GetLocalNow()` y los instantes de auditoría continúan en UTC.
- Los períodos futuros siguen permitidos.

## Consecuencias

- `BudgetsController` usa el reloj común para determinar el período actual y muestra los períodos
  históricos como solo lectura.
- Las pruebas SQL usan el mes actual para los escenarios de alta y corrección, y verifican el rechazo
  del mes anterior.
- `CLAUDE.md`, `docs/02-requisitos-trazabilidad.md` y `docs/06-pruebas-evidencias.md` reflejan la
  regla y su verificación.
