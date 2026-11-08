# ADR-0050: Acceso operativo ampliado del Asistente

- **Fecha:** 2026-11-06
- **Estado:** Aceptado

## Contexto

El perfil Asistente necesitaba ejecutar tareas operativas diarias en gastos e ingresos y consultar los
saldos por cobrar y por pagar, sin recibir permisos de confirmación, pago, anulación, ajustes ni
consulta contable. ADR-0026 había limitado Cuentas por Cobrar e Ingresos a Gerencia y Finanzas y
excluido al Asistente de la consulta de saldos.

## Decisión

- Asistente conserva el acceso a Facturas y puede crear y editar únicamente sus propios borradores de
  gastos. No puede confirmar ni anular gastos, ni consultar sus asientos.
- Asistente puede consultar ingresos y registrar ingresos manuales. No puede anularlos ni consultar
  sus asientos.
- Asistente puede consultar Cuentas por Cobrar y Cuentas por Pagar, incluidos saldos y detalles, pero
  no puede registrar ni anular abonos o pagos, ni ajustar saldos.
- Asistente continúa sin acceso a reportes, presupuesto, bitácora financiera o consulta general de
  asientos.
- El sidebar del Asistente no muestra “Catálogo contable” ni “Impuestos y retenciones”. Este cambio de
  visibilidad no modifica las políticas de autorización ni las rutas del servidor.
- El servidor aplica políticas específicas para consultar CxC/CxP y registrar ingresos manuales. La
  interfaz oculta acciones y enlaces a asientos que el perfil no puede usar.

## Consecuencias

- `ViewReceivablesAndPayables` permite las consultas de CxC/CxP a Gerencia, Finanzas y Asistente,
  manteniendo sus acciones de escritura protegidas por políticas independientes.
- `RegisterManualIncome` habilita el alta de ingresos manuales sin ampliar `ConfirmFinancialTransactions`.
- `IncomesController` permite la consulta con `ViewFinancialTransactions`; CxC y CxP usan
  `ViewReceivablesAndPayables`.
- Las vistas ocultan los enlaces a asientos cuando el usuario no satisface `ViewFinancialReports`.
- La matriz vigente del perfil se actualiza en `CLAUDE.md` y `docs/02-requisitos-trazabilidad.md`.
- `AssistantFinancialAccessTests` verifica las capacidades concedidas y denegadas; las pruebas de
  autorización de cada módulo verifican las políticas de las rutas.
