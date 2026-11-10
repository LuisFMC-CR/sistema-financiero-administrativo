# Mejoras finales de la lógica del sistema (paquete v7: lo que falta)

Este paquete **reemplaza** a `prompt_claude_code_mejoras_finales_v2.md`. Los bloques 0 a 6 de ese
paquete ya están en el repositorio (último commit revisado: `26a71fe`, reportes contables y
exportaciones). Aquí está solo lo que falta: las decisiones que se aprobaron después de la v2 sobre lo
ya construido, los bloques de presupuesto, reportes y nota de crédito que quedaban pendientes, y tres
funciones nuevas (traspasos, liquidación del IVA y antigüedad de cuentas por cobrar). Ya fue revisado y
aprobado. Reglas para todo el trabajo:

- **Antes de empezar:** revisa el estado real del repositorio contra este documento y dime qué
  difiere. Si ya hiciste algo de los bloques 7 u 8 del paquete v2, dímelo y ajústalo a lo que dice
  aquí, sin rehacerlo desde cero.
- Trabaja por **bloques (0 a 8), en el orden indicado**. Al terminar cada bloque: migración explícita
  (si aplica), pruebas unitarias y de integración, ADR, actualización de
  `docs/02-requisitos-trazabilidad.md`, `dotnet test` completo en verde, commit y un resumen corto.
  **Espera mi "continúa" antes de empezar el siguiente bloque.**
- Usa **exactamente** los nombres de tablas, columnas y clases indicados: van al diccionario de datos
  del documento. Si necesitas un nombre que no está aquí, pregúntame.
- Ninguna operación nueva puede saltarse la fecha de cierre, el límite de autorización, los permisos
  por perfil ni la regla de "nada se elimina físicamente".
- Interfaz y mensajes en español; montos en colones con 2 decimales; fechas dd/mm/aaaa.
- Si algo de este documento contradice `CLAUDE.md`, manda este documento (el bloque 0 actualiza
  `CLAUDE.md`). Si algo no está definido, pregúntame antes de decidir.
- Antes de eliminar o renombrar código existente, explica el impacto y espera mi confirmación.
- No agregues paquetes NuGet nuevos.

Códigos de trazabilidad para `docs/02` (coinciden con el documento académico). Los de RF-78 a RF-92
ya existen; se agregan: RF-93 condición de IVA del cliente y tarifa automática por línea · RF-94 IVA
no acreditable en gastos · RF-95 retención del 2 % del sector público · RF-96 traspaso entre cuentas
de efectivo · RF-97 liquidación mensual del IVA · RF-98 cuentas por cobrar por cliente con antigüedad
de saldos.

---

## Bloque 0. Actualizar `CLAUDE.md` y `README.md`

Ajusta `CLAUDE.md` (muéstrame el diff antes del commit):

- **Estado y orden de trabajo:** reemplaza la referencia a `prompt_claude_code_mejoras_finales_v2.md`
  por este archivo; marca como hechos los bloques 0 a 6 de la v2 y deja los bloques 1 a 8 de este
  paquete como trabajo pendiente.
- **Alcance, fuera del sistema:**
  - Quita "transferencias" de la línea de bancos: los traspasos entre cuentas de efectivo propias
    entran (bloque 4). Siguen fuera los saldos iniciales, la integración y la conciliación bancarias.
  - Hacienda: la **liquidación interna del IVA** entra (bloque 5: registro del pago o del saldo a
    favor). Siguen fuera la emisión, firma o transmisión de comprobantes y las declaraciones.
  - Sigue fuera: balance general, asientos manuales, saldos iniciales, préstamos, aportes de socios,
    cierres contables formales, inventario, planilla y moneda extranjera.
- **Regla 1 (cuentas contables):** el usuario solo crea cuentas de detalle de **tercer nivel** dentro
  de los grupos 1.1, 4.1, 4.2, 4.3, 5.1, 5.2 y 5.3 (bloque 3).
- **Regla 2 (flujo del dinero):** el dinero que pasa de una cuenta de efectivo a otra es un traspaso,
  nunca un ingreso ni un gasto. El pago del IVA a Hacienda es una liquidación, nunca un gasto.
