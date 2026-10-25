# ADR-0038: Parámetro de fecha de cierre

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Bloque 2 del paso 6 de `CLAUDE.md` (regla de negocio 16): "No se registra, confirma, anula ni ajusta
nada con fecha igual o anterior [a la fecha de cierre]... Adelantarla es el cierre mensual;
retrocederla es una reapertura con motivo y queda en bitácora... No puede ser posterior a hoy." Este
bloque agrega el parámetro y su cambio; la validación en las demás operaciones (facturas, gastos,
cuentas por cobrar/pagar, abonos, pagos, ingresos, ajustes de saldo) queda para el bloque 3.

## Decisión

- `SystemParameters.ClosingDate` nace en `DateOnly.MinValue` ("sin fecha de cierre configurada"), no
  como un dato que Gerencia deba elegir al configurar el límite y los días por primera vez: no tiene
  sentido pedir una fecha de cierre antes de que exista ningún movimiento que cerrar, y forzarla
  habría exigido un valor arbitrario. Mientras valga `DateOnly.MinValue`, `IsOpen(fecha)` es cierto
  para cualquier fecha real.
- `SystemParameters.ChangeClosingDate(newClosingDate, today, reason, ...)` es una operación propia,
  separada de `UpdateValues` (límite y días): así lo señala el documento (`UpdateLimits` y
  `ChangeClosingDate` son operaciones distintas de `SystemSettings`). Valida que la nueva fecha no sea
  posterior a `today`; si retrocede respecto a la fecha vigente, exige un motivo no vacío.
- `IsOpen(DateOnly date) => date > ClosingDate` es el único punto que el bloque 3 va a reutilizar en
  cada operación existente.
- El cambio de fecha de cierre tiene su propia bitácora, `ClosingDateChange`, separada de
  `SystemParameterChange`: mezclarlas habría obligado a repetir en cada fila de límite/días el valor
  de cierre sin cambios, o viceversa. `PreviousClosingDate` nunca es nulo (a diferencia de
  `SystemParameterChange`) porque la fila de parámetros siempre tiene una fecha de cierre desde que
  se crea, aunque sea el valor "sin configurar".
- Pantalla `/parametros`: dos formularios independientes dentro de la misma página
  (`SystemParametersPageViewModel` los agrupa). El de límite y días no cambió; el de fecha de cierre
  es nuevo, con su propia acción `ChangeClosingDate`, porque el motivo solo aplica a ese cambio y no
  tiene sentido en el formulario de límite y días. Ambos exigen `Movimientos.Administrar` — en
  realidad `Parametros.Administrar` (`SystemPolicies.ManageSystemParameters`), ya existente.

## Consecuencias

- Nuevo: `Domain/Parameters/ClosingDateChange.cs`,
  `Infrastructure/Persistence/Configurations/ClosingDateChangeConfiguration.cs`,
  `ChangeClosingDateViewModel`/`SystemParametersPageViewModel` (Web). Migración `AddClosingDate`
  (columna `ClosingDate` en `ParametrosSistema`, tabla nueva `ParametrosFechaCierreHistorial`),
  aplicada a la base de desarrollo.
- Modificado: `SystemParameters.cs` (propiedad, `IsOpen`, `ChangeClosingDate`),
  `SystemParametersContracts.cs` (campo `ClosingDate` en el modelo, método nuevo en la interfaz),
  `SystemParametersService.cs`, `SystemParametersConfiguration.cs`, `FinancialDbContext.cs`,
  `SystemParametersController.cs` y `Views/SystemParameters/Index.cshtml`. Las pruebas existentes de
  ese controlador (`SystemParametersControllerTests`) y de escenarios SQL
  (`SqlServerSystemParametersScenarioTests`) se actualizaron para el nuevo campo y el nuevo modelo de
  página.
- Verificación: 6 pruebas unitarias de dominio nuevas (`IsOpen`, adelantar sin motivo, retroceder sin
  motivo rechazado, retroceder con motivo aceptado, fecha posterior a hoy rechazada) y 4 pruebas SQL
  reales nuevas (adelantar, retroceder sin motivo, retroceder con motivo y su bitácora, fecha futura
  rechazada). 285 pruebas unitarias y 279 de integración pasan.
- Verificación manual: la app arranca y `/parametros` exige inicio de sesión.
- Pendiente: el bloque 3 del paso 6, que agrega la validación `IsOpen` a cada registro, confirmación,
  anulación y ajuste existente — el único punto todavía sin construir de este bloque.
