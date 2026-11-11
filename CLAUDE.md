# CLAUDE.md — Sistema Financiero Contable (Soporte Experto)

Proyecto final de graduación (UIA). Sistema web interno para el control financiero-administrativo de
una empresa pequeña: 4 usuarios, una PC como servidor y red local cableada sin dominio. El código y
`docs/` deben coincidir con el documento académico, cuyo Capítulo V tiene el diseño completo
(arquitectura, base de datos, procesos y UML). **El documento del TFG está en su versión final; el
sistema está congelado y solo admite correcciones que no cambien lo que el documento describe
(pantallas, campos, reglas, permisos, reportes, modelo de datos).** Si encuentras una diferencia entre
el documento, este archivo y el código, avísame antes de cambiar nada.

## Cómo trabajar conmigo

- Explica cada cambio antes de hacerlo y avanza paso a paso: quiero entender el código nuevo.
- Antes de eliminar, renombrar o mover código, revisa dependencias, explica el impacto y espera mi
  confirmación.
- No agregues paquetes NuGet sin preguntar ni uses versiones flotantes (`Directory.Packages.props`).
  Exportaciones: QuestPDF (licencia Community) para PDF y ClosedXML (MIT) para Excel.
- Cada corrección incluye pruebas y actualiza `docs/02-requisitos-trazabilidad.md` y, si aplica, un
  ADR en `docs/decisiones/` (el siguiente libre es ADR-0060).
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
`$env:SISTEMA_FINANCIERO_RUN_SQL_TESTS = "1"`; se ejecutan en transacciones revertidas y se aíslan de
los datos de la base de desarrollo con `SqlTestIsolation` y el reloj fijo `FixedLocalTimeProvider`. La
herramienta `dotnet-ef` está fijada en `.config/dotnet-tools.json` (`dotnet tool restore`). Si la
aplicación está abierta, bloquea la compilación del proyecto Web: ciérrala antes de compilar o migrar.

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
- Fechas de negocio en `DateOnly`, siempre en hora local: `DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime)`
  (nunca `DateTime.Now`, `DateTime.UtcNow` ni `GetUtcNow().UtcDateTime`). Instantes de auditoría en
  UTC (`DateTimeOffset`, `GetUtcNow()`).
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
  es el mensual automático con el período fiscal, regla 16); la partida doble sí está dentro, en los
  asientos automáticos (regla 1);
- intereses, préstamos y aportes de socios (el sistema no administra deudas ni capital de los socios);
- Hacienda: emisión, firma o transmisión de comprobantes electrónicos o declaraciones tributarias (la
  factura solo guarda datos preparados para el traslado manual a la plataforma externa en el formato
  4.4); la **liquidación interna del IVA** sí está dentro: es un registro interno del pago o del saldo
  a favor (regla 3);
- impuestos distintos del IVA y retenciones a terceros; la **estimación del impuesto sobre la renta**
  y el registro de sus pagos a Hacienda sí están dentro (regla 17), pero siguen fuera el cálculo fiscal
  oficial (ajustes de renta gravable, depreciación, gastos no deducibles), la declaración y la
  provisión contable del impuesto;
- dólares u otras monedas (el sistema opera **solo en colones**) y planilla;
- saldos iniciales, integración y conciliación bancarias (los traspasos entre cuentas de efectivo
  propias sí están dentro, regla 2);
- inventario, nube, app móvil, notificaciones entre usuarios, reportes personalizables y respaldos
  automáticos como función del sistema.

## Módulos (Tabla 4 del documento; nombres del menú)

Gestionar Cuentas Contables · Cuentas por Cobrar · Cuentas por Pagar · Ingresos · Gastos ·
Facturación · Presupuesto · Impuestos y Retenciones · Mantenimiento · Consultas · Reportes · Seguridad.

## Reglas de negocio aprobadas

