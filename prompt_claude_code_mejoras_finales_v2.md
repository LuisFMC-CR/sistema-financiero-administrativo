# Mejoras finales de la lógica del sistema

Este es el último paquete de cambios de lógica antes de cerrar el documento académico. Ya fue
revisado y aprobado. Reglas para todo el trabajo:

- Trabaja por **bloques (0 a 8), en el orden indicado**. Al terminar cada bloque: migración explícita (si
  aplica), pruebas unitarias y de integración, ADR, actualización de
  `docs/02-requisitos-trazabilidad.md`, `dotnet test` completo en verde, commit y un resumen corto.
  **Espera mi "continúa" antes de empezar el siguiente bloque.**
- Usa **exactamente** los nombres de tablas, columnas y clases indicados: ya están (o estarán) en el
  diccionario de datos del documento. Si necesitas un nombre que no está aquí, pregúntame.
- Ninguna operación nueva puede saltarse la fecha de cierre, el límite de autorización, los permisos
  por perfil ni la regla de "nada se elimina físicamente".
- Interfaz y mensajes en español; montos en colones con 2 decimales; fechas dd/mm/aaaa.
- Si algo de este documento contradice `CLAUDE.md`, manda este documento (el bloque 0 actualiza
  `CLAUDE.md`). Si algo no está definido, pregúntame antes de decidir.
- Antes de eliminar o renombrar código existente, explica el impacto y espera mi confirmación.
- No agregues paquetes NuGet nuevos.

Códigos de trazabilidad nuevos para `docs/02` (coinciden con el documento académico):
RF-78 asientos automáticos · RF-79 reversión de asientos · RF-80 libro diario · RF-81 libro mayor ·
RF-82 balanza de comprobación · RF-83 estado de resultados · RF-84 reporte de IVA del período ·
RF-85 reporte de retenciones · RF-86 flujo de efectivo mensual · RF-87 presupuesto anual a la fecha ·
RF-88 exportación de datos de la factura · RF-89 clave del comprobante electrónico oficial ·
RF-90 nota de crédito interna · RF-91 devolución de dinero al cliente · RF-92 semáforo e indicador
de presupuestos excedidos.

---

## Bloque 0. Actualizar `CLAUDE.md`

Ajusta `CLAUDE.md` para que refleje este paquete (muéstrame el diff antes del commit):

- **Alcance, fuera del sistema:** la partida doble deja de estar fuera. Sigue fuera: balance general,
  asientos manuales, saldos iniciales, préstamos, aportes de socios, conciliación bancaria, emisión
  o firma de comprobantes ante Hacienda, declaraciones tributarias, inventario y moneda extranjera.
- **Regla 1 (cuentas contables):** catálogo con tipos 1 Activo, 2 Pasivo, 3 Patrimonio, 4 Ingreso y
  5 Gasto, y cuentas de sistema (bloque 4).
- **Regla 6 (presupuesto):** ejecutado = gastos confirmados **sin IVA** (bloque 7).
- **Regla 8 (reportes):** agrega los reportes contables y tributarios de los bloques 6 y 7.
- **Regla 4 (estados y correcciones):** agrega la nota de crédito interna (bloque 8): una anulación
  corrige un error; una nota de crédito reduce una venta real (devolución o descuento posterior).
- **Modelo de datos y Estado:** agrega las tablas y columnas nuevas y deja el orden de este paquete
  como trabajo pendiente.
- **README.md:** hoy dice que el sistema no implementa partida doble ni estados financieros. Ajústalo
  a lo que el sistema hará después de este paquete (asientos automáticos, libros, balanza y estado de
  resultados; sin balance general).

## Bloque 1. Pendientes que el documento ya promete

1. **Gastos:** columna `VoucherReference nvarchar(100)`, obligatoria (RF-25, CU-14): referencia del
   comprobante electrónico del proveedor. Se pide en el formulario del gasto, se muestra en su
   detalle y en el listado y detalle de cuentas por pagar.
2. **EventosSeguridad (RF-77):** registrar inicio de sesión exitoso, intento fallido y bloqueo de la
   cuenta (nuevos valores de `SecurityAuditAction`). Hoy el inicio de sesión solo va al log.
3. **Datos iniciales (RF-54):** cargar por migración las tarifas "IVA 13 %" y "Exento" en
   TiposImpuesto (idempotente; si ya existen por la carga manual, no duplicar).
