# ADR-0032: Indicador de cuentas vencidas

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Primer bloque del paso 5 de `CLAUDE.md`: el indicador de cuentas vencidas y próximas a vencer en la
pantalla de inicio (regla de negocio 9), compartido más adelante con la pantalla de Consultas.

## Decisión

- **`GetOverdueSummaryAsync` vive en `IReceivableService` e `IPayableService`**, no en un servicio
  nuevo: cada uno ya conoce su propia tabla (`CuentasPorCobrar`/`CuentasPorPagar`) y su invariante de
  saldo; un servicio aparte solo duplicaría esas consultas.
- **`OverdueSummary`** (`Application/Catalogs/Common`, mismo lugar que `PagedResult`/
  `CatalogOperationResult`: tipos compartidos de Application, no solo de catálogos) es el tipo de
  retorno de ambos métodos, con la misma forma: cantidad y monto de vencidas, cantidad y monto de
  próximas a vencer.
- **El día de alerta sin parámetros configurados es 7 por defecto**, literal de la regla 9, a
  diferencia del límite de autorización (que por seguridad se trata como 0 sin configurar): aquí no
  hay ningún riesgo de bloquear o permitir de más, es solo informativo.
- **`HomeController.Index` calcula el indicador solo si el usuario tiene `Reportes.Ver`** (Gerencia y
  Finanzas); Asistente y Administrador ven la página de inicio sin él, igual criterio que "Asistente
  no ve... saldos". La vista parcial `_OverdueIndicator.cshtml` es compartida con el futuro
  `QueriesController` del bloque 2, para no duplicar el marcado.
- Se aprovechó el cambio para quitar de `Views/Home/Index.cshtml` las tres tarjetas de presentación
  obsoletas (mencionaban USD, retirado desde ADR-0015) por el indicador real.

## Consecuencias

- Nuevo: `Application/Catalogs/Common/OverdueSummary.cs`, `Web/Models/Overdue/OverdueIndicatorViewModel.cs`,
  `Views/Shared/_OverdueIndicator.cshtml`.
- Modificado: `IReceivableService`/`ReceivableService`, `IPayableService`/`PayableService` (ambos
  ganan `TimeProvider` inyectado, igual criterio que el resto de los servicios con lógica dependiente
  de "hoy"), `HomeController`, `Views/Home/Index.cshtml`.
- Verificación: `SqlServerReceivableServiceScenarioTests` y `SqlServerPayableServiceScenarioTests`
  (nuevos, cada uno con el caso vencida/próxima/ninguna de las dos). 269 pruebas unitarias y 240 de
  integración pasan contra SQL Server real.
- Pendiente: pantalla de Consultas (bloque 2), que mostrará el mismo indicador.