1. **Cuentas contables.** Catálogo jerárquico con cinco tipos: 1 Activo, 2 Pasivo, 3 Patrimonio,
   4 Ingreso y 5 Gasto. Código generado, nombre, tipo, cuenta superior del mismo tipo (sin ciclos) y
   estado. Las cuentas de efectivo (caja y bancos, `CashKind` no nulo) cuelgan de 1.1. Ingresos,
   gastos, presupuesto y reportes usan directamente las cuentas de ingreso y de gasto de detalle (ya
   no existen categorías financieras). Las cuentas de Pasivo y Patrimonio, y las de 1.2 y 1.3, son
   únicamente **cuentas de sistema** (columna `SystemRole`): las reglas de contabilización las buscan
   por su rol, nunca por código o nombre; no se eligen en ningún formulario de ingresos o gastos; se
   pueden renombrar y desactivar solo si no tienen movimientos en el libro mayor. El botón de
   desactivación siempre se muestra; si hay movimientos, se rechaza la acción con un mensaje claro. Si
   una cuenta de sistema está inactiva, las operaciones que requieran su rol no se contabilizan hasta
   reactivarla. Estas cuentas no se pueden mover. El usuario solo crea cuentas de detalle nuevas,
   siempre de **tercer nivel**, dentro de los grupos de segundo nivel 1.1, 4.1, 4.2, 4.3, 5.1, 5.2 y
   5.3 (`LedgerAccountGroups.DetailParentCodes`; una cuenta nueva bajo 1.1 siempre es de efectivo). La
   partida doble vive en los asientos automáticos (`Asientos`/`AsientoLineas`), no en el catálogo.
2. **Flujo del dinero.**
   - Factura de contado → ingreso, con la fecha de la factura. Factura a crédito → cuenta por cobrar;
     cada abono rebaja el saldo y genera su ingreso. Gastos igual, con cuentas por pagar y pagos. Un
     gasto es solo una compra a un proveedor respaldada por comprobante electrónico.
   - El ingreso de un abono es el **monto recibido** en la caja o el banco, sin la retención (CU-12,
     paso 7); el saldo de la cuenta por cobrar baja por monto + retención y la retención va a 1.3.02.
   - Ingresos manuales: solo otras entradas de la operación (reintegros, intereses que acredite el
     banco); nunca préstamos ni aportes.
   - Todo ingreso indica su cuenta de ingreso y la cuenta de efectivo de destino; todo pago, la de
     origen.
   - Un abono iniciado desde el detalle de una cuenta por cobrar se aplica únicamente a esa cuenta y
     toma de ella el cliente. El reparto de un cobro entre varias cuentas por cobrar se inicia como
     una opción separada en Ingresos: se elige el cliente, monto, cuenta de efectivo y referencia; el
     sistema propone el reparto de la más antigua a la más nueva (editable, con retención por línea),
     crea un abono por cuenta en una transacción con la misma referencia y `GroupId`, compara el límite
     con el **total** y permite anular cada abono individualmente. Los pagos a varias cuentas por pagar
     conservan su propio flujo desde Cuentas por Pagar.
   - El dinero que pasa de una cuenta de efectivo a otra es un **traspaso**, nunca un ingreso ni un
     gasto: no toca cuentas de ingreso o gasto, presupuesto ni estado de resultados. El pago del IVA a
     Hacienda es una **liquidación**, nunca un gasto. El pago del impuesto sobre la renta a Hacienda es
     un **pago de renta** (regla 17): anticipo del impuesto, nunca un gasto.
