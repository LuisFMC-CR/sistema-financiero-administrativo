# CLAUDE.md — Sistema Financiero Administrativo (Soporte Experto)

Proyecto final de graduación (UIA). Sistema web interno para el control financiero-administrativo de
una empresa pequeña: 4 usuarios, una PC como servidor y red local cableada sin dominio. El código y
`docs/` deben coincidir con el documento académico, cuyo Capítulo V ya tiene el diseño completo
(arquitectura, base de datos, procesos y UML). Si el documento y este archivo difieren, manda este
archivo y avísame de la diferencia.

## Cómo trabajar conmigo

- Explica cada cambio antes de hacerlo y avanza paso a paso: quiero entender el código nuevo.
- Antes de eliminar, renombrar o mover código, revisa dependencias, explica el impacto y espera mi
  confirmación.
- No agregues paquetes NuGet sin preguntar ni uses versiones flotantes (`Directory.Packages.props`).
  Exportaciones: QuestPDF (licencia Community) para PDF y ClosedXML (MIT) para Excel.
- Cada funcionalidad incluye pruebas y actualiza `docs/02-requisitos-trazabilidad.md` y, si aplica,
  un ADR en `docs/decisiones/`.
- Interfaz, mensajes y documentación en español; identificadores de código en inglés.
- Producción: IIS en Windows 11 con SQL Server 2025 Express en el mismo equipo, HTTPS con certificado
  interno e IP fija; el puerto de SQL Server no se expone a la red.

## Comandos (PowerShell, desde la raíz)

```powershell
dotnet build SistemaFinanciero.sln
dotnet test SistemaFinanciero.sln
dotnet ef migrations add <Nombre> --project src\SistemaFinanciero.Infrastructure --startup-project src\SistemaFinanciero.Web
dotnet ef database update --project src\SistemaFinanciero.Infrastructure --startup-project src\SistemaFinanciero.Web -- --environment Development
dotnet run --project src\SistemaFinanciero.Web
```

Migraciones siempre explícitas (nunca `EnsureCreated` ni al arrancar). Pruebas contra SQL Server:
`$env:SISTEMA_FINANCIERO_RUN_SQL_TESTS = "1"`.

## Arquitectura (no cambiar sin ADR)

- Monolito modular: `Domain` ← `Application` ← `Infrastructure` ← `Web` (`docs/03-arquitectura.md`).
- **Domain:** entidades, estados e invariantes; toda regla de negocio vive aquí. No conoce EF Core,
  MVC ni Identity.
- **Application:** contratos (`I...Service`), comandos, modelos y políticas.
- **Infrastructure:** servicios con EF Core, transacciones, Identity y migraciones. Los servicios
  coordinan; no duplican reglas del dominio.
- **Web:** MVC con Razor, ViewModels y autorización por políticas en el servidor.
- Sin microservicios, CQRS, MediatR, AutoMapper ni repositorios genéricos; las pruebas de
  `tests/SistemaFinanciero.UnitTests/Architecture` deben seguir pasando.

## Convenciones y requerimientos no funcionales

- Dinero en `decimal` (nunca `float`, `double` ni `money`). Cantidades y precios unitarios con 4
  decimales; importes, impuestos, totales, saldos, abonos y pagos a 2 decimales
  (`MidpointRounding.AwayFromZero`).
- Fechas de negocio en `DateOnly`; instantes de auditoría en UTC (`DateTimeOffset`).
- Entidades mutables con `rowversion`: un conflicto se informa y nunca se sobrescribe.
- Catálogos con baja lógica; documentos financieros nunca se eliminan físicamente.
- Todo registro guarda usuario y fecha de creación y de última modificación. Confirmar, anular,
  ajustar y cambiar parámetros queda en la bitácora. Las operaciones de varios registros van en una
  sola transacción.
- Seguridad: contraseña de 12 caracteres o más; bloqueo de 15 min tras 5 intentos fallidos; sesión de
  30 min de inactividad con cookies HttpOnly, Secure y SameSite=Strict; HTTPS y CSRF; errores sin
  detalles técnicos.
- Desempeño: registros y consultas en 3 s o menos; reportes de hasta 12 meses en 10 s o menos;
  consultas paginadas. Chrome y Edge vigentes; sin Internet ni CDN (bibliotecas en `wwwroot/lib`).
