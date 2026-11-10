# CLAUDE.md — Sistema Financiero Contable (Soporte Experto)

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
`$env:SISTEMA_FINANCIERO_RUN_SQL_TESTS = "1"`. La herramienta `dotnet-ef` está fijada en
`.config/dotnet-tools.json` (`dotnet tool restore`). Si la aplicación está abierta, bloquea la
compilación del proyecto Web: ciérrala antes de compilar o migrar.

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
- balance general, asientos manuales, saldos iniciales y cierres contables formales (el único cierre
  sigue siendo la fecha de cierre, regla 16); la partida doble ya no está fuera: ver regla 1 y el
  bloque de asientos automáticos del paquete de mejoras finales;
- intereses, préstamos y aportes de socios (el sistema no administra deudas ni capital de los socios);
- Hacienda: emisión, firma o transmisión de comprobantes electrónicos o declaraciones tributarias (la
  factura solo guarda datos preparados para el traslado manual a la plataforma externa, ver el bloque
  de factura 4.4 del paquete de mejoras finales); la **liquidación interna del IVA** ya no está fuera:
  es un registro interno del pago o del saldo a favor, ver la regla 3 (IVA) y el bloque de liquidación
  del IVA del paquete v7;
- impuestos distintos del IVA y retenciones a terceros; la **estimación del impuesto sobre la renta**
  y el registro de sus pagos a Hacienda ya no están fuera (regla 17), pero siguen fuera el cálculo
  fiscal oficial (ajustes de renta gravable, depreciación, gastos no deducibles), la declaración y la
  provisión contable del impuesto;
- dólares u otras monedas (el sistema opera **solo en colones**) y planilla;
- saldos iniciales, integración y conciliación bancarias (los traspasos entre cuentas de efectivo
  propias ya no están fuera: ver la regla 2 y el bloque de traspasos del paquete v7);
- inventario, nube, app móvil, notificaciones entre usuarios, reportes personalizables y respaldos
  automáticos como función del sistema.

## Módulos (Tabla 4 del documento; nombres del menú)

Gestionar Cuentas Contables · Cuentas por Cobrar · Cuentas por Pagar · Ingresos · Gastos ·
Facturación · Presupuesto · Impuestos y Retenciones · Mantenimiento · Consultas · Reportes · Seguridad.

## Reglas de negocio aprobadas

1. **Cuentas contables.** Catálogo jerárquico con cinco tipos: 1 Activo (solo efectivo: caja y
   bancos), 2 Pasivo, 3 Patrimonio, 4 Ingreso y 5 Gasto. Código generado, nombre, tipo, cuenta
   superior del mismo tipo (sin ciclos) y estado. Las cuentas de efectivo reemplazan a
   `FinancialAccount`. Cada categoría financiera apunta a una cuenta de Ingreso o Gasto de su mismo
   tipo. Las cuentas de Pasivo y Patrimonio son únicamente **cuentas de sistema** (columna
   `SystemRole`, ver el bloque de catálogo contable ampliado del paquete de mejoras finales): las
   reglas de contabilización las buscan por su rol, nunca por código o nombre; no tienen categoría
   financiera ni se eligen en ningún formulario de ingresos o gastos; se pueden renombrar y
   desactivar solo si no tienen movimientos en el libro mayor. El botón de desactivación siempre se
   muestra; si hay movimientos, se rechaza la acción con un mensaje claro. Si una cuenta de sistema
   está inactiva, las operaciones que requieran su rol no se contabilizan hasta reactivarla. Estas
   cuentas no se pueden mover. El usuario solo crea cuentas de detalle nuevas, siempre de **tercer nivel**,
   dentro de los grupos de segundo nivel 1.1, 4.1, 4.2, 4.3, 5.1, 5.2 y 5.3 (ver el bloque de regla de
   creación de cuentas contables del paquete v7; una cuenta nueva bajo 1.1 siempre es de efectivo). El
   catálogo en sí sigue siendo solo clasificación; la partida doble vive en los asientos automáticos
   (bloque de asientos contables del paquete de mejoras finales), no en el catálogo.