3. **IVA y retenciones.**
   - Tarifas precargadas por migración: IVA 13 %, IVA 2 %, Exento y No sujeto ("No sujeto" quedó
     inactiva por la migración `DeactivateNotSubjectVatRate`). **Las tarifas no se crean:** solo se
     editan (porcentaje y descripción) y se activan o inactivan; las de sistema no se inactivan. Cada
     cliente tiene una **condición de IVA** (Normal, institución estatal de educación superior o no
     sujeto) que determina automáticamente la tarifa de las líneas de sus facturas; la factura nunca
     ofrece una tarifa que no corresponda a la condición del cliente.
   - Precios sin IVA. **Una tarifa por línea**; el IVA se calcula sobre el neto (bruto − descuento) y
     la línea guarda tarifa, base y monto. El gasto se registra sin IVA y el sistema calcula IVA y
     total. Proveedor obligatorio en todo gasto. En los gastos, el IVA puede marcarse como **no
     acreditable** y entonces forma parte del costo en vez de acreditarse.
   - El IVA se liquida mes a mes contra Hacienda; ese registro no sustituye la declaración oficial. El
     reporte "IVA del período" suma en compras solo el IVA acreditable (CU-22).
   - Fuera del IVA: tasa cero, exoneraciones con documento, prorrata y retenciones de IVA por pagos con
     tarjeta.
   - Retención: la única que aplica es la del **2 % del sector público** (Ley del Impuesto sobre la
     Renta, art. 23, inciso g), sobre el monto sin IVA, solo en operaciones que superan la base mínima
     (un salario base) y solo para clientes marcados como sector público; si no corresponde, el
     sistema no ofrece la retención. Va en el abono con tipo, porcentaje, base (propuesta por el
     sistema, editable dentro del límite que corresponde) y monto, y rebaja el saldo. La retención de
     un abono anulado deja de contar como ya retenida. **Los pagos a proveedores no llevan retención**
     (RF-15). Fuera: retenciones por tarjeta, remesas al exterior, salarios, dividendos y dietas.
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
   - Ajuste de saldo (Gerencia y Finanzas), aumento o disminución con motivo; invariante 0 ≤ saldo ≤
     monto original; no genera ingresos ni pagos ni toca efectivo, documento o presupuesto; un error se
     corrige con otro ajuste en sentido contrario.
   - La factura confirmada se anula desde el flujo de nota de crédito, no con una acción directa. La
     nota de crédito interna no es un ajuste de saldo: documenta la corrección de una venta real
     después de facturar, conserva las líneas originales, liquida el saldo por cobrar y devuelve en
     efectivo lo cobrado. Las retenciones aplicadas se revierten en su cuenta contable, no se entregan
     en efectivo. No existen saldos a favor del cliente ni anulación de una nota ya aplicada.
5. **Moneda.** Solo colones; sin tipo de cambio ni conversiones.
6. **Presupuesto.** Mensual, por cuenta de gasto de último nivel (las superiores solo suman). Solo se
   puede crear o modificar para el mes actual o uno futuro; los meses anteriores quedan disponibles
   únicamente para consulta. La fecha y hora del sistema se obtienen del reloj común `TimeProvider`
   (`TimeProvider.System` en producción). Ejecutado = **monto sin IVA** (`NetAmount`) de los gastos
   confirmados del mes, sin borradores ni anulados; los pagos no lo cambian (RF-40 del documento;
   `BudgetService` y la alerta de `ExpenseService`). Al confirmar un gasto que supera lo disponible se
   muestra una alerta antes de guardar, sin bloquear; si no hay presupuesto, se indica y se permite
   continuar. Monto 0 válido. Cada cambio guarda monto anterior, nuevo, usuario y fecha. Disponible =
   presupuestado − ejecutado (mínimo 0); variación = ejecutado − presupuestado; variación % =
   variación / presupuestado.
7. **Límite de autorización.** Parámetro que **solo Gerencia cambia**. Cubre abonos (individuales o
   repartidos; en el reparto se compara el total), pagos a proveedores, gastos de contado, traspasos,
   devoluciones de notas de crédito, pagos de la liquidación del IVA y pagos de renta: sobre el límite
   solo los registra Gerencia (RF-76). Los cambios solo afectan operaciones posteriores. Un gasto de
   contado sobre el límite no se confirma de contado: va a crédito y Gerencia registra el pago. Al
   confirmar un gasto de contado se indican cuenta de efectivo y referencia; el medio de pago es el del
   gasto.
8. **Reportes** (PDF y Excel): ingresos recibidos por período; gastos por período y cuenta de gasto;
   presupuesto contra lo ejecutado; presupuesto anual a la fecha; estado de cuenta de cliente; cuentas
   por cobrar por cliente y cuentas por pagar por proveedor, con antigüedad de saldos; comparativo
   mensual de ingresos y gastos (desde los asientos); flujo de efectivo mensual (desde las líneas de
   asiento de las cuentas de efectivo: débitos = entradas, créditos = salidas, sin asientos anulados ni
   sus reversiones; sin filtro de cuenta excluye los traspasos); libro diario, libro mayor, balanza de
   comprobación, estado de resultados, IVA del período, retenciones aplicadas por clientes y
   estimación del impuesto sobre la renta por período fiscal (regla 17). Cada reporte muestra nombre,
   parámetros, fecha y hora y usuario. En Excel los montos van como números. Sin datos: mensaje y no se
   genera archivo.