- Interfaz en español con el menú de la Tabla 4; campos obligatorios marcados; confirmación antes de
  confirmar, anular o ajustar; montos con ₡ y 2 decimales; fechas dd/mm/aaaa.

## Alcance: fuera del sistema

Si una tarea requiere algo de esta lista, detente y pregúntame. Fuera del sistema:
- partida doble, libros, estados financieros y cierres contables formales (el único cierre es la
  fecha de cierre, regla 16);
- intereses, préstamos, aportes de socios y cuentas de pasivo o patrimonio;
- Hacienda (emisión de comprobantes, liquidación o declaración);
- impuestos distintos del IVA y retenciones a terceros;
- dólares u otras monedas (el sistema opera **solo en colones**) y planilla;
- saldos, transferencias, integración y conciliación bancarias;
- inventario, nube, app móvil, notificaciones entre usuarios, reportes personalizables y respaldos
  automáticos como función del sistema.

## Módulos (Tabla 4 del documento; nombres del menú)

Gestionar Cuentas Contables · Cuentas por Cobrar · Cuentas por Pagar · Ingresos · Gastos ·
Facturación · Presupuesto · Impuestos y Retenciones · Mantenimiento · Consultas · Reportes · Seguridad.

## Reglas de negocio aprobadas

1. **Cuentas contables.** Catálogo jerárquico solo de tipo Activo (solo efectivo: caja y bancos),
   Ingreso y Gasto. Código generado, nombre, tipo, cuenta superior del mismo tipo (sin ciclos) y
   estado. Las cuentas de efectivo reemplazan a `FinancialAccount`. Cada categoría apunta a una cuenta
   de su mismo tipo. Es solo clasificación, no partida doble.
2. **Flujo del dinero.**
   - Factura de contado → ingreso. Factura a crédito → cuenta por cobrar; cada abono rebaja el saldo
     y genera su ingreso. Gastos igual, con cuentas por pagar y pagos. Un gasto es solo una compra a
     un proveedor respaldada por comprobante electrónico.
   - Ingresos manuales: solo otras entradas de la operación (reintegros, intereses que acredite el
     banco); nunca préstamos ni aportes.
   - Todo ingreso indica categoría y cuenta de efectivo de destino; todo pago, la de origen.
   - Abono o pago a varias facturas: se eligen cliente o proveedor, monto, cuenta de efectivo y
     referencia; el sistema propone el reparto de la más antigua a la más nueva (editable; en abonos,
     cada línea admite retención); crea un abono o pago por cuenta en una sola transacción, todos con
     la misma referencia y un `GroupId` común; el límite se compara con el **total**; cada uno se anula
     de forma individual.
3. **IVA y retenciones.**
   - Solo IVA, como porcentaje. Tarifas precargadas: 13 % y Exento; Finanzas puede agregar o
     actualizar tarifas.
   - Precios sin IVA. **Una tarifa por línea**; el IVA se calcula sobre el neto (bruto − descuento) y
     la línea guarda tarifa, base y monto. El gasto se registra sin IVA y el sistema calcula IVA y
     total. Proveedor obligatorio en todo gasto.
   - Retención: solo la que aplica un cliente obligado por ley al pagar (p. ej., 2 % de renta sobre el
     neto). Va en el abono con tipo, porcentaje, base (por defecto el neto de la factura, editable) y
     monto, y rebaja el saldo. Tipos no precargados. Los pagos a proveedores no llevan retención.
   - Cambios posteriores del catálogo no alteran documentos confirmados.
4. **Estados y correcciones.**
   - Borrador (editable) → Confirmado (inmutable) → Anulado (motivo, usuario y fecha). Los borradores
     no se anulan. Solo Gerencia anula, con motivo, y la anulación no se revierte.
   - No se anula un documento con abonos o pagos vigentes sin anular antes esos movimientos.
   - Anular arrastra lo generado: factura de contado → ingreso; gasto de contado → pago; documento a
     crédito → su cuenta por cobrar o por pagar con sus ajustes; abono → ingreso y retención. Abonos y
     pagos restituyen el saldo. Lo generado automáticamente solo se anula desde su origen.
   - La factura anulada conserva su número. Lo anulado sale de totales, reportes y presupuesto, pero
     sigue visible en consultas e historial.
   - Ajuste de saldo (solo Gerencia), aumento o disminución con motivo; invariante 0 ≤ saldo ≤ monto
     original; no genera ingresos ni pagos ni toca efectivo, documento o presupuesto; un error se
     corrige con otro ajuste en sentido contrario.