2. **Flujo del dinero.**
   - Factura de contado → ingreso. Factura a crédito → cuenta por cobrar; cada abono rebaja el saldo
     y genera su ingreso. Gastos igual, con cuentas por pagar y pagos. Un gasto es solo una compra a
     un proveedor respaldada por comprobante electrónico.
   - Ingresos manuales: solo otras entradas de la operación (reintegros, intereses que acredite el
     banco); nunca préstamos ni aportes.
   - Todo ingreso indica categoría y cuenta de efectivo de destino; todo pago, la de origen.
   - Un abono iniciado desde el detalle de una cuenta por cobrar se aplica únicamente a esa cuenta y
     toma de ella el cliente. El reparto de un cobro entre varias cuentas por cobrar se inicia como
     una opción separada en Ingresos: se elige el cliente, monto, cuenta de efectivo y referencia; el
     sistema propone el reparto de la más antigua a la más nueva (editable, con retención por línea),
     crea un abono por cuenta en una transacción con la misma referencia y `GroupId`, compara el límite
     con el **total** y permite anular cada abono individualmente. Los pagos a varias cuentas por pagar
     conservan su propio flujo desde Cuentas por Pagar.
   - El dinero que pasa de una cuenta de efectivo a otra es un **traspaso** (bloque de traspasos del
     paquete v7), nunca un ingreso ni un gasto: no toca categorías, presupuesto ni estado de
     resultados. El pago del IVA a Hacienda es una **liquidación** (bloque de liquidación del IVA del
     paquete v7), nunca un gasto. El pago del impuesto sobre la renta a Hacienda es un **pago de
     renta** (regla 17): anticipo del impuesto, nunca un gasto.
3. **IVA y retenciones.**
   - Tarifas precargadas: IVA 13 %, IVA 2 %, Exento y No sujeto; Gerencia puede agregar o actualizar
     otras tarifas (bloque de IVA y retenciones del paquete v7). Cada cliente tiene una **condición de
     IVA** (Normal, institución estatal de educación superior o no sujeto) que determina
     automáticamente la tarifa de las líneas de sus facturas; la factura nunca ofrece una tarifa que no
     corresponda a la condición del cliente.
   - Precios sin IVA. **Una tarifa por línea**; el IVA se calcula sobre el neto (bruto − descuento) y
     la línea guarda tarifa, base y monto. El gasto se registra sin IVA y el sistema calcula IVA y
     total. Proveedor obligatorio en todo gasto. En los gastos, el IVA puede marcarse como **no
     acreditable** y entonces forma parte del costo en vez de acreditarse (bloque de IVA y retenciones
     del paquete v7).
   - El IVA se liquida mes a mes contra Hacienda (bloque de liquidación del IVA del paquete v7); ese
     registro no sustituye la declaración oficial.
   - Fuera del IVA: tasa cero, exoneraciones con documento, prorrata y retenciones de IVA por pagos con
     tarjeta.
   - Retención: la única que aplica es la del **2 % del sector público** (Ley del Impuesto sobre la
     Renta, art. 23, inciso g), sobre el monto sin IVA, solo en operaciones que superan la base mínima
     (un salario base) y solo para clientes marcados como sector público; si no corresponde, el
     sistema no ofrece la retención (bloque de IVA y retenciones del paquete v7). Va en el abono con
     tipo, porcentaje, base (propuesta por el sistema, editable dentro del límite que corresponde) y
     monto, y rebaja el saldo. Los pagos a proveedores no llevan retención. Fuera: retenciones por
     tarjeta, remesas al exterior, salarios, dividendos y dietas.
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
   - La factura confirmada se anula desde el flujo de nota de crédito, no con una acción directa. La
     nota de crédito interna no es un ajuste de saldo: documenta la corrección de una venta real
     después de facturar, conserva las líneas originales, liquida el saldo por cobrar y devuelve en
     efectivo lo cobrado. Las retenciones aplicadas se revierten en su cuenta contable, no se entregan
     en efectivo. No existen saldos a favor del cliente ni anulación de una nota ya aplicada.