4. **Factura en PDF (regla 15):** mostrar la identificación del cliente (y su correo y teléfono si
   existen) y, en cada línea, el código del producto o servicio cuando la línea viene del catálogo.
5. **Código propio por tipo de documento:** hoy los ingresos y los gastos usan los mismos prefijos
   que las categorías (ING-001, GAS-001) y se confunden en Consultas. Las categorías conservan los
   suyos; los documentos pasan a tener su propio prefijo, generado por secuencia al crear el
   registro y no editable:
   - Ingresos: `REC-000001` (recibo de dinero).
   - Gastos: `GTO-000001`.
   - Abonos: columna nueva `Code nvarchar(30)`, única: `ABO-000001` (una por fila, también en un
     abono a varias facturas).
   - Pagos: columna nueva `Code nvarchar(30)`, única: `PAG-000001` (igual criterio).
   Migra los códigos existentes de ingresos y gastos al prefijo nuevo según su orden de creación y
   asigna códigos a los abonos y pagos existentes. Muestra el código en listados, detalles, Consultas
   y reportes.

## Bloque 2. Categoría de ingreso en la factura

La categoría de una venta pertenece a la factura, no a cada cobro.

1. **Facturas:** columna `IncomeCategoryId` (FK a CategoriasFinancieras, categoría de ingreso
   activa), obligatoria en el encabezado al crear o editar el borrador. Si la primera línea viene
   de un producto o servicio con `DefaultIncomeCategoryId`, propónla como valor inicial.
2. El ingreso de una factura de contado y los ingresos de cada abono **heredan** la categoría de la
   factura. Quita la categoría del formulario de confirmación de contado y del formulario de abono.
3. Migración de datos de desarrollo: asigna `IncomeCategoryId` a las facturas existentes a partir de
   sus ingresos (si los tienen) y explícame qué hiciste con las que no tengan.

## Bloque 3. Factura preparada para el formato 4.4 de Hacienda

No se emite, firma ni transmite nada ante Hacienda: solo se guardan los datos con los códigos
oficiales para facilitar el traslado a la plataforma externa. Toma los códigos del documento
"Anexos y Estructuras versión 4.4" de Hacienda (TRIBU-CR). Si no tienes el catálogo exacto de una
lista, déjala como lista fija con los valores de uso común y márcala como pendiente de validar en el
ADR.

1. **ProductosServicios:** `CabysCode nvarchar(13)`, obligatorio, exactamente 13 dígitos.
   `UnitOfMeasure` pasa a ser una lista fija de códigos oficiales de unidad de medida (por ejemplo,
   unidad, hora, servicios profesionales, otros servicios); migra los valores de texto existentes al
   código más cercano y explícame la correspondencia.
2. **FacturaLineas:** `CabysCode nvarchar(13)`, copiado del catálogo al agregar la línea (fotografía,
   igual que la descripción y la unidad); vacío solo si la línea no viene del catálogo.
3. **Clientes y Proveedores:** `IdentificationType int` (1 Física, 2 Jurídica, 3 DIMEX, 4 NITE,
   5 Extranjero no domiciliado), obligatorio, con validación del número según el tipo (física 9
   dígitos, jurídica 10, DIMEX 11 o 12, NITE 10, extranjero texto libre). Migra los registros
   existentes con un tipo deducido de la longitud y explícame los casos dudosos.
4. **TiposImpuesto:** `HaciendaRateCode nvarchar(2)`, obligatorio: código oficial de la tarifa de
   IVA. Asigna los de "IVA 13 %" y "Exento".
5. **ParametrosSistema:** `CompanyActivityCode nvarchar(10)`: código de actividad económica de la
   empresa (datos de la empresa, solo Gerencia, con su historial en ParametrosEmpresaHistorial).
6. **Equivalencias en código (sin columnas nuevas):** `PaymentMethod` → código oficial de medio de
   pago; `PaymentTerm` → condición de venta (contado o crédito) y plazo en días.
7. **Facturas (RF-89):** `ElectronicInvoiceKey nvarchar(50)` y `ElectronicInvoiceNumber
   nvarchar(20)`, opcionales: clave y consecutivo del comprobante emitido en la plataforma externa.
   Se registran después de confirmar (Finanzas y Gerencia), solo una vez por factura, validando 50 y
   20 dígitos; se muestran en el detalle y en el PDF, y se puede buscar por clave.