- **Regla 3 (IVA):** tarifas cargadas: IVA 13 %, IVA 2 %, Exento y No sujeto (Gerencia puede agregar
  otras). Cada cliente tiene una condición de IVA que determina automáticamente la tarifa de las
  líneas de sus facturas (bloque 1). En los gastos, el IVA puede marcarse como no acreditable y
  entonces forma parte del costo (bloque 1). El IVA se liquida mes a mes (bloque 5). Fuera: tasa cero,
  exoneraciones con documento, prorrata y retenciones de IVA por pagos con tarjeta.
- **Regla 3 (retenciones):** la única retención que aplica es la del **2 % del sector público**
  (Ley del Impuesto sobre la Renta, art. 23, inciso g), sobre el monto sin IVA, solo en operaciones
  que superan la base mínima (un salario base, ₡462 200 en 2026; reglamento, art. 30, inciso vii) y
  solo para clientes marcados como sector público. Si no corresponde, el sistema no ofrece la
  retención (bloque 1). Fuera: retenciones por tarjeta, remesas al exterior, salarios, dividendos y
  dietas.
- **Regla 6 (presupuesto):** ejecutado = costo de los gastos confirmados: monto sin IVA, más el IVA
  solo cuando es no acreditable (bloque 6).
- **Regla 8 (reportes):** agrega cuentas por cobrar por cliente con antigüedad de saldos y la
  antigüedad en cuentas por pagar por proveedor (bloque 6); el flujo de efectivo sale de los asientos.
- **Regla 12 (códigos):** agrega los de documentos: `REC-`, `GTO-`, `ABO-`, `PAG-` (ya existen) y los
  nuevos `TRA-000001` (traspaso), `LIQ-000001` (liquidación de IVA) y `NCR-000001` (nota de crédito).
- **Regla 13 (datos iniciales):** tarifas IVA 13 %, IVA 2 %, Exento y No sujeto; tipo de retención
  "Retención 2 % sector público" precargado.
- **Modelo de datos:** agrega las tablas y columnas nuevas de este paquete.
- **README.md:** que refleje traspasos, liquidación del IVA y los reportes nuevos; sin balance general.

## Bloque 1. IVA y retenciones (lo que faltó del paquete anterior)

1. **Tarifas (RF-54):** carga por migración (idempotente; si ya existen, no dupliques) "IVA 2 %"
   (2 %) y "No sujeto" (0 %) en TiposImpuesto. No cargues otras tarifas reducidas ni tasa cero.
2. **Códigos de Hacienda de las tarifas (`HaciendaRateCode`):**
   - IVA 13 % → `08` (tarifa general; ya está).
   - **Exento → `10` "Tarifa Exenta".** Hoy tiene `01`, que en el Anexo 4.4 es "Tarifa 0 %
     (Artículo 32, num 1, RLIVA)", es decir, la tasa cero que dejamos fuera. Corrígelo en
     TiposImpuesto **y en la fotografía de las líneas de factura existentes** (`Tax_HaciendaRateCode`):
     es la corrección de un código mal asignado, no un cambio de tarifa.
   - IVA 2 % → `03` (tarifa reducida 2 %).
   - No sujeto: verifica en "Anexos y Estructuras versión 4.4" (nota 8.1 y sección de resumen) si
     existe un código de tarifa. Si no existe, permite `HaciendaRateCode` vacío **solo** para esa
     tarifa: sus líneas se exportan sin impuesto y se suman en los totales de no sujetos (bloque 2).
     Deja la decisión en el ADR.
   - Una tarifa que usa alguna condición de IVA de cliente (punto 3) no se puede inactivar.