5. **Moneda.** Solo colones; sin tipo de cambio ni conversiones.
6. **Presupuesto.** Mensual, por categoría de gasto de último nivel (las superiores solo suman).
   Ejecutado = total con IVA de los gastos confirmados del mes (sin borradores ni anulados; los pagos
   no lo cambian). Al confirmar un gasto que supera lo disponible se muestra una alerta antes de
   guardar, sin bloquear; si no hay presupuesto, se indica y se permite continuar. Monto 0 válido.
   Cada cambio guarda monto anterior, nuevo, usuario y fecha. Disponible = presupuestado − ejecutado
   (mínimo 0); variación = ejecutado − presupuestado; variación % = variación / presupuestado.
7. **Límite de autorización.** Parámetro. Abonos y pagos sobre el límite solo los registra Gerencia;
   los cambios solo afectan operaciones posteriores. Un gasto de contado sobre el límite no se
   confirma de contado: va a crédito y Gerencia registra el pago. Al confirmar un gasto de contado se
   indican cuenta de efectivo y referencia; el medio de pago es el del gasto.
8. **Reportes** (PDF y Excel): ingresos por período; gastos por período y categoría; presupuesto
   contra lo ejecutado; estado de cuenta de cliente; cuentas por pagar por proveedor; comparativo
   mensual de ingresos y gastos. Cada reporte muestra nombre, parámetros, fecha y hora y usuario. En
   Excel los montos van como números. Sin datos: mensaje y no se genera archivo.
9. **Cuentas vencidas.** Indicador en inicio y en Consultas para Finanzas y Gerencia, con cantidad y
   monto. Vencida: vencimiento anterior a hoy y saldo > 0. Próxima: vence en los próximos N días
   (parámetro, 7 por defecto).
10. **Numeración y referencias.** Consecutivo interno de facturas, no fiscal. Al confirmar una
    factura de contado se indican cuenta de efectivo, medio de pago y referencia. Solo se exportan a
    PDF facturas confirmadas o anuladas. Advertir, sin bloquear, si un ingreso, abono o pago repite la
    referencia en la misma cuenta de efectivo.
11. **Consultas y filtros.** Pantalla de Consultas con filtros combinados (fecha, estado, cliente,
    proveedor, categoría, cuenta contable), detalle con registros relacionados e historial. Además,
    cada listado tiene filtros sobre su propia tabla, al menos los de su RF: cuentas contables (RF-06),
    cuentas por cobrar y por pagar (RF-11, RF-17), ingresos (RF-23), gastos (RF-30), facturas (RF-36),
    IVA y retenciones (RF-49). Los filtros de los listados no reemplazan la pantalla de Consultas.
12. **Códigos automáticos**, asignados al crear y no editables: `CLI-0001`, `PRV-0001`, `PRD-0001`,
    `SRV-0001`, `ING-001`, `GAS-001`, `IMP-01`, `RET-01`. Cuentas contables: código jerárquico
    (`3.1.07`) = siguiente hijo libre de la superior; después solo cambian nombre y referencia
    bancaria. Generación segura ante concurrencia (secuencia o índice único con reintento).
