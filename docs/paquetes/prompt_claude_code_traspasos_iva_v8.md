# Traspasos entre cuentas de efectivo y pago del IVA (paquete v8, solo lo que falta)

Contexto: el repositorio está en el commit `8d1118c`. Ya existen partida doble, notas de crédito,
IVA, retenciones, reportes contables y cierre mensual. Este paquete agrega solo dos operaciones que
hoy no se pueden registrar: mover dinero entre cajas y bancos propios, y pagar el IVA a Hacienda.
Antes de empezar, revisa el código y dime qué difiere de lo que aquí se describe.

Reglas:
- Trabaja por bloques (1 a 3). Al terminar cada uno: migración explícita, pruebas unitarias y de
  integración, `dotnet test` completo en verde, commit y un resumen corto. Espera mi "continúa".
- Usa exactamente los nombres de tablas, columnas y clases indicados. Si necesitas otro, pregúntame.
- Sigue los patrones que ya existen: `SupplierPayment` y `CreditNote` como modelo de entidad, servicio,
  configuración EF, secuencia de código, bitácora y PDF; `JournalPostingService` para los asientos.
- Estas operaciones deben respetar el cierre mensual automático y el período fiscal
  (`ClosingDateGuard`), el límite de autorización, los permisos y que nada se elimina físicamente.
- Interfaz y mensajes en español; montos en colones; fechas dd/mm/aaaa. Sin paquetes NuGet nuevos.

Diferencias con el código actual que debes respetar:
- **Ya no existen las categorías financieras.** Ingresos, gastos y presupuestos apuntan directo a la
  cuenta contable (`AccountId`). Donde el texto de abajo hable de categorías, ignóralo.
- **El cierre ya no es una fecha única.** Usa `ClosingDateGuard.GetViolationMessageAsync` con la fecha
  de negocio de cada operación.
- **Permisos:** registrar traspasos y liquidaciones: política `ConfirmFinancialTransactions`
  (Finanzas y Gerencia). Anular: política `VoidFinancialTransactions` (solo Gerencia).
- **Asientos:** extiende `JournalEntrySourceType` con `CashTransfer = 8` y `VatSettlement = 9`, y la
  restricción CHECK de `Asientos.SourceType` (hay una migración reciente que la corrigió; sigue ese
  ejemplo). Extiende también el comando de generación de asientos y `OperationLogEntityType`.
- Las cuentas de IVA se buscan por `LedgerAccountSystemRole.VatPayable` y `VatCreditable`; las de
  efectivo son las cuentas activas con `IsCash`.
- Los códigos de documento usan secuencia SQL (`Seq_TraspasoCodigo`, `Seq_LiquidacionIvaCodigo`),
  como `Seq_PagoCodigo`.

## Bloque 1. Traspaso entre cuentas de efectivo

Un traspaso mueve dinero entre dos cuentas de efectivo propias: depositar la caja chica en el banco,
retirar del banco para reponer la caja, o pasar dinero entre dos cuentas bancarias. No es ingreso ni
gasto: no toca cuentas de ingreso o gasto, presupuesto ni estado de resultados.

1. **Tabla** `finanzas.Traspasos` (clase `CashTransfer`): `Id`, `Code nvarchar(30)` único
   (`TRA-000001`, secuencia `finanzas.Seq_TraspasoCodigo`), `TransferDate date`,
   `SourceCashAccountId` y `DestinationCashAccountId` (FK a CuentasContables, cuentas de efectivo
   activas), `Amount decimal(18,2)`, `Reference nvarchar(100)` obligatoria (número de transferencia,
   depósito o comprobante de caja), `Description nvarchar(300)` obligatoria, `Status int` (1 Vigente,
   2 Anulado), `CancellationReason nvarchar(300)`, `CancelledAtUtc`, `CancelledByUserId`, auditoría
   (`CreatedAtUtc`, `CreatedByUserId`, `UpdatedAtUtc`, `UpdatedByUserId`) y `RowVersion`.
   CHECK: `Amount > 0`, origen distinto de destino, `Status IN (1, 2)`.