3. **Condición de IVA del cliente (RF-93):** columna `VatCondition int` en Clientes, obligatoria,
   predeterminada 1:
   - 1 **Normal:** cada línea toma la tarifa predeterminada del producto o servicio
     (`DefaultTaxTypeId`), o IVA 13 % si no tiene; el usuario puede cambiarla en la línea, pero la
     lista **no muestra** las tarifas que pertenecen a otra condición (IVA 2 % y No sujeto), y el
     servidor las rechaza para estos clientes. Tampoco se ofrecen como tarifa predeterminada de un
     producto o servicio.
   - 2 **Institución estatal de educación superior:** todas las líneas al **IVA 2 %** (Ley 9635,
     art. 11, inciso 2.d; por ejemplo UCR, TEC, UNA, UNED y UTN).
   - 3 **No sujeto:** todas las líneas como **No sujeto** (por ejemplo CCSS y municipalidades).

   En las condiciones 2 y 3 la tarifa de la línea **no se puede cambiar** y el selector no se
   muestra: la define el cliente. Regla general: la factura nunca ofrece una tarifa que no corresponde
   a la condición del cliente. Si se cambia el cliente de un borrador, el sistema recalcula la tarifa
   de todas sus líneas según la nueva condición y lo informa. Las facturas confirmadas conservan su
   fotografía. La condición se administra en el formulario de clientes (Mantenimiento) y se muestra en
   el encabezado de la factura. Migración: los clientes existentes quedan en 1 Normal.

   **Cambios de tasa:** cada línea conserva el porcentaje vigente **cuando se agregó**, también en
   borrador. Si después cambia el porcentaje de una tarifa, los borradores **no** se recalculan ni se
   advierte nada al confirmar: no implementes esa lógica. Solo una línea que el usuario agrega o
   reemplaza toma el porcentaje vigente en ese momento. La única recalculación automática es la del
   cambio de cliente descrita arriba, porque cambia la condición.
4. **Retención del 2 % del sector público (RF-95):** la retención la aplica el cliente; el sistema
   solo la ofrece cuando corresponde y nunca en otro caso.
   - **TiposRetencion:** columna `MinimumBase decimal(18,2)`, opcional: base mínima de la operación
     para que el tipo aplique. Carga por migración (idempotente) el tipo "Retención 2 % sector
     público", tarifa 2 %, `MinimumBase` = 462 200 (un salario base de 2026). Gerencia y Finanzas
     pueden actualizar la base mínima cuando cambie el salario base.
   - **Clientes:** columna `AppliesStateWithholding bit`, obligatoria, predeterminada 0: "Retiene
     2 % (sector público)". Se administra en el formulario de clientes.
   - **Al registrar un abono:** la opción de retención **solo se muestra y solo se acepta** en las
     facturas de un cliente con la marca y cuyo monto neto (sin IVA) supera la `MinimumBase` del tipo.
     En cualquier otro caso no se muestra, y el servidor rechaza una retención enviada igualmente.
   - Cuando corresponde, el sistema la **propone** con el tipo "Retención 2 % sector público" y una
     base proporcional a lo que se paga: `base = neto × monto aplicado / (total − 2 % × neto)`, sin
     superar el neto que aún no ha tenido retención; si el abono salda la cuenta, la base es ese neto
     pendiente. El usuario puede ajustar la base dentro de ese límite.
   - Las retenciones de otros tipos del catálogo siguen la misma regla: solo para clientes con la
     marca y operaciones sobre su base mínima, si la tienen.
   - **Supuesto del sistema:** los clientes retienen correctamente (solo cuando corresponde y por el
     monto que corresponde). Los errores de retención de un cliente quedan fuera del sistema; no
     construyas flujos para ese caso.
5. **IVA no acreditable en gastos (RF-94):** columna `VatCreditable bit` en Gastos, obligatoria,
   predeterminada en 1 (sí), editable en el borrador, visible en el formulario y en el detalle
   ("IVA acreditable: Sí/No"). Si es 0, el IVA no se acredita: el asiento debita la cuenta de la
   categoría por el **total** (neto + IVA) y no tiene línea de IVA acreditable. Se usa para compras
   ajenas a la actividad gravada o cuyo comprobante no permite acreditar el IVA; ese IVA lo asume la
   empresa como costo. Hoy `JournalPostingService.PostExpenseConfirmationAsync` siempre debita IVA
   acreditable: ajústalo. Los gastos existentes quedan en 1. Si hay gastos confirmados con asiento,
   no los regeneres.