5. **Moneda.** Solo colones; sin tipo de cambio ni conversiones.
6. **Presupuesto.** Mensual, por categoría de gasto de último nivel (las superiores solo suman).
   Solo se puede crear o modificar para el mes actual o uno futuro; los meses anteriores quedan
   disponibles únicamente para consulta. La fecha y hora del sistema se obtienen del reloj común
   `TimeProvider` (`TimeProvider.System` en producción).
   Ejecutado = **costo** de los gastos confirmados del mes (sin borradores ni anulados; los pagos no lo
   cambian): el monto sin IVA, más el IVA solo cuando ese gasto lo marca como no acreditable (bloque de
   presupuesto y reportes del paquete v7; el IVA acreditable va a IVA acreditable, no al presupuesto).
   Al confirmar un gasto que supera lo disponible se muestra una alerta antes de guardar, sin
   bloquear; si no hay presupuesto, se indica y se permite continuar. Monto 0 válido. Cada cambio
   guarda monto anterior, nuevo, usuario y fecha. Disponible = presupuestado − ejecutado (mínimo 0);
   variación = ejecutado − presupuestado; variación % = variación / presupuestado.
7. **Límite de autorización.** Parámetro. Abonos (individuales o repartidos; en el reparto se compara
   el total), pagos, traspasos y pagos de renta sobre el límite solo los registra Gerencia; los cambios solo afectan operaciones posteriores. Un gasto de contado sobre el límite no
   se confirma de contado: va a crédito y Gerencia registra el pago. Al confirmar un gasto de contado
   se indican cuenta de efectivo y referencia; el medio de pago es el del gasto.
8. **Reportes** (PDF y Excel): ingresos recibidos por período; gastos por período y categoría;
   presupuesto contra lo ejecutado; presupuesto anual a la fecha; estado de cuenta de cliente; cuentas
   por pagar por proveedor; comparativo mensual de ingresos y gastos (desde los asientos); flujo de
   efectivo mensual (desde los asientos); y los contables y tributarios del paquete de mejoras
   finales: libro diario, libro mayor, balanza de comprobación, estado de resultados, IVA del período
   y retenciones aplicadas por clientes. El paquete v7 agrega cuentas por cobrar por cliente con
   antigüedad de saldos y antigüedad en el reporte de cuentas por pagar por proveedor. RF-99 agrega la
   estimación del impuesto sobre la renta por período fiscal (regla 17). Cada reporte
   muestra nombre, parámetros, fecha y hora y usuario. En Excel los montos van como números. Sin
   datos: mensaje y no se genera archivo.
9. **Cuentas vencidas.** Indicador en inicio y en Consultas para Finanzas y Gerencia, con cantidad y
   monto. Vencida: vencimiento anterior a hoy y saldo > 0. Próxima: vence en los próximos N días
   (parámetro, 7 por defecto).
10. **Numeración y referencias.** Consecutivo interno de facturas, no fiscal. Al confirmar una
    factura de contado se indican cuenta de efectivo, medio de pago y referencia. Solo se exportan a
    PDF facturas confirmadas o anuladas. Advertir, sin bloquear, si un ingreso, abono, pago, traspaso o
    pago de renta repite la referencia en la misma cuenta de efectivo.
11. **Consultas y filtros.** Pantalla de Consultas con filtros combinados (fecha, estado, cliente,
    proveedor, categoría, cuenta contable), detalle con registros relacionados e historial. Además,
    cada listado tiene filtros sobre su propia tabla, al menos los de su RF: cuentas contables (RF-06),
    cuentas por cobrar y por pagar (RF-11, RF-17), ingresos (RF-23), gastos (RF-30), facturas (RF-36),
    IVA y retenciones (RF-49). Los filtros de los listados no reemplazan la pantalla de Consultas.
12. **Códigos automáticos**, asignados al crear y no editables: `CLI-0001`, `PRV-0001`, `PRD-0001`,
    `SRV-0001`, `REC-000001`, `GTO-000001`, `ABO-000001`, `PAG-000001` (y los nuevos del paquete v7:
    `TRA-000001` traspaso, `LIQ-000001` liquidación de IVA y `NCR-000001` nota de crédito; y
    `PIR-000001` pago de impuesto sobre la renta, RF-99). Cuentas
    contables: código jerárquico (`3.1.07`) = siguiente hijo libre de la superior; después solo
    cambian nombre y referencia bancaria. Generación segura ante concurrencia (secuencia o índice
    único con reintento).