8. **Exportar datos (RF-88):** en una factura confirmada, "Exportar datos" genera un JSON con las
   secciones del comprobante: emisor (datos de la empresa y actividad), receptor (tipo y número de
   identificación, nombre, correo), condición de venta y plazo, medio de pago, líneas (número,
   CABYS, código comercial, descripción, cantidad, unidad, precio unitario, descuento, base, código
   de tarifa, tarifa, IVA, total) y resumen (totales de servicios y de mercancías, gravados y
   exentos; total de descuentos; venta neta; IVA desglosado por tarifa; total del comprobante). No
   generes XML oficial ni clave ni consecutivo oficial. Agrega también la exportación a Excel de las
   facturas confirmadas de un período con esos mismos datos por línea.

## Bloque 4. Catálogo contable ampliado

1. `LedgerAccountType`: 1 Activo, 2 Pasivo, 3 Patrimonio, 4 Ingreso, 5 Gasto.
2. **Renumera el catálogo base** a la convención 1 Activo, 2 Pasivo, 3 Patrimonio, 4 Ingresos,
   5 Gastos (los Id no cambian, así que las categorías siguen apuntando a sus cuentas). Resultado:
   - 1 Activo › 1.1 Efectivo › 1.1.01 Caja chica, 1.1.02 Cuenta bancaria principal;
     1.2 Cuentas por cobrar › 1.2.01 Cuentas por cobrar a clientes;
     1.3 Impuestos a favor › 1.3.01 IVA acreditable, 1.3.02 Retenciones de renta a favor.
   - 2 Pasivo › 2.1 Cuentas por pagar › 2.1.01 Cuentas por pagar a proveedores;
     2.2 Impuestos por pagar › 2.2.01 IVA por pagar.
   - 3 Patrimonio (solo el grupo, sin cuentas de detalle en esta etapa).
   - 4 Ingresos › 4.1 Ingresos por servicios › 4.1.01 Soporte técnico, 4.1.02 Mantenimiento y
     reparación de equipos, 4.1.03 Instalación de infraestructura informática;
     4.2 Ingresos por ventas › 4.2.01 Venta de equipos y repuestos;
     4.3 Otros ingresos › 4.3.01 Otros ingresos de la operación, 4.3.02 Ajustes a favor en cuentas
     por pagar;
     4.4 Devoluciones y descuentos sobre ventas › 4.4.01 Devoluciones y descuentos sobre ventas
     (cuenta de ingreso de naturaleza deudora: resta de los ingresos en el estado de resultados).
   - 5 Gastos › 5.1 Gastos de operación › 5.1.01 a 5.1.06 (las mismas de hoy);
     5.2 Gastos administrativos › 5.2.01 a 5.2.04 (las mismas de hoy), 5.2.05 Ajustes a cuentas por
     cobrar;
     5.3 Costo de ventas › 5.3.01 Compras de equipos para la venta (servidores, laptops, UPS y demás
     equipos que se compran para revender; sin inventario, la compra se reconoce como costo al
     confirmar el gasto). Esta cuenta sí lleva su categoría de gasto, con código automático, para
     poder usarla en gastos y presupuesto.
3. **Cuentas de sistema:** columna `SystemRole int` en CuentasContables (nula en las demás, única
   cuando no es nula) con los roles: cuentas por cobrar a clientes, IVA acreditable, retenciones a
   favor, cuentas por pagar a proveedores, IVA por pagar, ajustes a cuentas por cobrar, ajustes a
   favor en cuentas por pagar y devoluciones y descuentos sobre ventas. Las reglas de contabilización
   buscan estas cuentas por su rol, nunca por código o nombre. Son protegidas: se pueden renombrar,
   pero no inactivar ni mover.
4. Las cuentas de sistema **no** tienen categoría financiera y no aparecen para elegir en categorías,
   ingresos ni gastos. El usuario solo puede crear cuentas de detalle dentro de 1.1 Efectivo,
   4 Ingresos y 5 Gastos.

## Bloque 5. Asientos contables automáticos (partida doble)