6. **Una sola pantalla "Impuestos y retenciones"** (módulo Gestionar Impuestos y Retenciones de la
   Tabla 4). Reemplaza las pantallas separadas de tipos de impuesto y de retención:
   - Un solo elemento de menú, "Impuestos y retenciones", con dos secciones en la misma página:
     tarifas de IVA (código, nombre, código de Hacienda, porcentaje, estado) y tipos de retención
     (código, nombre, porcentaje, base mínima, estado).
   - Finanzas y Gerencia editan el porcentaje, la base mínima, la descripción y el estado; las demás
     personas solo consultan. Las tarifas y la retención que usan la lógica del sistema (IVA 13 %,
     IVA 2 %, Exento, No sujeto y la retención del 2 % del sector público) no se pueden inactivar.
   - La opción de **agregar** queda con poco protagonismo: un enlace secundario al final de cada
     sección, solo para Gerencia, con una nota de que los impuestos y retenciones nuevos se aplican
     con las reglas generales del sistema. Al agregar una tarifa se exige el código de Hacienda de una
     lista fija; una tarifa nueva solo queda disponible para clientes de condición Normal.
   - La pantalla de liquidación del IVA (bloque 5) vive en este mismo módulo.
   - Mantén las rutas anteriores redirigiendo a la nueva pantalla y actualiza las pruebas de
     autorización.

## Bloque 2. Datos del formato 4.4 que faltaron

No se emite, firma ni transmite nada ante Hacienda. Toma los códigos del documento "Anexos y
Estructuras versión 4.4"; si no tienes un catálogo exacto, déjalo como lista fija con los valores de
uso común y márcalo como pendiente de validar en el ADR.

1. **Clientes:** `ActivityCode nvarchar(10)`, opcional: código de actividad económica del receptor,
   que algunos clientes exigen (la UCR pide 8530.1). En el formulario de clientes.
2. **ParametrosSistema (datos del emisor):** solo Gerencia, con su historial en
   ParametrosEmpresaHistorial (agrega ahí las mismas columnas):
   - `CompanyTradeName nvarchar(100)`, opcional: nombre comercial.
   - `CompanyProvinceCode nvarchar(1)`, `CompanyCantonCode nvarchar(2)` y `CompanyDistrictCode
     nvarchar(2)`: ubicación codificada según la división territorial del formato 4.4 (provincia,
     cantón, distrito, en listas dependientes). `CompanyAddress` pasa a ser "otras señas". Si no tienes
     el catálogo completo de cantones y distritos, valida solo el formato, carga las provincias y deja
     el catálogo marcado como pendiente en el ADR.
3. **Exportar datos (RF-88), completar:** el JSON y el Excel agregan:
   - En el emisor: nombre comercial, ubicación codificada y otras señas.
   - En el receptor: actividad económica.
   - En el resumen: totales de servicios y de mercancías **no sujetos**, separados de los exentos (hoy
     todo lo que tiene tarifa 0 se cuenta como exento).
   - En las líneas con descuento: el código oficial del descuento. Verifica en el Anexo 4.4 (nota 20)
     si existe un código de "descuento comercial"; si no existe, usa `99` "Otros" con la naturaleza
     "Descuento comercial". Deja la decisión en el ADR.
   - La referencia interna a la factura (número y fecha) y, si existen, la clave y el consecutivo
     oficiales (verifica que ya estén).
4. **Preparación para el recibo electrónico de pago (sin emitirlo):** en el detalle de la cuenta por
   cobrar, cada abono muestra su código, fecha, la factura, su clave oficial (si existe), el monto
   aplicado, la retención y el saldo resultante. No agregues campos nuevos para esto.

## Bloque 3. Regla de creación de cuentas contables

Hoy `LedgerAccountService.CreateAsync` acepta como cuenta superior cualquier cuenta de Ingreso o de
Gasto que no sea de sistema, incluido un grupo de primer nivel o una cuenta de detalle con movimientos.
Eso produce dos errores: bajo "4" genera el código "4.05" en lugar de "4.5" (el sufijo siempre se
rellena a dos dígitos), y una cuenta de detalle con categoría o asientos puede recibir hijas y quedar
a la vez como grupo y como cuenta de movimiento, que en contabilidad no se permite.

