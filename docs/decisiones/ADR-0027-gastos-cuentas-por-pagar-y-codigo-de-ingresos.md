# ADR-0027: Gastos, cuentas por pagar y código automático de ingresos

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Primer bloque del paso 3 de `CLAUDE.md`: "Gastos, cuentas por pagar y pagos". Es el espejo, del lado
de gastos, de lo que el paso 2 construyó del lado de ingresos (ADR-0023 a ADR-0026). Antes de empezar
se confirmó por exploración que no existía ningún código de `Expense`, `Payable` ni `SupplierPayment`
en el repositorio.

Al diseñar `Expense` se detectó una brecha frente a la regla de negocio 12 (códigos automáticos): pide
`ING-001` para ingresos y `GAS-001` para gastos, pero `Income` (construido en el paso 2) nunca recibió
un campo `Code`. Se decidió con el usuario corregir también esa brecha en este mismo incremento, en
vez de dejarla pendiente.

## Decisión

- **`Expense` no tiene líneas.** A diferencia de `Invoice`, es una sola entidad con un monto neto, una
  categoría de gasto y una tarifa de IVA (regla 2: "un gasto es solo una compra... respaldada por
  comprobante electrónico"). El IVA y el total se calculan sobre el neto que se registra (regla 3); la
  fotografía de la tarifa (`TaxCode`, `TaxName`, `TaxRate`, `TaxAmount`) vive como columnas propias,
  sin un tipo propietario separado, porque solo hay una "línea".
- **La cuenta de efectivo y la referencia de un gasto de contado se guardan en el propio `Expense`**,
  no en una entidad aparte: a diferencia de `Invoice` (que genera un `Income` independiente), un gasto
  de contado no genera ningún otro documento. El medio de pago no se vuelve a pedir al confirmar: ya
  quedó fijo desde el borrador (regla 7), a diferencia de `ConfirmInvoiceCommand`, que sí lo pide.
- **Confirmar de contado sobre el límite de autorización usa el mismo criterio que abonos y pagos**:
  se rechaza para Finanzas, pero Gerencia puede confirmarlo de contado igual (`actorIsManagement` en
  `IExpenseService.ConfirmAsync`), decisión confirmada explícitamente con el usuario. Si Gerencia no
  confirma, la alternativa es cambiar el plazo a crédito mientras el gasto es borrador: eso genera su
  `Payable` con vencimiento real, y Gerencia registrará su pago (bloque 2 del roadmap).
- **`Payable` es una copia exacta de `Receivable`** (mismos métodos `ApplyPayment`/`RevertPayment`/
  `Cancel`, misma invariante de saldo), con `ExpenseId` único y `SupplierId`. Tampoco tiene todavía
  ajuste de saldo independiente ni valida la fecha de cierre: ambas quedan para el paso 6.
- **La bitácora de operaciones solo registra el `Expense`**, no el `Payable` que genera: mismo
  criterio que `Receivable`, que tampoco tiene entrada propia (solo se amplió
  `OperationLogEntityType` con `Expense = 5` y `Payable = 6`, y el `CHECK` de
  `seguridad.BitacoraOperaciones` de `BETWEEN 1 AND 4` a `BETWEEN 1 AND 6`).
- **`Income` gana su código automático** (`ING-001`, regla 12), generado con la misma secuencia SQL
  que los catálogos (`CatalogPersistence.GenerateSequentialCodeAsync`). La migración agrega la columna
  `Code`, **respalda los ingresos ya existentes** en la base de desarrollo (2 filas, sin relación con
  partida doble ni datos sensibles) asignándoles `ING-001`/`ING-002` por orden de creación mediante
  `ROW_NUMBER()`, y arranca la secuencia después del último asignado para que el siguiente ingreso real
  no choque con ellos; en una base sin ingresos previos no actualiza ninguna fila y la secuencia
  arranca en 1.
- **`IExpenseService` no tiene `SearchAsync` todavía**, igual criterio que `IReceivableService` e
  `IIncomeService` en el paso 2 (ADR-0023/ADR-0024): se agrega junto con la pantalla que lo necesite
  (bloque 3 de este mismo paso).

## Consecuencias

- Nuevo: `Domain/Expenses/{ExpenseStatus,Expense}.cs`, `Domain/Payables/{PayableStatus,Payable}.cs`,
  `Application/Expenses/ExpenseContracts.cs`, `Infrastructure/Expenses/ExpenseService.cs`,
  `ExpenseConfiguration.cs`, `PayableConfiguration.cs`.
- Modificado: `Income` (constructor y las tres fábricas reciben `code`), `IncomeModel.Code`,
  `IncomeConfiguration` (`UX_Ingresos_Code`), `OperationLogEntityType`, `OperationLogEntryConfiguration`,
  `FinancialDbContext` (`DbSet<Expense>`, `DbSet<Payable>`), `DependencyInjection.AddInfrastructure`.
- Migraciones: `AddExpensesPayablesAndIncomeCode` (tablas `Gastos`/`CuentasPorPagar`, columna
  `Ingresos.Code`, secuencias `Seq_GastoCodigo`/`Seq_IngresoCodigo`, respaldo de los ingresos
  existentes) y `WidenOperationLogEntityTypeRange`.
- `docs/02-requisitos-trazabilidad.md` agrega la matriz del incremento de gastos (RF-GAS-001 a
  RF-GAS-004) y amplía RF-51 (código automático) para incluir ingresos y gastos.
- Verificación: `ExpenseTests`, `PayableTests` (dominio) y
  `SqlServerExpenseServiceScenarioTests` (confirmar de contado dentro y sobre el límite —rechazado
  para Finanzas, permitido para Gerencia—, confirmar a crédito genera el `Payable`, anular un gasto a
  crédito sin pagos lo anula en cascada y con un pago vigente se bloquea). Las 251 pruebas unitarias y
  las 219 de integración (214 previas + 5 nuevas, estas últimas solo con
  `SISTEMA_FINANCIERO_RUN_SQL_TESTS=1`) pasan contra SQL Server real.
- Pendiente: `SupplierPayment` (bloque 2) y las pantallas web (`ExpensesController`,
  `PayablesController`, `SupplierPaymentsController`, bloque 3), con
  `ISupplierCatalogService.GetActiveOptionsAsync` todavía por agregar para el selector de proveedor.