13. **Datos iniciales (RF-54)**, cargados por migración (nunca al arrancar): catálogo base, una
    categoría por cuenta de detalle (mismo nombre), tarifas de IVA 13 %, IVA 2 %, Exento y No sujeto,
    tipo de retención "Retención 2 % sector público" precargado, plazos de pago y parámetros por
    defecto (incluida la escala del impuesto sobre la renta de 2026 y la cuenta de sistema 1.3.03,
    regla 17). Las cuentas de grupo están protegidas; las de detalle se pueden renombrar o inactivar, y
    se agregan nuevas dentro de los grupos. Catálogo base:
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
16. **Cierres mensuales y período fiscal.** Todo mes anterior al mes actual (según la fecha local
    obtenida del `TimeProvider`) se cierra automáticamente: no se registra, confirma, anula ni ajusta
    en él. Gerencia puede reabrirlo con motivo; la reapertura permanece hasta que Gerencia vuelva a
    cerrarlo. Cada cambio de estado queda en una bitácora inmutable. El período fiscal, exclusivo de
    Gerencia, inicia el primer día de un mes y termina el último día de doce meses después; cambiarlo
    admite motivo opcional y conserva historial. Los reportes anuales usan ese rango. El período
    inicial acordado es 01/01/2026–31/12/2026. Ninguna operación se registra con fecha fuera del
    período fiscal vigente.
17. **Impuesto sobre la renta (RF-99).** Es una **estimación**, no un cálculo oficial; los textos de
    pantalla y reportes la llaman así y no mencionan declaraciones oficiales.
    - Escala única vigente en `ParametrosSistema` (Gerencia la cambia; cada cambio queda en
      `EscalaRentaHistorial`; al cambiarla, toda estimación se recalcula, incluso la de períodos
      anteriores). Valores 2026: hasta ₡5.621.000 al 5 %; sobre el exceso hasta ₡8.433.000 al 10 %;
      sobre el exceso hasta ₡11.243.000 al 15 %; sobre el exceso de ₡11.243.000 al 20 %. Si la renta
      bruta del período **supera** ₡119.174.000 no aplican los tramos y se usa la tarifa general del
      30 % sobre toda la renta neta. No hay tramo exento (escala de persona jurídica); si se requiere,
      Gerencia pone 0 % en el primer tramo.
    - Renta bruta = total de ingresos y renta neta = utilidad del estado de resultados del período
      fiscal (ya sin IVA; el IVA no acreditable cuenta como gasto). Con pérdida, el impuesto es 0.
      Cada tramo se redondea a 2 decimales.
    - Saldo estimado = impuesto − retenciones del 2 % (movimientos netos de la cuenta 1.3.02 en el
      período) − pagos de renta vigentes del período; positivo por pagar, negativo a favor.
    - Pago de renta: período fiscal al que se aplica (explícito, porque la declaración se paga cuando
      el período ya terminó), cuenta de efectivo, medio de pago, monto, referencia y fecha (no anterior
      al inicio de ese período). Asiento: débito 1.3.03 "Impuesto sobre la renta pagado por
      anticipado", crédito efectivo. Vigente → Anulado solo por Gerencia, con reversión del asiento. No
      toca categorías, presupuesto ni estado de resultados.

Trazabilidad: usa los códigos RF-01 a RF-99 (RF-15 = pago a varias cuentas; RF-54 = datos iniciales;
RF-78 a RF-92 del paquete de mejoras finales; RF-93 a RF-98 del paquete v7: condición de IVA del
cliente y tarifa automática por línea, IVA no acreditable en gastos, retención del 2 % del sector
público, traspaso entre cuentas de efectivo, liquidación mensual del IVA, cuentas por cobrar por
cliente con antigüedad de saldos; RF-99 estimación del impuesto sobre la renta y pagos a Hacienda) y
CU-01 a CU-22 en `docs/` y en las pruebas; los flujos alternos de cada CU definen sus validaciones.

## Modelo de datos aprobado

Columna por columna en `docs/modelo/diseno_fisico_bd_workbench.sql` (tipos de MySQL, solo para el
diagrama). La base real se crea con migraciones de EF Core en SQL Server, con los mismos nombres de
tablas y columnas. Si el código difiere, explícame la diferencia antes de migrar.