1. Las cuentas nuevas solo se crean bajo uno de estos grupos de segundo nivel: **1.1, 4.1, 4.2, 4.3,
   5.1, 5.2 y 5.3**. Siempre quedan en el tercer nivel (`4.1.04`). Define la lista permitida en un solo
   lugar del dominio y explícala en el ADR. 4.4 queda fuera porque solo contiene la cuenta de sistema
   de devoluciones.
2. Una cuenta nueva bajo 1.1 siempre es de efectivo: `CashKind` (Caja o Banco) pasa a ser obligatorio
   en ese caso.
3. El formulario solo ofrece esos grupos como cuenta superior. El servidor rechaza cualquier otro con
   el mensaje "Las cuentas nuevas solo se crean dentro de un grupo de segundo nivel de Efectivo,
   Ingresos o Gastos."
4. No borres ni muevas cuentas existentes. Si encuentras alguna creada fuera de esta regla, repórtala
   en el resumen del bloque.

## Bloque 4. Traspaso entre cuentas de efectivo (RF-96)

Un traspaso mueve dinero entre dos cuentas de efectivo propias: depositar la caja chica en el banco,
retirar del banco para reponer la caja, o pasar dinero entre dos cuentas bancarias. No es ingreso ni
gasto: no toca categorías, presupuesto ni estado de resultados.

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
3. **Asiento** (`SourceType` 8 Traspaso): Débito cuenta de destino / Crédito cuenta de origen, con la
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

## Bloque 5. Liquidación mensual del IVA (RF-97)

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
     (`SourceType` 9), menos la suma de
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
4. **Asiento** (`SourceType` 9 Liquidación de IVA), con fecha `SettlementDate`: Débito IVA por pagar
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

## Bloque 6. Presupuesto y reportes con criterios coherentes

1. **Presupuesto con el costo real:** el ejecutado y la alerta al confirmar un gasto usan el **costo**
   de cada gasto confirmado: `NetAmount` cuando el IVA es acreditable y `NetAmount + TaxAmount` cuando
   es no acreditable. Aplica también al reporte de presupuesto contra lo ejecutado, al anual a la
   fecha, al reporte de gastos por categoría y al semáforo, de modo que siempre coincidan con el
   estado de resultados.
2. **Pantalla de presupuesto:** filtro "Solo con presupuesto" (predeterminado) o "Todas".
3. **Presupuesto anual a la fecha (RF-87):** reporte nuevo con los parámetros año y "hasta el mes"
   (por defecto, el mes actual): por cada categoría de gasto de último nivel, presupuesto acumulado y
   ejecutado acumulado de enero hasta ese mes, disponible, variación y variación %, más el presupuesto
   total del año como referencia. Es solo una vista: el presupuesto se sigue registrando a mano, mes
   por mes y categoría por categoría (no agregues opciones para copiar o repartir montos entre meses).
4. **Comparativo mensual de ingresos y gastos:** hoy suma los ingresos cuando entra el dinero y los
   gastos cuando se confirman, y por eso no coincide con el estado de resultados. Calcúlalo desde los
   asientos (cuentas tipo 4 contra tipo 5, sin IVA) para cada mes del rango.
5. **Flujo de efectivo mensual (RF-86):** reporte nuevo calculado desde las líneas de asiento de las
   **cuentas de efectivo**, por mes y con el neto: entradas (débitos) y salidas (créditos) agrupadas
   por origen: facturas de contado, abonos, ingresos manuales, gastos de contado, pagos a proveedores,
   pago de IVA, devoluciones a clientes (bloque 8) y traspasos. Filtro opcional por cuenta de efectivo.
   Sin filtro, los traspasos se excluyen porque se compensan entre cuentas; con filtro, aparecen como
   "Traspasos recibidos" y "Traspasos enviados". Las anulaciones se compensan con su reversión. Pie:
   "Calculado a partir de las operaciones registradas en el sistema; no incluye saldos iniciales."