13. **Datos iniciales (RF-54)**, cargados por migración (nunca al arrancar): catálogo base, una
    categoría por cuenta de detalle (mismo nombre), IVA 13 % y Exento, plazos de pago y parámetros
    por defecto; sin tipos de retención. Las cuentas de grupo están protegidas; las de detalle se
    pueden renombrar o inactivar, y se agregan nuevas dentro de los grupos. Catálogo base:
    - 1 Activo › 1.1 Efectivo › 1.1.01 Caja chica, 1.1.02 Cuenta bancaria principal.
    - 2 Ingresos › 2.1 Ingresos por servicios › 2.1.01 Soporte técnico, 2.1.02 Mantenimiento y
      reparación de equipos, 2.1.03 Instalación de infraestructura informática; 2.2 Ingresos por
      ventas › 2.2.01 Venta de equipos y repuestos; 2.3 Otros ingresos › 2.3.01 Otros ingresos de la
      operación.
    - 3 Gastos › 3.1 Gastos de operación › 3.1.01 Alquiler, 3.1.02 Electricidad y agua, 3.1.03
      Internet y telefonía, 3.1.04 Materiales y repuestos, 3.1.05 Software y licencias, 3.1.06
      Transporte y combustible; 3.2 Gastos administrativos › 3.2.01 Papelería y suministros de
      oficina, 3.2.02 Servicios profesionales, 3.2.03 Comisiones bancarias, 3.2.04 Otros gastos
      administrativos.
14. **Plazos de pago.** Lista fija sin mantenimiento: Contado, Crédito 30, 60 y 90 días, para
    facturas y gastos. Vencimiento = fecha del documento + días (no se digita).
15. **Factura interna (pantalla y PDF).** Encabezado con datos de la empresa (parámetros, con sus
    valores vigentes), número, fecha, plazo, vencimiento y estado; datos del cliente; detalle por línea
    (código, descripción, cantidad, unidad, precio, subtotal, descuento, neto, tarifa e importe de IVA,
    total); resumen con IVA desglosado por tarifa; pie con medio de pago (contado) y la leyenda
    "Documento administrativo interno. No sustituye el comprobante electrónico oficial." Las anuladas
    muestran "ANULADA" y el motivo.
16. **Fecha de cierre.** Parámetro exclusivo de Gerencia. No se registra, confirma, anula ni ajusta
    nada con fecha igual o anterior (validar en el servidor). Adelantarla es el cierre mensual;
    retrocederla es una reapertura con motivo y queda en bitácora (valor anterior y nuevo). Anular en
    un mes cerrado exige reabrirlo. No puede ser posterior a hoy.

Trazabilidad: usa los códigos RF-01 a RF-77 (RF-15 = pago a varias cuentas; RF-54 = datos iniciales)
y CU-01 a CU-22 en `docs/` y en las pruebas; los flujos alternos de cada CU definen sus validaciones.

## Modelo de datos aprobado

Columna por columna en `docs/modelo/diseno_fisico_bd_workbench.sql` (tipos de MySQL, solo para el
diagrama). La base real se crea con migraciones de EF Core en SQL Server, con los mismos nombres de
tablas y columnas. Si el código difiere, explícame la diferencia antes de migrar.

- **Esquemas:** `seguridad` (Usuarios, Roles, UsuariosRoles, EventosSeguridad, BitacoraOperaciones);
  `catalogos` (Clientes, Proveedores, ProductosServicios, CategoriasFinancieras, TarifasIva,
  TiposRetencion, PlazosPago, Parametros); `finanzas` (CuentasContables, Facturas, FacturaLineas,
  CuentasPorCobrar, Abonos, Retenciones, Ingresos, Gastos, CuentasPorPagar, Pagos, AjustesSaldo,
  Presupuestos, PresupuestoCambios).
- **Tipos:** `CHAR(36)` → `uniqueidentifier`; `VARCHAR(n)` → `nvarchar(n)`; `TEXT` → `nvarchar(max)`;
  `DATETIME` → `datetimeoffset` (UTC); `BIT` → `bit`; `BINARY(8)` → `rowversion`. Importes
  `decimal(18,2)`, cantidades y precios `decimal(18,4)`, porcentajes `decimal(5,2)`, fechas `date`.
  PlazosPago y Parametros usan llave `int`.
- **Enumeraciones (int con CHECK):** Facturas.Status 1 Borrador, 2 Confirmada, 3 Anulada; Gastos.Status
  1 Borrador, 2 Confirmado, 3 Anulado; CuentasPorCobrar/Pagar.Status 1 Pendiente, 2 Parcialmente
  pagada, 3 Pagada, 4 Anulada; Abonos, Pagos e Ingresos.Status 1 Vigente, 2 Anulado; Ingresos.Origin
  1 Factura de contado, 2 Abono, 3 Registro manual; PaymentMethod 1 Efectivo, 2 Transferencia
  bancaria, 3 SINPE Móvil, 4 Tarjeta, 5 Cheque; CuentasContables.Type 1 Activo, 2 Ingreso, 3 Gasto;
  CategoriasFinancieras.Kind 1 Ingreso, 2 Gasto; ProductosServicios.Type 1 Producto, 2 Servicio;
  AjustesSaldo.Direction 1 Disminución, 2 Aumento.