- **Esquemas:** `seguridad` (Usuarios, Roles, UsuariosRoles, EventosSeguridad, BitacoraOperaciones);
  `catalogos` (Clientes, Proveedores, ProductosServicios, CategoriasFinancieras, TarifasIva,
  TiposRetencion, PlazosPago, Parametros); `finanzas` (CuentasContables, Facturas, FacturaLineas,
  CuentasPorCobrar, Abonos, Retenciones, Ingresos, Gastos, CuentasPorPagar, Pagos, AjustesSaldo,
  Presupuestos, PresupuestoCambios, Asientos, AsientoLineas, y del paquete v7: Traspasos,
  LiquidacionesIva, NotasCredito, NotaCreditoLineas, PeriodosContablesReabiertos,
  PeriodosContablesHistorial, PeriodoFiscalHistorial; y de RF-99: PagosImpuestoRenta,
  EscalaRentaHistorial). `ParametrosSistema` conserva las columnas históricas `ClosingDate` y su
  tabla `ClosingDateChanges`, pero el cierre automático ya no las usa; agrega `FiscalPeriodStartDate`
  y `FiscalPeriodEndDate`, y la escala de renta (`IncomeTaxBracket1UpperLimit` a
  `IncomeTaxBracket3UpperLimit`, `IncomeTaxBracket1Rate` a `IncomeTaxBracket4Rate`,
  `IncomeTaxGrossIncomeThreshold` e `IncomeTaxGeneralRate`). Nota: en la base real
  `CuentasContables` vive en el esquema `catalogos`.
- **Tipos:** `CHAR(36)` → `uniqueidentifier`; `VARCHAR(n)` → `nvarchar(n)`; `TEXT` → `nvarchar(max)`;
  `DATETIME` → `datetimeoffset` (UTC); `BIT` → `bit`; `BINARY(8)` → `rowversion`. Importes
  `decimal(18,2)`, cantidades y precios `decimal(18,4)`, porcentajes `decimal(5,2)`, fechas `date`.
  PlazosPago y Parametros usan llave `int`.
- **Enumeraciones (int con CHECK):** Facturas.Status 1 Borrador, 2 Confirmada, 3 Anulada; Gastos.Status
  1 Borrador, 2 Confirmado, 3 Anulado; CuentasPorCobrar/Pagar.Status 1 Pendiente, 2 Parcialmente
  pagada, 3 Pagada, 4 Anulada; Abonos, Pagos e Ingresos.Status 1 Vigente, 2 Anulado; Ingresos.Origin
  1 Factura de contado, 2 Abono, 3 Registro manual; PaymentMethod 1 Efectivo, 2 Transferencia
  bancaria, 3 SINPE Móvil, 4 Tarjeta, 5 Cheque; CuentasContables.Type 1 Activo, 2 Pasivo, 3 Patrimonio,
  4 Ingreso, 5 Gasto; CategoriasFinancieras.Kind 1 Ingreso, 2 Gasto; ProductosServicios.Type
  1 Producto, 2 Servicio; AjustesSaldo.Direction 1 Disminución, 2 Aumento; Asientos.SourceType
  1 Factura, 2 Abono, 3 Ingreso manual, 4 Gasto, 5 Pago, 6 Ajuste de saldo, 7 Nota de crédito,
  8 Traspaso, 9 Liquidación de IVA, 10 Pago de impuesto sobre la renta; Traspasos.Status y
  LiquidacionesIva.Status 1 Vigente, 2 Anulada; NotasCredito.Status 1 Vigente, 2 Anulada;
  PagosImpuestoRenta.Status 1 Vigente, 2 Anulado; Clientes.VatCondition 1 Normal, 2 Institución
  estatal de educación superior, 3 No sujeto; CuentasContables.SystemRole 1 a 9 (9 = 1.3.03 Impuesto
  sobre la renta pagado por anticipado); BitacoraOperaciones.EntityType 1 a 11 (11 = pago de renta).
  Los valores ya asignados no se renumeran.
