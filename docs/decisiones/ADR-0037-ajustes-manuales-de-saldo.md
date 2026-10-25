# ADR-0037: Ajustes manuales de saldo

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Bloque 1 del paso 6 de `CLAUDE.md` (regla de negocio 4): "Ajuste de saldo (solo Gerencia), aumento o
disminución con motivo; invariante 0 ≤ saldo ≤ monto original; no genera ingresos ni pagos ni toca
efectivo, documento o presupuesto; un error se corrige con otro ajuste en sentido contrario." Es
funcionalidad nueva por completo: no existía entidad, servicio ni pantalla.

## Decisión

- `BalanceAdjustment` (namespace nuevo `Domain.Adjustments`) es un registro inmutable, igual criterio
  que `OperationLogEntry`: no se modifica ni se anula. Exactamente una de `ReceivableId`/`PayableId`
  está presente (regla reforzada con un `CHECK` en `finanzas.AjustesSaldo`).
- `Receivable.Adjust`/`Payable.Adjust` (nuevos métodos, mismo patrón que `ApplyPayment`/
  `RevertPayment`): validan 0 ≤ nuevo saldo ≤ monto original y actualizan `Balance`/`Status`; no
  tocan nada más.
- Un solo `IBalanceAdjustmentService` para ambos lados (no dos espejados como abonos/pagos): es la
  misma tabla y el mismo comportamiento, solo cambia a cuál de las dos cuentas aplica.
- Concurrencia: `ReceivableModel`/`PayableModel` ganan un campo `Version` (antes no lo tenían, porque
  nada los editaba directamente desde un formulario de dos pasos) para que el formulario de ajuste
  pueda detectar si alguien más modificó la cuenta entre que se mostró y que se confirmó, igual
  patrón que `ReceivablePaymentCancelViewModel`.
- Exclusivo de Gerencia: nueva política `SystemPolicies.AdjustFinancialBalances`
  ("Movimientos.Ajustar"), mismo rol único que `VoidFinancialTransactions`.
- `BalanceAdjustmentsController` expone `/cuentas-por-cobrar/{id}/ajustar` y
  `/cuentas-por-pagar/{id}/ajustar`; `ReceivablesController`/`PayablesController.Details` ganan el
  historial de ajustes y el botón, visible solo con el permiso.
- Bitácora: nueva acción `OperationLogAction.Adjusted` (se ensancha el `CHECK` de
  `BitacoraOperaciones.Action` de 1-2 a 1-3); reutiliza los `OperationLogEntityType.Receivable`/
  `Payable` que ya existían.

## Consecuencias

- Nuevo: `Domain/Adjustments/{BalanceAdjustment,BalanceAdjustmentDirection}.cs`,
  `Application/Adjustments/BalanceAdjustmentContracts.cs`,
  `Infrastructure/Adjustments/BalanceAdjustmentService.cs`,
  `Infrastructure/Persistence/Configurations/BalanceAdjustmentConfiguration.cs`,
  `Web/Controllers/BalanceAdjustmentsController.cs`,
  `Web/Models/Adjustments/{BalanceAdjustmentViewModels,BalanceAdjustmentDisplayExtensions}.cs`,
  `Web/Views/BalanceAdjustments/{AdjustReceivable,AdjustPayable}.cshtml`. Migración
  `AddBalanceAdjustments`, aplicada a la base de desarrollo.
- Modificado: `Receivable.cs`/`Payable.cs` (método `Adjust`), `OperationLogAction.cs`,
  `ReceivableContracts.cs`/`PayableContracts.cs` (campo `Version`) y sus `Map` en
  `ReceivableService.cs`/`PayableService.cs`, `FinancialDbContext.cs` (DbSet e inmutabilidad),
  `DependencyInjection.cs`, `SystemPolicies.cs`, `AuthorizationExtensions.cs`,
  `ReceivablesController.cs`/`PayablesController.cs` y sus vistas `Details.cshtml` y
  `ViewModels`. Las pruebas de autorización existentes (`ReceivablesAuthorizationTests`,
  `PayablesAuthorizationTests`) se actualizaron para excluir las rutas `ajustar` de la verificación
  genérica de `Reportes.Ver` y verificar en su lugar que exigen `Movimientos.Ajustar`.
- Verificación: 8 pruebas unitarias de dominio nuevas (`Adjust` en `ReceivableTests`/`PayableTests`:
  aumento, disminución, rechazo fuera de rango en ambos sentidos, rechazo sobre cuenta anulada) y 4
  pruebas SQL reales nuevas (corrección de un ajuste con otro en sentido contrario, rechazo que
  excede el monto original, disminución del lado de pagar, conflicto de concurrencia con una versión
  vieja). 279 pruebas unitarias y 272 de integración pasan.
- Verificación manual: la app arranca y `/cuentas-por-cobrar/{id}/ajustar` y
  `/cuentas-por-pagar/{id}/ajustar` exigen inicio de sesión.
- Pendiente: los bloques 2 y 3 del paso 6 (parámetro de fecha de cierre, y su validación en todas las
  operaciones existentes, incluido este ajuste).