1. **Tablas** (esquema `finanzas`):
   - `Asientos` (clase `JournalEntry`): `Id`, `Number` (consecutivo por secuencia
     `finanzas.Seq_AsientoNumero`), `EntryDate date`, `Description nvarchar(300)`,
     `SourceType int` (1 Factura, 2 Abono, 3 Ingreso manual, 4 Gasto, 5 Pago, 6 Ajuste de saldo),
     `SourceId uniqueidentifier`, `ReversesEntryId` (FK a Asientos, nula salvo en reversiones),
     `CreatedAtUtc`, `CreatedByUserId`.
   - `AsientoLineas` (clase `JournalEntryLine`): `Id`, `JournalEntryId`, `LineNumber`,
     `AccountId` (FK a CuentasContables, cuenta de detalle), `Debit decimal(18,2)`,
     `Credit decimal(18,2)`; CHECK: ambos ≥ 0 y exactamente uno mayor que cero.
   - La descripción indica el tipo y el número o código del documento de origen (por ejemplo,
     "Factura 25" o "Abono ABO-000003").
   - Inmutables: no se editan ni se eliminan. El dominio exige al menos dos líneas y
     suma de débitos = suma de créditos antes de guardar. Se omiten las líneas en cero (por
     ejemplo, IVA de una tarifa exenta).
2. **Reglas de contabilización (RF-78)**, generadas en la **misma transacción** de la operación, con
   la fecha de negocio del documento:
   - Factura confirmada de contado: Débito cuenta de efectivo (total) / Crédito cuenta de la
     categoría de ingreso de la factura (neto) / Crédito IVA por pagar (IVA).
   - Factura confirmada a crédito: Débito Cuentas por cobrar (total) / Crédito cuenta de la
     categoría (neto) / Crédito IVA por pagar (IVA).
   - Abono: Débito cuenta de efectivo (monto) / Débito Retenciones a favor (retención, si hay) /
     Crédito Cuentas por cobrar (monto + retención). El ingreso que genera el abono **no** tiene
     asiento propio.
   - Ingreso manual: Débito cuenta de efectivo / Crédito cuenta de su categoría.
   - Gasto confirmado de contado: Débito cuenta de la categoría de gasto (neto) / Débito IVA
     acreditable (IVA) / Crédito cuenta de efectivo (total).
   - Gasto confirmado a crédito: Débito cuenta de la categoría (neto) / Débito IVA acreditable
     (IVA) / Crédito Cuentas por pagar (total).
   - Pago a proveedor: Débito Cuentas por pagar / Crédito cuenta de efectivo.
   - Ajuste de cuenta por cobrar: disminución → Débito Ajustes a cuentas por cobrar / Crédito
     Cuentas por cobrar; aumento → al revés. Fecha: la del ajuste.
   - Ajuste de cuenta por pagar: disminución → Débito Cuentas por pagar / Crédito Ajustes a favor en
     cuentas por pagar; aumento → al revés. Fecha: la del ajuste.
   - Los ingresos de facturas de contado y de abonos no generan asientos propios: su efecto ya está
     en el asiento de la factura o del abono.
3. **Reversión (RF-79):** al anular una factura, gasto, abono, pago o ingreso manual se genera un
   asiento de reversión con las mismas líneas intercambiando débitos y créditos, `ReversesEntryId`
   apuntando al original y **la misma fecha del documento original** (la anulación ya exige que su
   período esté abierto). La anulación en cascada (factura → cuenta por cobrar) genera una sola
   reversión, la del asiento de la factura.
4. **Datos existentes:** un comando explícito e idempotente (no al arrancar) que genere los asientos
   de los documentos ya confirmados y las reversiones de los ya anulados, en orden cronológico.
5. **Trazabilidad:** en el detalle de cada documento, "Ver asiento" muestra su asiento y, si existe,
   su reversión. Las anulaciones y la generación quedan cubiertas por la bitácora existente.

## Bloque 6. Reportes contables

Gerencia y Finanzas; en pantalla, PDF y Excel, con la misma infraestructura y encabezado de los
reportes actuales (nombre, parámetros, fecha y hora, usuario); sin datos no se genera archivo.

1. **Libro diario (RF-80):** rango de fechas; cada asiento con número, fecha, descripción, documento
   de origen y sus líneas; totales de débito y crédito iguales.
2. **Libro mayor (RF-81):** una cuenta y un rango de fechas; saldo anterior (movimientos previos al
   rango), movimientos con su asiento y saldo acumulado según la naturaleza de la cuenta (deudora
   para Activo y Gasto, acreedora para Pasivo, Patrimonio e Ingreso).