2. **Registro:** Finanzas y Gerencia, en una sola operación (sin borrador), con la misma política que
   los pagos a proveedores. Un traspaso que supera el límite de autorización solo lo registra
   Gerencia. Respeta la fecha de cierre. El formulario muestra, como dato informativo, el saldo de la
   cuenta de origen según los asientos; **no bloquees** por saldo insuficiente, porque el sistema no
   tiene saldos iniciales. Advierte, sin bloquear, si la referencia se repite en la misma cuenta.
3. **Asiento** (`JournalEntrySourceType.CashTransfer = 8`): Débito cuenta de destino / Crédito cuenta de origen, con la
   fecha del traspaso. Descripción: "Traspaso TRA-000001".
4. **Anulación:** solo Gerencia, con motivo; genera la reversión con la fecha original. Respeta la
   fecha de cierre.
5. **Pantallas:** en el módulo Gestionar Cuentas Contables, "Traspasos": listado con filtros (código,
   fecha, cuenta de origen o destino, estado), formulario de registro y detalle con "Ver asiento".
   Agrega el tipo de documento a Consultas.
6. **Bitácora:** registrar y anular quedan en BitacoraOperaciones (nuevo valor `CashTransfer` en
   `OperationLogEntityType`).
7. Extiende el comando de generación de asientos (idempotente) y la trazabilidad "Ver asiento" a este
   origen.

## Bloque 2. Liquidación mensual del IVA

Cada mes la empresa compensa el IVA cobrado (IVA por pagar) con el IVA pagado en sus compras (IVA
acreditable) y paga la diferencia a Hacienda; si el acreditable es mayor, queda un saldo a favor para
el mes siguiente. Hoy no hay forma de registrarlo: si se anota el pago como gasto, el estado de
resultados muestra un gasto falso y las cuentas de IVA crecen sin límite. Esta operación es un
registro interno: **no sustituye la declaración ante Hacienda.**

1. **Tabla** `finanzas.LiquidacionesIva` (clase `VatSettlement`): `Id`, `Code nvarchar(30)` único
   (`LIQ-000001`, secuencia `finanzas.Seq_LiquidacionIvaCodigo`), `PeriodYear int`,
   `PeriodMonth int`, `SettlementDate date` (fecha del pago o de la liquidación),
   `VatPayableAmount decimal(18,2)` (IVA por pagar liquidado), `VatCreditApplied decimal(18,2)` (IVA
   acreditable aplicado), `PaymentAmount decimal(18,2)` (pago a Hacienda),
   `CreditCarriedForward decimal(18,2)` (IVA acreditable que queda para el período siguiente,
   informativo), `CashAccountId` (FK a CuentasContables, nula si no hay pago),
   `Reference nvarchar(100)` (número del comprobante de pago; obligatoria si hay pago),
   `Status int` (1 Vigente, 2 Anulada), `CancellationReason nvarchar(300)`, `CancelledAtUtc`,
   `CancelledByUserId`, auditoría y `RowVersion`.
   CHECK: montos ≥ 0, `PeriodMonth` entre 1 y 12, `VatPayableAmount = VatCreditApplied +
   PaymentAmount`, `CashAccountId` no nula cuando `PaymentAmount > 0`. Índice único filtrado
   (`PeriodYear`, `PeriodMonth`) donde `Status = 1`.
2. **Cálculo**, al último día del mes liquidado (fecha de corte), desde los asientos y sin depender
   de la fecha en que se registraron las liquidaciones anteriores:
   - **A (IVA por pagar pendiente)** = movimientos de la cuenta de sistema IVA por pagar (créditos
     menos débitos) con fecha hasta el corte, **sin** los asientos de liquidación ni sus reversiones
     (`VatSettlement`), menos la suma de
     `VatPayableAmount` de las liquidaciones vigentes de períodos anteriores.
   - **B (IVA acreditable disponible)** = movimientos de la cuenta de sistema IVA acreditable
     (débitos menos créditos) con fecha hasta el corte, sin los asientos de liquidación, menos la
     suma de `VatCreditApplied` de las liquidaciones vigentes de períodos anteriores.
   - Si A > B: `VatCreditApplied` = B, `PaymentAmount` = A − B, `CreditCarriedForward` = 0.
   - Si A ≤ B: `VatCreditApplied` = A, `PaymentAmount` = 0, `CreditCarriedForward` = B − A.
   - `VatPayableAmount` = A. Si A ≤ 0, no se registra: "No hay IVA por pagar en este período; el IVA
     acreditable se acumula para el siguiente."
   - Así, lo que se registre tarde con fecha de un mes ya liquidado entra automáticamente en la
     liquidación siguiente, y el saldo a favor se arrastra solo.