6. **IVA del período (RF-84):** rango de fechas; por código de tarifa: base e IVA de facturas
   confirmadas (IVA cobrado, incluidas las ventas al 2 % y las no sujetas con IVA cero) y de gastos
   confirmados con IVA acreditable (IVA pagado), y la diferencia. El IVA de los gastos no acreditables
   se muestra aparte, como informativo, sin restarlo. Excluye borradores y anulados. Los totales deben
   coincidir con los movimientos del período en IVA por pagar e IVA acreditable **sin contar los
   asientos de liquidación**. Si el rango es un mes con liquidación vigente, muestra su código y su
   resultado (pago o saldo a favor). Pie: "Apoyo para la declaración; no sustituye la declaración ante
   Hacienda."
7. **Retenciones aplicadas por clientes (RF-85):** rango de fechas; por cliente: abono, factura, tipo
   de retención, base y monto, solo de abonos vigentes, con el total.
8. **Cuentas por cobrar por cliente con antigüedad de saldos (RF-98):** reporte nuevo, a la fecha de
   hoy, de las cuentas por cobrar con saldo (pendientes o con abono parcial), agrupadas por cliente:
   factura, fecha, vencimiento, monto original, saldo y días de atraso, con el saldo ubicado en una de
   estas columnas: Al día (no vencida), 1 a 30, 31 a 60, 61 a 90 y Más de 90 días. Subtotal por
   cliente y total general por columna. Filtro opcional por cliente.
9. **Cuentas por pagar por proveedor:** agrega las mismas columnas de antigüedad al reporte existente
   (por proveedor).
10. Renombra el reporte "Ingresos" a "Ingresos recibidos" para dejar claro que son entradas de
    dinero, distintas de las ventas del estado de resultados.
11. **Semáforo de ejecución (RF-92):** en la pantalla de presupuesto, en el reporte de presupuesto
    contra lo ejecutado y en el reporte anual a la fecha, cada categoría muestra el porcentaje de
    ejecución con un indicador: verde por debajo del 80 %, amarillo del 80 % al 100 % y rojo por
    encima del 100 %. El color va acompañado del texto (Normal, Atención, Excedido), no solo del
    color. En Excel y PDF se muestra el texto.
12. **Indicador de presupuestos excedidos (RF-92):** en el inicio, para Gerencia y Finanzas, junto al
    indicador de cuentas vencidas: cantidad de categorías del mes actual en rojo y el monto total
    excedido, con enlace a la pantalla de presupuesto filtrada. No hay notificaciones en tiempo real;
    el excedente nunca bloquea el gasto.

Todos los reportes nuevos: Gerencia y Finanzas; en pantalla, PDF y Excel, con la infraestructura y el
encabezado actuales; sin datos no se genera archivo. Actualiza la pantalla de perfiles y permisos.

## Bloque 7. Nota de crédito interna con devolución de dinero

Una **anulación** corrige un error (la factura nunca debió existir). Una **nota de crédito** reduce
una venta real después de facturar: el cliente devolvió un equipo, hubo un descuento posterior o se
facturó de más. La nota es un documento interno: no sustituye la nota de crédito electrónica oficial,
que se emite en la plataforma externa.

1. **Tablas** (esquema `finanzas`):
   - `NotasCredito` (clase `CreditNote`): `Id`, `Code` (`NCR-000001`, secuencia
     `finanzas.Seq_NotaCreditoCodigo`, único), `InvoiceId` (FK), `IssueDate date`,
     `Reason nvarchar(300)` obligatorio, `NetAmount`, `TaxAmount`, `TotalAmount`, `AppliedToBalance`
     (parte que rebaja la cuenta por cobrar), `RefundedAmount` (parte devuelta en dinero),
     `CashAccountId` y `RefundReference nvarchar(100)` (obligatorios solo si hay devolución),
     `ElectronicCreditNoteKey nvarchar(50)` opcional (clave de la nota oficial, igual criterio que en
     facturas), `Status` (1 Vigente, 2 Anulada), campos de anulación y auditoría, `RowVersion`.
     CHECK: `TotalAmount = AppliedToBalance + RefundedAmount`.
   - `NotaCreditoLineas` (clase `CreditNoteLine`): `Id`, `CreditNoteId`, `InvoiceLineId` (FK),
     `NetAmount`, `TaxRate`, `TaxAmount`, `TotalAmount`. El IVA se calcula con la tarifa conservada en
     la línea de la factura, para que una factura con varias tarifas se acredite correctamente.
