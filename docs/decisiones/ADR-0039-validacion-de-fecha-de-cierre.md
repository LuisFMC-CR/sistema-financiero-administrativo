# ADR-0039: Validación de la fecha de cierre en las operaciones existentes

- **Fecha:** 2026-10-26
- **Estado:** Aceptado

## Contexto

Bloque 3 del paso 6 de `CLAUDE.md` (regla de negocio 16), el último del paso: aplicar
`SystemParameters.IsOpen(fecha)` (ADR-0038) a cada registro, confirmación, anulación y ajuste que ya
existe. Con esto termina completo el paso 6.

## Decisión

- **Un único punto de validación**, `ClosingDateGuard.GetViolationMessageAsync` (Infrastructure/
  Parameters), que cada servicio llama con la fecha de negocio que le corresponde. Evita repetir en
  cada servicio la misma lectura de `SystemParameters` y el mismo mensaje. Sin parámetros
  configurados, no hay fecha de cierre que aplicar (mismo criterio que el límite de autorización,
  regla 7).
- **Qué fecha se valida en cada operación** (service layer, el dominio sigue sin conocer
  `SystemParameters`, igual patrón que el límite de autorización):

  | Servicio | Operación | Fecha |
  |---|---|---|
  | `InvoiceService` | `CreateAsync` | `command.IssueDate` |
  | | `ConfirmAsync` / `CancelAsync` | `invoice.IssueDate` |
  | `ExpenseService` | `CreateAsync` | `command.IssueDate` |
  | | `ConfirmAsync` / `CancelAsync` | `expense.IssueDate` |
  | `ReceivablePaymentService` | `RegisterAsync` | `command.PaymentDate` |
  | | `CancelAsync` | `payment.PaymentDate` |
  | `SupplierPaymentService` | `RegisterAsync` | `command.PaymentDate` |
  | | `CancelAsync` | `payment.PaymentDate` |
  | `IncomeService` | `CreateManualAsync` | `command.IncomeDate` |
  | | `CancelAsync` | `income.IncomeDate` |
  | `BalanceAdjustmentService` | `AdjustReceivableAsync`/`AdjustPayableAsync` | hoy |

  La fecha usada es siempre la del documento (su período), no la del instante en que se hace clic:
  así, "anular en un mes cerrado exige reabrirlo" (regla 16) funciona incluso si la anulación se
  intenta hoy sobre un documento de un mes ya cerrado.
- **Fuera de esta validación**: `UpdateHeaderAsync`, `AddLineAsync`, `ReplaceLineAsync`,
  `RemoveLineAsync` de Invoice/Expense (editar un borrador no mueve dinero ni compromete ningún
  período; el punto de control real es confirmar) y `Budget.ChangeAmount` (parámetro de planificación,
  no un documento con fecha propia, y el modelo no lo lista como consciente de `closingDate`).

## Consecuencias

- Nuevo: `Infrastructure/Parameters/ClosingDateGuard.cs`.
- Modificado: `InvoiceService.cs`, `ExpenseService.cs`, `ReceivablePaymentService.cs`,
  `SupplierPaymentService.cs`, `IncomeService.cs`, `BalanceAdjustmentService.cs` (una llamada al
  guard por cada operación de la tabla anterior).
- Verificación: 12 pruebas SQL reales nuevas, dos o tres por servicio (operación rechazada con el
  período cerrado; donde aplica, también la misma operación en un período abierto para contrastar).
  285 pruebas unitarias y 291 de integración pasan.
- Verificación manual: la app arranca y las rutas existentes siguen exigiendo inicio de sesión.
- Con este bloque termina completo el paso 6 de `CLAUDE.md` (anulaciones, ajustes de saldo y fecha de
  cierre). No quedan pasos pendientes del roadmap salvo el paso 7 (pantalla de perfiles y permisos).