3. **Balanza de comprobación (RF-82):** fecha de corte; por cuenta de detalle con movimiento: total
   débitos, total créditos, saldo deudor o acreedor; con subtotales por grupo y totales iguales.
4. **Estado de resultados (RF-83):** rango de fechas; ingresos (tipo 4) y gastos (tipo 5) por
   cuenta con subtotales por grupo, y la utilidad o pérdida del período. Sale de los asientos, por
   lo que no incluye IVA.
5. Indica en el pie de los reportes contables: "Saldos calculados a partir de las operaciones
   registradas en el sistema; no incluye saldos iniciales."
6. Actualiza la pantalla de perfiles y permisos con estos reportes.

## Bloque 7. Presupuesto y reportes con criterios coherentes

1. **Presupuesto sin IVA:** el ejecutado y la alerta al confirmar un gasto usan `NetAmount` de los
   gastos confirmados (el IVA de las compras va a IVA acreditable, no al gasto). Aplica también al
   reporte de presupuesto contra lo ejecutado.
2. **Pantalla de presupuesto:** filtro "Solo con presupuesto" (predeterminado) o "Todas".
3. **Presupuesto anual a la fecha (RF-87):** reporte nuevo con los parámetros año y "hasta el mes"
   (por defecto, el mes actual): por cada categoría de gasto de último nivel, presupuesto acumulado
   y ejecutado acumulado (sin IVA) de enero hasta ese mes, disponible, variación y variación %, más
   el presupuesto total del año como referencia. Es solo una vista: no cambia cómo se registra el
   presupuesto, que sigue siendo manual, mes por mes y categoría por categoría (no agregues opciones
   para copiar o repartir montos entre meses).
4. **Comparativo mensual de ingresos y gastos:** se calcula desde los asientos (cuentas tipo 4
   contra tipo 5, sin IVA) para cada mes del rango, de modo que coincida con el estado de
   resultados.
5. **Flujo de efectivo mensual (RF-86):** reporte nuevo: entradas (ingresos vigentes de facturas de
   contado, abonos e ingresos manuales) contra salidas (pagos vigentes y gastos de contado
   confirmados), con IVA, por fecha del movimiento y por mes, con el neto; filtro opcional por cuenta
   de efectivo.
6. **IVA del período (RF-84):** rango de fechas; por código de tarifa: base e IVA de facturas
   confirmadas (IVA cobrado) y de gastos confirmados (IVA pagado), y la diferencia; excluye
   borradores y anulados; los totales deben coincidir con los movimientos del período en IVA por
   pagar e IVA acreditable. Pie: "Apoyo para la declaración; no sustituye la declaración ante
   Hacienda."
7. **Retenciones aplicadas por clientes (RF-85):** rango de fechas; por cliente: abono, factura, tipo
   de retención, base y monto, solo de abonos vigentes, con el total.
8. Renombra el reporte "Ingresos" a "Ingresos recibidos" para dejar claro que son entradas de
   dinero, distintas de las ventas del estado de resultados.
9. **Semáforo de ejecución (RF-92):** en la pantalla de presupuesto, en el reporte de presupuesto
   contra lo ejecutado y en el reporte anual a la fecha, cada categoría muestra el porcentaje de
   ejecución con un indicador: verde por debajo del 80 %, amarillo del 80 % al 100 % y rojo por
   encima del 100 %. El color va acompañado del texto (Normal, Atención, Excedido), no solo del
   color. En Excel y PDF se muestra el texto.
10. **Indicador de presupuestos excedidos (RF-92):** en el inicio, para Gerencia y Finanzas, junto al
    indicador de cuentas vencidas: cantidad de categorías del mes actual en rojo y el monto total
    excedido, con enlace a la pantalla de presupuesto filtrada. No hay notificaciones en tiempo real;
    el excedente nunca bloquea el gasto.

## Bloque 8. Nota de crédito interna con devolución de dinero

Una **anulación** corrige un error (la factura nunca debió existir). Una **nota de crédito** reduce
una venta real después de facturar: el cliente devolvió un equipo, hubo un descuento posterior o se
facturó de más. La nota es un documento interno: no sustituye la nota de crédito electrónica
oficial, que se emite en la plataforma externa.