2. **Registro (RF-90):** Finanzas y Gerencia, sobre una factura **confirmada** (de contado o a
   crédito). El usuario indica la fecha, el motivo y, por cada línea, el monto neto a acreditar. Lo
   acreditado acumulado por línea (notas vigentes) nunca supera el neto de esa línea.
3. **Reparto automático y devolución (RF-91):**
   - Factura a crédito: la nota rebaja primero el saldo pendiente de la cuenta por cobrar
     (`AppliedToBalance`). Si el total de la nota supera ese saldo porque el cliente ya pagó, la
     diferencia es una **devolución de dinero** en la misma operación (`RefundedAmount`), con cuenta de
     efectivo y referencia.
   - Factura de contado: todo el total de la nota es devolución.
   - **No existen saldos a favor del cliente:** lo que no rebaja el saldo se devuelve en ese mismo
     momento.
   - La devolución nunca supera el dinero efectivamente recibido por esa factura (total de contado o
     abonos vigentes) menos lo ya devuelto. Si la factura tiene retenciones vigentes, la nota solo
     puede rebajar el saldo pendiente, sin devolución.
   - Una devolución que supera el límite de autorización solo la registra Gerencia.
   - El estado de la cuenta por cobrar se recalcula con las reglas actuales.
4. **Asiento** (`SourceType` 7 Nota de crédito; el número 7 queda reservado para este origen aunque
   los traspasos y las liquidaciones usen 8 y 9): Débito Devoluciones y descuentos sobre ventas (neto)
   / Débito IVA por pagar (IVA) / Crédito Cuentas por cobrar (`AppliedToBalance`) / Crédito cuenta de
   efectivo (`RefundedAmount`). Se omiten las líneas en cero.
5. **Anulación:** solo Gerencia, con motivo; genera la reversión del asiento, restituye el saldo
   aplicado en la cuenta por cobrar y revierte la devolución. Una factura con notas de crédito
   vigentes no se anula hasta anular primero esas notas. Respeta la fecha de cierre en el registro y en
   la anulación.
6. **Pantallas:** en el detalle de la factura, "Registrar nota de crédito" y la lista de sus notas;
   listado propio de notas de crédito con filtros (código, cliente, factura, fecha, estado); detalle
   con "Ver asiento"; PDF interno de la nota con la leyenda "Nota de crédito interna. No sustituye la
   nota de crédito electrónica oficial." y "ANULADA" con el motivo cuando corresponda.
7. **Efecto en los reportes:** el estado de cuenta del cliente y la antigüedad de cuentas por cobrar
   reflejan las notas; el IVA del período resta el IVA de las notas vigentes por tarifa; el flujo de
   efectivo cuenta las devoluciones como salidas; el estado de resultados las refleja mediante la
   cuenta de devoluciones; la liquidación del IVA las incluye sola, porque debitan IVA por pagar. El
   presupuesto no cambia: es solo de gastos.
8. **Bitácora:** registrar y anular una nota quedan en BitacoraOperaciones (nuevo valor `CreditNote`
   en `OperationLogEntityType`). Extiende el comando de generación de asientos y "Ver asiento".

## Bloque 8. Cierre del paquete

1. Revisa que `docs/02-requisitos-trazabilidad.md` tenga RF-78 a RF-98 con su estado real.
2. Actualiza `docs/05-modelo-datos.md` con las tablas y columnas nuevas de este paquete.
3. Prueba de extremo a extremo con datos de ejemplo, y dime el resultado de cada paso:
   factura a un cliente de condición Normal con dos tarifas → abono → nota de crédito con
   devolución; factura a una universidad estatal (IVA 2 %) con retención del 2 % en el abono;
   gasto con IVA no acreditable; traspaso del banco a la caja chica; liquidación del IVA del mes;
   y comprueba que la balanza cuadra, que el estado de resultados coincide con el comparativo
   mensual, que el IVA del período coincide con los movimientos de las cuentas de IVA y que el
   flujo de efectivo coincide con el libro mayor de las cuentas de efectivo.
4. Resumen final: qué quedó hecho, qué quedó pendiente y cualquier decisión que hayas tomado sin
   consultarme.