3. **Reglas:**
   - Una sola liquidación vigente por mes. No se liquida un mes igual o anterior al último mes con
     liquidación vigente.
   - `SettlementDate` posterior a la fecha de corte; respeta la fecha de cierre.
   - Registran Finanzas y Gerencia; un pago que supera el límite de autorización solo lo registra
     Gerencia.
   - Si la fecha de cierre es anterior a la fecha de corte, advierte sin bloquear: "Hay días del
     período sin cerrar; lo que se registre después con fecha de este período pasará a la liquidación
     siguiente."
   - Los intereses o multas por pago tardío no son parte de la liquidación: se registran aparte como
     gasto.
4. **Asiento** (`JournalEntrySourceType.VatSettlement = 9`), con fecha `SettlementDate`: Débito IVA por pagar
   (`VatPayableAmount`) / Crédito IVA acreditable (`VatCreditApplied`) / Crédito cuenta de efectivo
   (`PaymentAmount`). Se omiten las líneas en cero. Descripción: "Liquidación de IVA 10/2026".
5. **Anulación:** solo Gerencia, con motivo, y **solo la liquidación vigente más reciente**; genera la
   reversión con la fecha original y respeta la fecha de cierre.
6. **Pantalla** "Liquidación de IVA", en el módulo Impuestos y retenciones:
   - Se elige año y mes y el sistema muestra la vista previa: IVA por pagar del período, arrastre de
     períodos anteriores, IVA acreditable del período, saldo a favor anterior, crédito aplicado, monto
     a pagar o saldo a favor que queda, y un enlace al reporte de IVA del período de ese mes.
   - Si hay pago, pide fecha, cuenta de efectivo y referencia.
   - Listado de liquidaciones con filtros (año, estado) y detalle con "Ver asiento".
   - Leyenda: "Registro interno del pago del IVA. No sustituye la declaración ante Hacienda."
7. **Bitácora:** registrar y anular quedan en BitacoraOperaciones (nuevo valor `VatSettlement` en
   `OperationLogEntityType`). Extiende el comando de generación de asientos y "Ver asiento" a este
   origen.

## Bloque 3. Ajustar los reportes existentes

1. **Flujo de efectivo (`CashFlow`):** incluye los orígenes nuevos. Sin filtro de cuenta, los
   traspasos se excluyen porque se compensan; con filtro por cuenta, aparecen como "Traspasos
   recibidos" y "Traspasos enviados". El pago del IVA aparece como salida "Pago de IVA".
2. **IVA del período (`VatPeriod`):** sus totales no cuentan los asientos de liquidación. Si el rango
   es un mes con liquidación vigente, muestra su código y su resultado (pago o saldo a favor).
3. **Libro diario, libro mayor y trazabilidad:** deben mostrar y rastrear ambos orígenes ("Ver
   asiento" desde el documento y desde el asiento al documento).
4. **Bitácora de operaciones:** registrar y anular aparecen con su tipo de entidad.
5. **Prueba de extremo a extremo:** traspaso del banco a la caja chica; gasto de contado desde la
   caja; factura y gasto con IVA; liquidación del mes. Comprueba que la balanza cuadra, que el IVA del
   período coincide con los movimientos de las cuentas de IVA sin contar las liquidaciones, y que el
   flujo de efectivo coincide con el libro mayor de las cuentas de efectivo.
6. Resumen final: qué quedó hecho, qué quedó pendiente y cualquier decisión que tomaste sin consultarme.