1. **Tablas** (esquema `finanzas`):
   - `NotasCredito` (clase `CreditNote`): `Id`, `Code` (`NCR-000001`, secuencia, único),
     `InvoiceId` (FK), `IssueDate date`, `Reason nvarchar(300)` obligatorio, `NetAmount`,
     `TaxAmount`, `TotalAmount`, `AppliedToBalance` (parte que rebaja la cuenta por cobrar),
     `RefundedAmount` (parte devuelta en dinero), `CashAccountId` y `RefundReference nvarchar(100)`
     (obligatorios solo si hay devolución), `ElectronicCreditNoteKey nvarchar(50)` opcional (clave
     de la nota oficial, igual criterio que en facturas), `Status` (1 Vigente, 2 Anulada), campos de
     anulación y auditoría, `RowVersion`. CHECK: `TotalAmount = AppliedToBalance + RefundedAmount`.
   - `NotaCreditoLineas` (clase `CreditNoteLine`): `Id`, `CreditNoteId`, `InvoiceLineId` (FK),
     `NetAmount`, `TaxRate`, `TaxAmount`, `TotalAmount`. El IVA se calcula con la tarifa conservada
     en la línea de la factura, para que una factura con varias tarifas se acredite correctamente.
2. **Registro (RF-90):** Finanzas y Gerencia, sobre una factura **confirmada** (de contado o a
   crédito). El usuario indica la fecha, el motivo y, por cada línea, el monto neto a acreditar. Lo
   acreditado acumulado por línea (notas vigentes) nunca supera el neto de esa línea.
3. **Reparto automático y devolución (RF-91):**
   - Factura a crédito: la nota rebaja primero el saldo pendiente de la cuenta por cobrar
     (`AppliedToBalance`). Si el total de la nota supera ese saldo porque el cliente ya pagó, la
     diferencia es una **devolución de dinero** en la misma operación (`RefundedAmount`), con cuenta
     de efectivo y referencia.
   - Factura de contado: todo el total de la nota es devolución.
   - **No existen saldos a favor del cliente:** lo que no rebaja el saldo se devuelve en ese mismo
     momento.
   - La devolución nunca supera el dinero efectivamente recibido por esa factura (total de contado o
     abonos vigentes) menos lo ya devuelto. Si la factura tiene retenciones vigentes, la nota solo
     puede rebajar el saldo pendiente, sin devolución.
   - Una devolución que supera el límite de autorización solo la registra Gerencia.
   - El estado de la cuenta por cobrar se recalcula con las reglas actuales.
4. **Asiento (agrega `SourceType` 7 Nota de crédito):** Débito Devoluciones y descuentos sobre ventas
   (neto) / Débito IVA por pagar (IVA) / Crédito Cuentas por cobrar (`AppliedToBalance`) / Crédito
   cuenta de efectivo (`RefundedAmount`). Se omiten las líneas en cero.
5. **Anulación:** solo Gerencia, con motivo; genera la reversión del asiento, restituye el saldo
   aplicado en la cuenta por cobrar y revierte la devolución. Una factura con notas de crédito
   vigentes no se anula hasta anular primero esas notas. Respeta la fecha de cierre en el registro y
   en la anulación.
6. **Pantallas:** en el detalle de la factura, "Registrar nota de crédito" y la lista de sus notas;
   listado propio de notas de crédito con filtros (código, cliente, factura, fecha, estado); detalle
   con "Ver asiento"; PDF interno de la nota con la leyenda "Nota de crédito interna. No sustituye la
   nota de crédito electrónica oficial." y "ANULADA" con el motivo cuando corresponda.
7. **Efecto en los reportes:** el estado de cuenta del cliente incluye las notas; el IVA del período
   resta el IVA de las notas vigentes por tarifa; el flujo de efectivo cuenta las devoluciones como
   salidas; el estado de resultados las refleja mediante la cuenta de devoluciones. El presupuesto
   no cambia: es solo de gastos.
8. **Bitácora:** registrar y anular una nota quedan en BitacoraOperaciones (nuevo tipo de documento).

Al terminar el bloque 8, ejecuta todas las pruebas, actualiza `docs/06-pruebas-evidencias.md` con la
ejecución completa y dame un resumen final con: tablas y columnas nuevas o renombradas, reportes
nuevos y cualquier decisión que haya quedado pendiente de validar.