- **Únicos:** Facturas.Number (nulo en borradores), CuentasPorCobrar.InvoiceId,
  CuentasPorPagar.ExpenseId, Retenciones.ReceivablePaymentId, Ingresos.ReceivablePaymentId,
  UsuariosRoles.UserId, (InvoiceId, LineNumber), (ExpenseCategoryId, PeriodYear, PeriodMonth), Code
  de cada catálogo e Identification (obligatoria) de clientes y proveedores; (PeriodYear, PeriodMonth)
  de LiquidacionesIva filtrado a Status = 1 Vigente (una sola liquidación vigente por mes).
- **Otras restricciones:** CHECK `0 <= Balance <= OriginalAmount`, con el saldo recalculado en la
  misma transacción de cada movimiento; AjustesSaldo con exactamente una de ReceivableId o PayableId;
  Ingresos.Origin coherente con InvoiceId y ReceivablePaymentId; Pagos.PayableId siempre fijo (un
  gasto de contado no genera ningún pago: guarda su cuenta de efectivo y su referencia en el propio
  Gasto, igual que dice la regla 7; corregido respecto a una versión anterior de esta línea, ADR-0028);
  Traspasos con origen distinto de destino; LiquidacionesIva.VatPayableAmount =
  VatCreditApplied + PaymentAmount, con CashAccountId obligatoria solo si PaymentAmount > 0;
  NotasCredito.TotalAmount = AppliedToBalance + RefundedAmount; PagosImpuestoRenta con período fiscal
  de doce meses que inicia el día 1 y PaymentDate ≥ FiscalPeriodStartDate; escala de renta con límites
  crecientes y porcentajes entre 0 y 100.
- **Auditoría:** CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId y RowVersion en
  catálogos y documentos, con FK reales a Usuarios (el diagrama no las dibuja). En catálogos con
  datos iniciales, los usuarios de auditoría admiten nulo para las filas de la migración.
- **Secuencias SQL Server:** número de factura (se pide después de validar la confirmación);
  códigos CLI, PRV, PRD, SRV, REC, GTO, ABO y PAG; número de asiento; las nuevas del paquete v7
  para traspaso, liquidación de IVA y nota de crédito; y `Seq_PagoRentaCodigo` (PIR). Las secuencias
  se crean a mano en la migración: no están en el modelo de EF Core.
- **Clases (diagramas UML):** ApplicationUser, Role, SecurityAuditEvent, OperationLogEntry
  (BitacoraOperaciones), SystemSettings (Parametros), Customer, Supplier, CatalogItem,
  AccountingAccount (LedgerAccount), FinancialCategory, VatRate (TaxType), WithholdingType,
  PaymentTerm, Invoice, InvoiceLine, Receivable, ReceivablePayment (Abonos), Withholding, Income,
  Expense, Payable, SupplierPayment (Pagos), BalanceAdjustment, Budget, BudgetChange, JournalEntry
  (Asientos), JournalEntryLine (AsientoLineas), y del paquete v7: CashTransfer (Traspasos),
  VatSettlement (LiquidacionesIva), CreditNote (NotasCredito), CreditNoteLine (NotaCreditoLineas);
  AccountingPeriodOverride (PeriodosContablesReabiertos), AccountingPeriodChange
  (PeriodosContablesHistorial), FiscalPeriodChange (PeriodoFiscalHistorial); y de RF-99:
  IncomeTaxPayment (PagosImpuestoRenta), IncomeTaxScaleChange (EscalaRentaHistorial), y los valores
  sin tabla IncomeTaxScale e IncomeTaxEstimate.