- **Únicos:** Facturas.Number (nulo en borradores), CuentasPorCobrar.InvoiceId,
  CuentasPorPagar.ExpenseId, Retenciones.ReceivablePaymentId, Ingresos.ReceivablePaymentId,
  UsuariosRoles.UserId, (InvoiceId, LineNumber), (ExpenseCategoryId, PeriodYear, PeriodMonth), Code
  de cada catálogo e Identification (obligatoria) de clientes y proveedores.
- **Otras restricciones:** CHECK `0 <= Balance <= OriginalAmount`, con el saldo recalculado en la
  misma transacción de cada movimiento; AjustesSaldo con exactamente una de ReceivableId o PayableId;
  Ingresos.Origin coherente con InvoiceId y ReceivablePaymentId; Pagos.PayableId siempre fijo (un
  gasto de contado no genera ningún pago: guarda su cuenta de efectivo y su referencia en el propio
  Gasto, igual que dice la regla 7; corregido respecto a una versión anterior de esta línea, ADR-0028).
- **Auditoría:** CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId y RowVersion en
  catálogos y documentos, con FK reales a Usuarios (el diagrama no las dibuja). En catálogos con
  datos iniciales, los usuarios de auditoría admiten nulo para las filas de la migración.
- **Secuencias SQL Server:** número de factura (se pide después de validar la confirmación) y
  códigos CLI, PRV, PRD, SRV, ING, GAS, IMP y RET.
- **Clases (diagramas UML):** ApplicationUser, Role, SecurityAuditEvent, OperationLogEntry
  (BitacoraOperaciones), SystemSettings (Parametros), Customer, Supplier, CatalogItem,
  AccountingAccount, FinancialCategory, VatRate, WithholdingType, PaymentTerm, Invoice, InvoiceLine,
  Receivable, ReceivablePayment (Abonos), Withholding, Income, Expense, Payable, SupplierPayment
  (Pagos), BalanceAdjustment, Budget, BudgetChange.
- **Operaciones publicadas:** Invoice.AddLine, RemoveLine, Confirm(closingDate), AssignNumber,
  Cancel; InvoiceLine.Recalculate; Receivable y Payable.ApplyPayment, RevertPayment, Adjust, Cancel;
  Expense.Recalculate, Confirm(closingDate, authorizationLimit), Cancel; ReceivablePayment,
  SupplierPayment e Income.Cancel; Budget.ChangeAmount; SystemSettings.IsOpen(date),
  ChangeClosingDate(newDate, today, reason), UpdateLimits, UpdateCompany;
  PaymentTerm.CalculateDueDate; catálogos con Update, Activate y Deactivate (VatRate y
  WithholdingType con ChangeRate; AccountingAccount con Rename). Servicios: InvoiceService.ConfirmAsync,
  ReceivablePaymentService.RegisterAsync, ExpenseService.CheckBudgetAsync y ConfirmAsync.

## Perfiles (roles fijos; sin permisos por usuario)

| Perfil | Permisos |
|---|---|
| Asistente | Consulta catálogos; mantiene clientes y proveedores; registra borradores de facturas y gastos y ve solo los suyos. No ve reportes, presupuesto ni saldos. |
| Finanzas | Catálogos financieros (cuentas, categorías, tarifas, retenciones, productos); confirma documentos; ingresos manuales; abonos y pagos hasta el límite; presupuesto; consultas y reportes. |
| Gerencia | Todo lo de Finanzas y Asistente (ve los borradores de todos), más lo exclusivo: abonos y pagos sobre el límite, anulaciones, ajustes de saldo y parámetros (límite, días de alerta, datos de la empresa, fecha de cierre). No administra usuarios. |
| Administrador | Solo usuarios y configuración técnica; sin acceso a información financiera. |