9. **Cuentas vencidas.** Indicador en inicio para Finanzas, Gerencia y Asistente, con cantidad y monto.
   Vencida: vencimiento anterior a hoy (fecha local) y saldo > 0. Próxima: vence en los próximos N días
   (parámetro, 7 por defecto).
10. **Numeración y referencias.** Consecutivo interno de facturas, no fiscal. Al confirmar una
    factura de contado se indican cuenta de efectivo, medio de pago y referencia. Solo se exportan a
    PDF facturas confirmadas o anuladas. Advertir, sin bloquear, si un ingreso, abono, pago, traspaso o
    pago de renta repite la referencia en la misma cuenta de efectivo.
11. **Consultas y filtros.** Cada listado tiene filtros sobre su propia tabla, al menos los de su RF:
    cuentas contables (RF-06), cuentas por cobrar y por pagar (RF-11, RF-17), ingresos (RF-23), gastos
    (RF-30), facturas (RF-36), IVA y retenciones (RF-49); el detalle de cada operación muestra sus
    registros relacionados, su asiento e historial (CU-21). La pantalla única de Consultas se retiró
    (RF-MEN-002 de `docs/02`).
12. **Códigos automáticos**, asignados al crear y no editables: `CLI-0001`, `PRV-0001`, `PRD-0001`,
    `SRV-0001`, `REC-000001` (ingreso), `GTO-000001`, `ABO-000001`, `PAG-000001`, `TRA-000001`
    (traspaso), `LIQ-000001` (liquidación de IVA), `NCR-000001` (nota de crédito) y `PIR-000001` (pago
    de impuesto sobre la renta). Cuentas contables: código jerárquico (`5.1.07`) = siguiente hijo libre
    de la superior; después solo cambian nombre y referencia bancaria. Generación segura ante
    concurrencia (secuencia o índice único con reintento).
13. **Datos iniciales (RF-54)**, cargados por migración (nunca al arrancar): catálogo base, tarifas de
    IVA 13 %, IVA 2 %, Exento y No sujeto, tipo de retención "Retención 2 % sector público" y
    parámetros por defecto (incluida la escala del impuesto sobre la renta de 2026). Los plazos de pago
    son una lista fija en el código (regla 14). Las cuentas de grupo están protegidas; las de detalle se
    pueden renombrar o inactivar, y se agregan nuevas dentro de los grupos habilitados (regla 1).
    Catálogo base vigente (migraciones `SeedBaseChartOfAccounts` y `ExpandChartOfAccountsWithSystemRoles`):
    - 1 Activo › 1.1 Efectivo › 1.1.01 Caja chica, 1.1.02 Cuenta bancaria principal; 1.2 Cuentas por
      cobrar › 1.2.01 Cuentas por cobrar a clientes (sistema); 1.3 Impuestos a favor › 1.3.01 IVA
      acreditable, 1.3.02 Retenciones de renta a favor, 1.3.03 Impuesto sobre la renta pagado por
      anticipado (sistema).
    - 2 Pasivo › 2.1 Cuentas por pagar › 2.1.01 Cuentas por pagar a proveedores (sistema); 2.2
      Impuestos por pagar › 2.2.01 IVA por pagar (sistema).
    - 3 Patrimonio (sin cuentas de detalle).
    - 4 Ingresos › 4.1 Ingresos por servicios › 4.1.01 Soporte técnico, 4.1.02 Mantenimiento y
      reparación de equipos, 4.1.03 Instalación de infraestructura informática; 4.2 Ingresos por ventas
      › 4.2.01 Venta de equipos y repuestos; 4.3 Otros ingresos › 4.3.01 Otros ingresos de la operación,
      4.3.02 Ajustes a favor en cuentas por pagar (sistema); 4.4 Devoluciones y descuentos sobre ventas ›
      4.4.01 Devoluciones y descuentos sobre ventas (sistema).
    - 5 Gastos › 5.1 Gastos de operación › 5.1.01 Alquiler, 5.1.02 Electricidad y agua, 5.1.03 Internet
      y telefonía, 5.1.04 Materiales y repuestos, 5.1.05 Software y licencias, 5.1.06 Transporte y
      combustible; 5.2 Gastos administrativos › 5.2.01 Papelería y suministros de oficina, 5.2.02
      Servicios profesionales, 5.2.03 Comisiones bancarias, 5.2.04 Otros gastos administrativos, 5.2.05
      Ajustes a cuentas por cobrar (sistema); 5.3 Costo de ventas › 5.3.01 Compras de equipos para la
      venta.
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
    período fiscal vigente, **salvo en los meses aún abiertos del período inmediatamente anterior**
    (RF-55 y CU-06): `ClosingDateGuard` acepta fechas desde el inicio del período − 12 meses hasta su
    fin, y en el período anterior solo un mes abierto (el actual o uno reabierto). Fuera de ese rango:
    "No es posible operar con fecha dd/MM/yyyy: está fuera del período fiscal vigente y del período
    anterior."