- **Operaciones publicadas:** Invoice.AddLine, RemoveLine, Confirm(closingDate), AssignNumber,
  Cancel; InvoiceLine.Recalculate; Receivable y Payable.ApplyPayment, RevertPayment, Adjust, Cancel;
  Expense.Recalculate, Confirm(closingDate, authorizationLimit), Cancel; ReceivablePayment,
  SupplierPayment e Income.Cancel; Budget.ChangeAmount; SystemSettings.UpdateFiscalPeriod(start, end),
  AccountingPeriodOverride.Reopen/Close, UpdateLimits, UpdateCompany;
  PaymentTerm.CalculateDueDate; catálogos con Update, Activate y Deactivate (VatRate y
  WithholdingType con ChangeRate; AccountingAccount con Rename). Servicios: InvoiceService.ConfirmAsync,
  ReceivablePaymentService.RegisterAsync, ExpenseService.CheckBudgetAsync y ConfirmAsync. El paquete
  v7 agrega: CashTransfer.Cancel y el registro de CashTransferService; VatSettlement.Cancel y el
  cálculo de VatSettlementService; CreditNote.Cancel y el reparto automático de
  CreditNoteService.RegisterAsync. RF-99 agrega: IncomeTaxPayment.Cancel,
  SystemParameters.GetIncomeTaxScale/UpdateIncomeTaxScale, IncomeTaxEstimate.Calculate e
  IncomeTaxService (RegisterPaymentAsync, CancelPaymentAsync, UpdateScaleAsync, GetEstimateAsync).
- **Asientos (JournalEntry/JournalEntryLine):** inmutables (no se editan ni eliminan): una anulación
  genera un asiento de reversión nuevo, nunca modifica el original. NotasCredito, Traspasos y
  LiquidacionesIva siguen el mismo patrón Vigente/Anulada que los demás documentos financieros (nunca
  se eliminan físicamente).

## Perfiles (roles fijos; sin permisos por usuario)

| Perfil | Permisos |
|---|---|
| Asistente | Consulta catálogos; mantiene clientes y proveedores; consulta facturas y gastos de todos los usuarios y mantiene únicamente sus propios borradores; consulta y registra ingresos manuales; consulta cuentas por cobrar y por pagar, y ve sus indicadores de vencimientos en el inicio; registra abonos a cuentas por cobrar, individuales o repartidos entre varias facturas (ingreso dividido), hasta el límite. No confirma documentos, no registra pagos a proveedores ni pagos de renta, no anula movimientos ni ve asientos, reportes o presupuesto. |
| Finanzas | Catálogos financieros (cuentas, categorías, tarifas, retenciones, productos); confirma documentos; ingresos manuales; abonos, pagos, traspasos y pagos de renta hasta el límite; liquidación del IVA hasta el límite; notas de crédito; presupuesto; consultas y reportes (incluida la estimación de renta); ve la escala de renta en solo lectura. |
| Gerencia | Todo lo de Finanzas y Asistente (ve los borradores de todos), más lo exclusivo: abonos, pagos, traspasos y pagos de renta sobre el límite, pagos de liquidación de IVA sobre el límite, anulaciones (incluidas notas de crédito, traspasos, liquidaciones y pagos de renta), ajustes de saldo y parámetros (límite, días de alerta, datos de la empresa, período fiscal, reaperturas y escala de renta). No administra usuarios. |
| Administrador | Solo usuarios y configuración técnica; sin acceso a información financiera. |

- Gerencia entra en toda política que permita a Finanzas o Asistente; solo las de administración de
  usuarios la excluyen. Lo exclusivo de Gerencia sigue siendo exclusivo.
- Permisos validados siempre en el servidor, además de ocultar opciones; pantalla de solo consulta con
  esta matriz.
- Políticas: los abonos usan `RegisterReceivablePayments` (Gerencia, Finanzas y Asistente), separada
  de `ConfirmFinancialTransactions` (Gerencia y Finanzas); la escala de renta usa
  `ManageAccountingPeriods` (solo Gerencia).
- El documento aún no refleja los permisos ampliados de Gerencia (corrección pendiente en CU-04, 05,
  07, 08, 09, 10, 13, 14, 15 y 19, Tabla 10 y análisis de módulos) ni los abonos del Asistente
  (casos de uso de abonos y matriz de perfiles). Si comparas, manda este archivo.

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

Pasos 1 a 7 completos. Los bloques 0 a 6 del **paquete de mejoras finales**
(`prompt_claude_code_mejoras_finales_v2.md`) están hechos y en el repositorio (último commit
revisado: `26a71fe`, reportes contables y exportaciones):
0. Actualizar `CLAUDE.md` (hecho).
1. Pendientes que el documento ya promete (hecho).
2. Categoría de ingreso en la factura (hecho).
3. Factura preparada para el formato 4.4 de Hacienda (hecho).
4. Catálogo contable ampliado (hecho).
5. Asientos contables automáticos (partida doble) y su reversión al anular (hecho).
6. Reportes contables (libro diario, libro mayor, balanza de comprobación, estado de resultados)
   (hecho).