- Gerencia entra en toda política que permita a Finanzas o Asistente; solo las de administración de
  usuarios la excluyen. Lo exclusivo de Gerencia sigue siendo exclusivo.
- Permisos validados siempre en el servidor, además de ocultar opciones; pantalla de solo consulta con
  esta matriz.
- El documento aún no refleja los permisos ampliados de Gerencia (corrección pendiente en CU-04, 05,
  07, 08, 09, 10, 13, 14, 15 y 19, Tabla 10 y análisis de módulos). Si comparas, manda este archivo.

## Estado y orden de trabajo

Última revisión del repositorio: 26/09/2026. Existían seguridad y usuarios, clientes, proveedores,
productos y servicios, categorías, cajas y bancos (`FinancialAccount`), tipo de cambio y el dominio y
persistencia de facturas (sin servicios, pantallas ni pruebas). Revisa primero el estado real y dime
qué difiere antes de cambiar nada.

Ajustes pendientes al código existente (explica el impacto y pide confirmación antes de borrar):
- Retirar la moneda: `ExchangeRate`, `CrcPerUsd`, `TiposCambioDiarios`, `ReferencePriceUsd`, la
  moneda de cajas y bancos y la de la factura.
- IVA: quitar `TaxCalculationType.FixedAmountPerUnit` y `FacturaLineaImpuestos`; la línea guarda
  `VatRateId`, `VatRate` y `VatAmount`, con el neto como base.
- Facturas: plazo de pago y vencimiento; edición de líneas y encabezado en borrador; número al
  **confirmar** con secuencia (hoy `Number` es identity al insertar); importes a 2 decimales; corregir
  RF-INV-006 (Asistente no registra cobros).
- Catálogos: `Identification` obligatoria y única en clientes y proveedores; `EconomicActivity`
  obligatoria en proveedores; `Code` generado (no se pide en formularios); en productos,
  `ReferencePriceCrc` pasa a `ReferencePrice` y se agrega `DefaultVatRateId` (editable en la línea).
- `CuentasFinancieras` pasa a `CuentasContables`, con migración de datos.

Orden de construcción:
1. Catálogos faltantes: cuentas contables, tarifas de IVA, tipos de retención, plazos de pago,
   parámetros, relación categoría–cuenta y datos iniciales.
2. Facturación, cuentas por cobrar, abonos (incluido a varias facturas) e ingresos.
3. Gastos, cuentas por pagar y pagos (incluido a varias facturas).
4. Presupuesto y alerta.
5. Consultas, indicador de vencidas y reportes con exportación.
6. Anulaciones y ajustes, y validación de la fecha de cierre en todas las operaciones.
7. Pantalla de perfiles y permisos.

Prioriza flujos completos y demostrables (factura → cuenta por cobrar → abono → ingreso → reporte;
gasto → cuenta por pagar → pago → presupuesto) sobre pantallas secundarias.

## Lo que el documento necesita del prototipo

- **Entradas (5 pantallas o más):** factura en borrador con líneas; abono a una o varias facturas con
  retención; gasto con la alerta de presupuesto; presupuesto mensual; parámetros con fecha de cierre.
  Completas, con datos realistas en colones y validaciones junto al campo.
- **Salidas (5 o más):** factura interna en PDF; reportes de ingresos, gastos, presupuesto contra lo
  ejecutado, estado de cuenta y cuentas por pagar por proveedor, en pantalla, PDF y Excel.
- **Programación:** fragmentos cortos y legibles de una entrada (ViewModel con validaciones), una
  salida (PDF o Excel), un proceso (confirmación de factura en una transacción) y una validación de
  dominio (saldo, fecha de cierre o límite).
- **Pruebas (5 o más, automatizadas, esperado contra obtenido):** abono que supera el saldo; operación
  en período cerrado; abono o pago sobre el límite sin perfil de Gerencia; factura de contado que
  genera ingreso y a crédito que genera cuenta por cobrar; gasto que supera el presupuesto y muestra
  la alerta sin bloquear (casos citados en la conclusión 4).

Los nombres de tablas, columnas, clases, operaciones y servicios deben coincidir con este archivo.