17. **Impuesto sobre la renta (RF-99 del repositorio; RF-84 y RF-85 del documento).** Es una
    **estimación**, no un cálculo oficial; los textos de pantalla y reportes la llaman así y no mencionan
    declaraciones oficiales.
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
      toca cuentas de ingreso o gasto, presupuesto ni estado de resultados.

Trazabilidad: el repositorio conserva sus propios códigos RF (RF-01 a RF-100, RF-CAT, RF-INV, etc.) y
casos de uso UC-… en `docs/` y en las pruebas; no se renombran. La equivalencia con los RF-01 a RF-85
y los CU-01 a CU-26 del documento final está en `docs/02-requisitos-trazabilidad.md` ("Equivalencias
con el documento final"). Ojo: en `docs/02` RF-99 también nombra el límite de crédito del cliente; en
este archivo y en el código, RF-99 es la estimación del impuesto sobre la renta.

## Modelo de datos aprobado

Columna por columna en `docs/modelo/diseno_fisico_bd_workbench.sql` (tipos de MySQL, solo para el
diagrama). La base real se crea con migraciones de EF Core en SQL Server. Si el código difiere,
explícame la diferencia antes de migrar (el sistema está congelado: no se agregan migraciones).

- **Esquemas (tablas reales):** `seguridad` (Usuarios, Roles, UsuariosRoles, UsuariosClaims,
  RolesClaims, UsuariosLogins, UsuariosTokens, EventosSeguridad, BitacoraOperaciones); `catalogos`
  (Clientes, Proveedores, ProductosServicios, CuentasContables, TiposImpuesto, TiposRetencion);
  `finanzas` (Facturas, FacturaLineas, CuentasPorCobrar, Abonos, Retenciones, Ingresos, Gastos,
  CuentasPorPagar, Pagos, AjustesSaldo, Presupuestos, PresupuestoCambios, Asientos, AsientoLineas,
  Traspasos, LiquidacionesIva, NotasCredito, NotaCreditoLineas, PagosImpuestoRenta,
  EscalaRentaHistorial, ParametrosSistema, ParametrosSistemaHistorial, ParametrosEmpresaHistorial,
  ParametrosFechaCierreHistorial, PeriodoFiscalHistorial, PeriodosContablesReabiertos,
  PeriodosContablesHistorial). Los plazos de pago son una enumeración sin tabla. `ParametrosSistema`
  conserva la columna histórica `ClosingDate` y su historial `ParametrosFechaCierreHistorial`, pero el
  cierre automático ya no los usa; tiene `FiscalPeriodStartDate` y `FiscalPeriodEndDate`, y la escala
  de renta (`IncomeTaxBracket1UpperLimit` a `IncomeTaxBracket3UpperLimit`, `IncomeTaxBracket1Rate` a
  `IncomeTaxBracket4Rate`, `IncomeTaxGrossIncomeThreshold` e `IncomeTaxGeneralRate`).
- **Tipos:** `CHAR(36)` → `uniqueidentifier`; `VARCHAR(n)` → `nvarchar(n)`; `TEXT` → `nvarchar(max)`;
  `DATETIME` → `datetimeoffset` (UTC); `BIT` → `bit`; `BINARY(8)` → `rowversion`. Importes
  `decimal(18,2)`, cantidades y precios `decimal(18,4)`, porcentajes `decimal(5,2)`, fechas `date`.
- **Enumeraciones (int con CHECK):** Facturas.Status 1 Borrador, 2 Confirmada, 3 Anulada; Gastos.Status
  1 Borrador, 2 Confirmado, 3 Anulado; CuentasPorCobrar/Pagar.Status 1 Pendiente, 2 Parcialmente
  pagada, 3 Pagada, 4 Anulada; Abonos, Pagos e Ingresos.Status 1 Vigente, 2 Anulado; Ingresos.Origin
  1 Factura de contado, 2 Abono, 3 Registro manual; PaymentMethod 1 Efectivo, 2 Transferencia
  bancaria, 3 SINPE Móvil, 4 Tarjeta, 5 Cheque; CuentasContables.Type 1 Activo, 2 Pasivo, 3 Patrimonio,
  4 Ingreso, 5 Gasto; ProductosServicios.Type 1 Producto, 2 Servicio; AjustesSaldo.Direction
  1 Disminución, 2 Aumento; Asientos.SourceType 1 Factura, 2 Abono, 3 Ingreso manual, 4 Gasto, 5 Pago,
  6 Ajuste de saldo, 7 Nota de crédito, 8 Traspaso, 9 Liquidación de IVA, 10 Pago de impuesto sobre la
  renta; Traspasos.Status y LiquidacionesIva.Status 1 Vigente, 2 Anulada; NotasCredito.Status
  1 Vigente, 2 Anulada; PagosImpuestoRenta.Status 1 Vigente, 2 Anulado; Clientes.VatCondition 1 Normal,
  2 Institución estatal de educación superior, 3 No sujeto; CuentasContables.SystemRole 1 a 9 (9 =
  1.3.03 Impuesto sobre la renta pagado por anticipado); BitacoraOperaciones.EntityType 1 a 11 (11 =
  pago de renta). Los valores ya asignados no se renumeran.
- **Únicos:** Facturas.Number (nulo en borradores) y ElectronicInvoiceKey, CuentasPorCobrar.InvoiceId,
  CuentasPorPagar.ExpenseId, Retenciones.ReceivablePaymentId, Ingresos.InvoiceId e
  Ingresos.ReceivablePaymentId (filtrados a no nulos), UsuariosRoles.UserId, FacturaLineas
  (InvoiceId, Position), Presupuestos (AccountId, PeriodYear, PeriodMonth), Asientos.Number y
  ReversesEntryId, NotasCredito.InvoiceId, Code de cada catálogo y documento, SystemRole de
  CuentasContables y TiposImpuesto, e Identification (obligatoria) de clientes y proveedores;
  (PeriodYear, PeriodMonth) de LiquidacionesIva filtrado a Status = 1 Vigente (una sola liquidación
  vigente por mes).
- **Otras restricciones:** CHECK `0 <= Balance <= OriginalAmount`, con el saldo recalculado en la
  misma transacción de cada movimiento; AjustesSaldo con exactamente una de ReceivableId o PayableId;
  Ingresos.Origin coherente con InvoiceId y ReceivablePaymentId; Pagos.PayableId siempre fijo (un
  gasto de contado no genera ningún pago: guarda su cuenta de efectivo y su referencia en el propio
  Gasto, regla 7, ADR-0028); Traspasos con origen distinto de destino;
  LiquidacionesIva.VatPayableAmount = VatCreditApplied + PaymentAmount, con CashAccountId obligatoria
  solo si PaymentAmount > 0; NotasCredito.TotalAmount = AppliedToBalance + RefundedAmount;
  PagosImpuestoRenta con período fiscal de doce meses que inicia el día 1 y PaymentDate ≥
  FiscalPeriodStartDate; escala de renta con límites crecientes y porcentajes entre 0 y 100.
- **Auditoría:** CreatedAtUtc, CreatedByUserId, UpdatedAtUtc, UpdatedByUserId y RowVersion en
  catálogos y documentos, con FK reales a Usuarios (el diagrama no las dibuja). En catálogos con
  datos iniciales, los usuarios de auditoría admiten nulo para las filas de la migración.
- **Secuencias SQL Server:** número de factura (se pide después de validar la confirmación); códigos
  CLI, PRV, PRD, SRV, REC, GTO, ABO, PAG, TRA, LIQ, NCR y PIR (`Seq_PagoRentaCodigo`); número de
  asiento. Las secuencias se crean a mano en la migración: no están en el modelo de EF Core.
- **Clases (diagramas UML):** ApplicationUser, Role, SecurityAuditEvent, OperationLogEntry
  (BitacoraOperaciones), SystemParameters (ParametrosSistema), Customer, Supplier, CatalogItem,
  LedgerAccount (CuentasContables), TaxType (TiposImpuesto), WithholdingType, PaymentTerm
  (enumeración), Invoice, InvoiceLine, Receivable, ReceivablePayment (Abonos), Withholding, Income,
  Expense, Payable, SupplierPayment (Pagos), BalanceAdjustment, Budget, BudgetChange, JournalEntry
  (Asientos), JournalEntryLine (AsientoLineas), CashTransfer (Traspasos), VatSettlement
  (LiquidacionesIva), CreditNote (NotasCredito), CreditNoteLine (NotaCreditoLineas),
  AccountingPeriodOverride (PeriodosContablesReabiertos), AccountingPeriodChange
  (PeriodosContablesHistorial), FiscalPeriodChange (PeriodoFiscalHistorial), IncomeTaxPayment
  (PagosImpuestoRenta), IncomeTaxScaleChange (EscalaRentaHistorial), y los valores sin tabla
  IncomeTaxScale e IncomeTaxEstimate.
- **Operaciones publicadas:** Invoice.AddLine, RemoveLine, Confirm, AssignNumber, Cancel;
  InvoiceLine.Recalculate; Receivable y Payable.ApplyPayment, RevertPayment, Adjust, Cancel;
  Expense.Recalculate, Confirm, Cancel; ReceivablePayment, SupplierPayment e Income.Cancel;
  Budget.ChangeAmount; SystemParameters.UpdateFiscalPeriod(start, end), UpdateLimits, UpdateCompany;
  AccountingPeriodOverride.Reopen/Close; PaymentTerm.CalculateDueDate; catálogos con Update, Activate
  y Deactivate (TaxType y WithholdingType con ChangeRate; LedgerAccount con Rename);
  CashTransfer.Cancel; VatSettlement.Cancel; CreditNote.Cancel; IncomeTaxPayment.Cancel;
  SystemParameters.GetIncomeTaxScale/UpdateIncomeTaxScale; IncomeTaxEstimate.Calculate. Servicios:
  InvoiceService.ConfirmAsync, ReceivablePaymentService.RegisterAsync,
  ExpenseService.CheckBudgetAsync y ConfirmAsync, CashTransferService, VatSettlementService,
  CreditNoteService.RegisterAsync, IncomeTaxService (RegisterPaymentAsync, CancelPaymentAsync,
  UpdateScaleAsync, GetEstimateAsync) y SystemParametersService (CreateAsync/UpdateAsync reciben
  `actorIsManagement`).
- **Asientos (JournalEntry/JournalEntryLine):** inmutables (no se editan ni eliminan): una anulación
  genera un asiento de reversión nuevo con la fecha original, nunca modifica el original.
  NotasCredito, Traspasos, LiquidacionesIva y PagosImpuestoRenta siguen el mismo patrón
  Vigente/Anulada que los demás documentos financieros (nunca se eliminan físicamente).

## Perfiles (roles fijos; sin permisos por usuario)

| Perfil | Permisos |
|---|---|
| Asistente | Consulta catálogos; mantiene clientes y proveedores; consulta facturas y gastos de todos los usuarios y mantiene únicamente sus propios borradores; consulta y registra ingresos manuales; consulta cuentas por cobrar y por pagar, y ve sus indicadores de vencimientos en el inicio; registra abonos a cuentas por cobrar, individuales o repartidos entre varias facturas, hasta el límite. No confirma documentos, no registra pagos a proveedores, no anula movimientos, no ve traspasos, liquidaciones de IVA ni pagos de renta (ni siquiera escribiendo la ruta), ni asientos, reportes, presupuesto o parámetros. |
| Finanzas | Catálogos financieros (cuentas contables, tarifas, retenciones, productos); confirma documentos; ingresos manuales; abonos, pagos, gastos de contado, traspasos, devoluciones de notas de crédito, pagos de liquidación de IVA y pagos de renta hasta el límite; notas de crédito; ajustes de saldo; presupuesto; consultas y reportes (incluida la estimación de renta). En Parámetros solo cambia los días de anticipación y consulta en solo lectura el límite, los datos de la empresa, el período fiscal y la escala de renta. |
| Gerencia | Todo lo de Finanzas y Asistente (ve los borradores de todos), más lo exclusivo: operaciones sobre el límite, anulaciones (incluidas notas de crédito, traspasos, liquidaciones y pagos de renta) y los parámetros: límite de autorización, datos de la empresa, período fiscal, reaperturas y cierres de meses, y escala de renta. No administra usuarios. |
| Administrador | Solo usuarios y configuración técnica; sin acceso a información financiera. |

- Gerencia entra en toda política que permita a Finanzas o Asistente; solo las de administración de
  usuarios la excluyen. Lo exclusivo de Gerencia sigue siendo exclusivo.
- Permisos validados siempre en el servidor, además de ocultar opciones; pantalla de solo consulta con
  esta matriz.
- Políticas: los abonos usan `RegisterReceivablePayments` (Gerencia, Finanzas y Asistente), separada
  de `ConfirmFinancialTransactions` (Gerencia y Finanzas), que también protege el listado y el detalle
  de traspasos, liquidaciones de IVA y pagos de renta; `ManageSystemParameters` (Gerencia y Finanzas)
  entra a Parámetros y guarda los días; el límite lo valida el servicio con `actorIsManagement`; los
  datos de la empresa, el período fiscal, las reaperturas y la escala de renta usan
  `ManageAccountingPeriods` (solo Gerencia).

## Estado

Versión final (paquete v9, `docs/prompt_claude_code_version_final_v9.md`, aplicado el 10/11/2026;
ADR-0059). El sistema está congelado. Pruebas: 417 unitarias y 430 de integración; con
`SISTEMA_FINANCIERO_RUN_SQL_TESTS=1` pasan las 847, sin omitidas. Los archivos de `docs/paquetes/` y
`docs/prompt_claude_code_version_final_v9.md` son historial de instrucciones ya aplicadas.

## Lo que el documento necesita del prototipo

- **Entradas (5 pantallas o más):** factura en borrador con líneas; abono a una o varias facturas con
  retención; gasto con la alerta de presupuesto; presupuesto mensual; parámetros con período fiscal.
  Completas, con datos realistas en colones y validaciones junto al campo.
- **Salidas (5 o más):** factura interna en PDF; reportes de ingresos, gastos, presupuesto contra lo
  ejecutado, estado de cuenta y cuentas por pagar por proveedor, en pantalla, PDF y Excel.
- **Programación:** fragmentos cortos y legibles de una entrada (ViewModel con validaciones), una
  salida (PDF o Excel), un proceso (confirmación de factura en una transacción) y una validación de
  dominio (saldo, mes cerrado o límite).
- **Pruebas (5 o más, automatizadas, esperado contra obtenido):** abono que supera el saldo; operación
  en período cerrado; abono o pago sobre el límite sin perfil de Gerencia; factura de contado que
  genera ingreso y a crédito que genera cuenta por cobrar; gasto que supera el presupuesto y muestra
  la alerta sin bloquear (casos citados en la conclusión 4).

Los nombres de tablas, columnas, clases, operaciones y servicios deben coincidir con este archivo.