El paquete v2 quedaba con los bloques 7 (presupuesto y reportes) y 8 (nota de crédito) pendientes; el
bloque 7 se avanzó parcialmente (semáforo de ejecución, indicador de inicio, filtro de presupuesto,
comparativo mensual desde los asientos, reporte "Ingresos recibidos", reportes de IVA del período y
de retenciones por cliente). El **paquete v7** (`docs/paquetes/prompt_claude_code_mejoras_finales_v7.md`)
**reemplaza** al v2 para todo lo que falta: retoma ese trabajo pendiente sin rehacerlo y agrega
decisiones nuevas sobre lo ya construido (condición de IVA del cliente, IVA no acreditable, retención
del 2 % del sector público, regla de creación de cuentas contables) y tres funciones nuevas
(traspasos, liquidación del IVA y antigüedad de cuentas por cobrar), bloque por bloque y esperando
confirmación entre cada uno:
0. Actualizar `CLAUDE.md` y `README.md` (hecho, este cambio).
1. IVA y retenciones que faltaron del paquete anterior (tarifas IVA 2 % y No sujeto, corrección del
   código de Hacienda del Exento, condición de IVA del cliente con tarifa automática por línea,
   retención del 2 % del sector público, IVA no acreditable en gastos, pantalla única "Impuestos y
   retenciones").
2. Datos del formato 4.4 que faltaron (actividad económica del cliente, datos codificados del emisor,
   exportación completa, preparación del recibo electrónico de pago).
3. Regla de creación de cuentas contables (solo bajo los grupos de segundo nivel permitidos, siempre
   de tercer nivel; cuenta nueva bajo 1.1 siempre de efectivo).
4. Traspaso entre cuentas de efectivo.
5. Liquidación mensual del IVA.
6. Presupuesto y reportes con criterios coherentes, terminado (costo real con IVA no acreditable,
   flujo de efectivo desde las líneas de asiento de cuentas de efectivo con desglose por origen,
   antigüedad de cuentas por cobrar y por pagar).
7. Nota de crédito interna con devolución de dinero al cliente.
8. Cierre del paquete (documentación, prueba de extremo a extremo, resumen final).

Los traspasos, la liquidación del IVA y sus reportes se construyeron con el **paquete v8**
(`docs/paquetes/prompt_claude_code_traspasos_iva_v8.md`, bloques 1 a 3, commits `127ae0e`,
`5809846` y `dd79d4c`). Los archivos de paquetes en `docs/paquetes/` son historial de instrucciones
ya aplicadas.

Trabajo posterior (09/11/2026), con código y pruebas pero **sin documentación en `docs/` todavía**:
- RF-99, estimación del impuesto sobre la renta y pagos a Hacienda (regla 17): migración
  `AddIncomeTaxEstimate`; pantallas en `/pagos-renta`, escala en Parámetros y reporte
  `/reportes/estimacion-renta` en pantalla, PDF y Excel.
- Abonos del Asistente (individuales y repartidos) hasta el límite, con la política
  `RegisterReceivablePayments`.
- Limpieza del repositorio: sin salidas de compilación en `artifacts/` (excluidas en
  `Directory.Build.props`), bibliotecas de `wwwroot/lib` reducidas a los archivos que se cargan,
  `README.md` reescrito como resumen del sistema actual.

Pendientes conocidos:
- Documentar RF-99 y los abonos del Asistente en `docs/02-requisitos-trazabilidad.md`,
  `docs/04-casos-de-uso.md` y `docs/05-modelo-datos.md`, con un ADR (el siguiente libre es ADR-0059).
- Con `SISTEMA_FINANCIERO_RUN_SQL_TESTS=1` fallan 22 de 411 pruebas de integración, todas anteriores
  a RF-99: 14 por datos acumulados en la base de desarrollo, fechas fijas y reglas posteriores a la
  prueba, y 8 de la liquidación del IVA sin diagnosticar. La suite normal (sin SQL) pasa completa.

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
