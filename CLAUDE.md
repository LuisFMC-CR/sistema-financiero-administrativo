# CLAUDE.md — Sistema Financiero Administrativo (Soporte Experto)

Proyecto final de graduación (UIA). Sistema web interno para centralizar el control
financiero-administrativo de una empresa pequeña: 4 usuarios, una PC como servidor y una red local
cableada sin dominio. El código y la documentación en `docs/` deben mantenerse coherentes con el
documento académico.

## Cómo trabajar conmigo

- Explica cada cambio antes de hacerlo y avanza paso a paso. Soy estudiante y quiero entender el
  código línea por línea cuando sea nuevo para mí.
- Antes de eliminar, renombrar o mover código existente, valida sus dependencias, explica el impacto y
  espera mi confirmación.
- No agregues paquetes NuGet sin preguntar. No uses versiones flotantes: las versiones se administran
  en `Directory.Packages.props`.
- Cada funcionalidad nueva incluye pruebas y actualiza `docs/02-requisitos-trazabilidad.md` y, si
  aplica, un ADR en `docs/decisiones/`.
- Textos de interfaz, mensajes y documentación en español. Identificadores de código en inglés.

## Comandos (PowerShell, desde la raíz)

```powershell
dotnet build SistemaFinanciero.sln
dotnet test SistemaFinanciero.sln
dotnet ef migrations add <Nombre> --project src\SistemaFinanciero.Infrastructure --startup-project src\SistemaFinanciero.Web
dotnet ef database update --project src\SistemaFinanciero.Infrastructure --startup-project src\SistemaFinanciero.Web -- --environment Development
dotnet run --project src\SistemaFinanciero.Web
```

Las migraciones se aplican explícitamente; nunca uses `EnsureCreated` ni la migración automática al
arrancar. Las pruebas contra SQL Server requieren `$env:SISTEMA_FINANCIERO_RUN_SQL_TESTS = "1"`.

## Arquitectura (no cambiar sin ADR)

- Monolito modular: `Domain` ← `Application` ← `Infrastructure` ← `Web` (ver `docs/03-arquitectura.md`).
- **Domain:** entidades, estados e invariantes. Toda regla de negocio vive aquí (por ejemplo, un abono
  no supera el saldo). No conoce EF Core, MVC ni Identity.
- **Application:** contratos (`I...Service`), comandos, modelos y políticas.
- **Infrastructure:** implementación de los servicios con EF Core, transacciones, Identity y
  migraciones. Los servicios coordinan; no duplican reglas del dominio.
- **Web:** MVC con Razor, ViewModels y autorización por políticas en el servidor.
- Sin microservicios, CQRS, MediatR, AutoMapper ni repositorios genéricos.
- Las pruebas de arquitectura en `tests/SistemaFinanciero.UnitTests/Architecture` deben seguir pasando.

## Convenciones de datos

- Dinero en `decimal`, nunca `float`, `double` ni `money`. Cantidades y precios unitarios con 4
  decimales. Importes de línea, impuestos, totales, saldos, abonos y pagos redondeados a **2
  decimales** (`MidpointRounding.AwayFromZero`). Tasas de cambio con 6 decimales.
- Fechas de negocio en `DateOnly`; instantes de auditoría en UTC (`DateTimeOffset`).
- Las entidades mutables usan `rowversion`. Un conflicto se informa al usuario y nunca se sobrescribe.
- Catálogos: baja lógica (activo/inactivo). Documentos financieros: **nunca** se eliminan físicamente.
- Todo registro conserva el usuario y la fecha de creación y de última modificación. Las acciones
  sensibles (confirmar, anular, ajustar, cambios de parámetros) quedan en la bitácora.

## Alcance: fuera del sistema

Partida doble (asientos, diario, mayor, estados financieros); emisión o envío de comprobantes a
Hacienda; integración bancaria; nómina y horas extra; inventario; nube o aplicación móvil; flujos de
solicitudes o notificaciones entre usuarios; reportes personalizables; respaldos automáticos como
función del sistema. Si una tarea parece requerir algo de esto, detente y pregúntame.

## Módulos que deben existir (Tabla 4 del documento; definen si el proyecto cumple)

Gestionar Cuentas Contables · Cuentas por Cobrar · Cuentas por Pagar · Ingresos · Gastos ·
Facturación · Presupuesto · Impuestos y Retenciones · Mantenimiento · Consultas · Reportes · Seguridad.
Usa estos nombres en el menú y en las pantallas.

## Reglas de negocio aprobadas

1. **Cuentas contables.** Un solo catálogo con código único, nombre, tipo (Activo, Pasivo, Patrimonio,
   Ingreso o Gasto), cuenta superior opcional (jerarquía sin ciclos) y estado. Caja y bancos son
   cuentas de Activo marcadas como *de efectivo*, con moneda fija; reemplazan a `FinancialAccount`.
   Cada categoría de ingreso o de gasto apunta a una cuenta contable de su mismo tipo. Es solo
   clasificación, no partida doble.
2. **Flujo del dinero.**
   - Factura de contado: al confirmarse genera su ingreso.
   - Factura a crédito: genera una cuenta por cobrar con fecha de vencimiento. Cada abono rebaja el
     saldo y genera su ingreso.
   - Gastos: igual, con cuentas por pagar y pagos.
   - Se permiten ingresos manuales para otros ingresos.
   - Todo ingreso indica su categoría (y, por ella, su cuenta contable) y la cuenta de efectivo de
     destino. Todo pago indica la cuenta de efectivo de origen.
   - Los saldos se calculan a partir de los movimientos vigentes; no son campos editables.
3. **Impuestos y retenciones.**
   - Catálogo de tipos de impuesto y de retención con tarifas configurables.
   - Los precios se registran sin impuesto incluido.
   - El impuesto se calcula por línea sobre el monto neto (bruto menos descuento).
   - Cada línea conserva código, nombre, método, tarifa, **base** y monto calculado.
   - Una retención se registra al aplicar un abono o un pago: tipo, porcentaje, base (por defecto el
     neto sin impuestos del documento, editable) y monto. La retención también rebaja el saldo.
   - Los cambios posteriores del catálogo no alteran documentos confirmados.
4. **Estados y correcciones.**
   - Documentos: Borrador (editable) → Confirmado (inmutable) → Anulado (con motivo, usuario y fecha).
   - No se anula un documento con abonos o pagos vigentes sin anular primero esos movimientos.
   - Ajuste de saldo con motivo para cuentas por cobrar y por pagar.
5. **Moneda.** Colones (CRC) y dólares (USD). Cada documento usa una sola moneda y conserva la tasa CRC
   por USD al confirmarse. Los abonos y pagos se hacen en la moneda del documento, con una cuenta de
   efectivo de esa moneda. El presupuesto y los reportes consolidados se expresan en CRC, convirtiendo
   con la tasa de cada documento.
6. **Presupuesto.** Mensual, por categoría de gasto, en CRC. Lo ejecutado son los gastos confirmados
   del mes. Al confirmar un gasto que supera lo disponible se muestra una alerta, pero no se bloquea el
   registro.
7. **Límite de autorización.** Parámetro configurable (en CRC). Los abonos y pagos por encima del
   límite solo los registra Gerencia.
8. **Reportes** (exportables a PDF y Excel): ingresos por período, gastos por período y categoría,
   presupuesto contra lo ejecutado, estado de cuenta por cliente y cuentas por pagar por proveedor.
   La factura interna tiene su propio documento imprimible, rotulado como "documento administrativo
   interno, no es comprobante electrónico".
9. **Cuentas vencidas.** Indicador de cuentas por cobrar y por pagar vencidas y próximas a vencer
   (parámetro: 7 días) en la pantalla de inicio y en Consultas.
10. **Numeración.** Consecutivo interno de facturas. No es fiscal.

## Perfiles (roles fijos; no hay permisos por usuario)

| Perfil | Permisos |
|---|---|
| Asistente (rol básico; lo usa también recepción) | Consulta catálogos; mantiene clientes y proveedores; registra **borradores** de facturas y gastos. No ve reportes, presupuesto ni saldos. |
| Finanzas | Mantiene catálogos financieros (cuentas contables, categorías, impuestos, retenciones, productos, tipo de cambio); confirma documentos; registra abonos y pagos hasta el límite; registra presupuestos; consulta todo lo operativo y los reportes. |
| Gerencia | Consulta y exporta reportes; registra abonos y pagos sobre el límite; anula documentos; hace ajustes de saldo; define los parámetros (límite y días de alerta). |
| Administrador | Solo usuarios y configuración técnica. Sin acceso a información financiera. |

Debe existir una pantalla de solo consulta que muestre esta matriz de permisos por perfil. Los
permisos se validan siempre en el servidor, además de ocultar opciones en la interfaz.

## Estado actual y pendientes (en este orden)

Existen: seguridad y usuarios, clientes, proveedores, productos y servicios, categorías, cajas y
bancos (`FinancialAccount`), tipo de cambio, y el dominio y la persistencia de facturas (sin servicios,
pantallas ni pruebas).

Ajustes al trabajo de facturas ya iniciado:
- agregar condición de contado o crédito y la fecha de vencimiento;
- calcular los impuestos de la línea a partir de un tipo de impuesto del catálogo, usando el neto de
  la línea como base, y guardar la base;
- redondear importes a 2 decimales;
- permitir editar y quitar líneas y datos del encabezado mientras la factura esté en borrador;
- corregir RF-INV-006 en la trazabilidad: Asistente no registra cobros.

Después, construir en este orden:
1. Catálogos faltantes: cuentas contables (unificando cajas y bancos, con migración de datos); tipos
   de impuesto; tipos de retención; parámetros; relación entre categoría y cuenta contable.
2. Facturación, cuentas por cobrar, abonos e ingresos (automáticos y manuales).
3. Gastos, cuentas por pagar y pagos.
4. Presupuesto y alerta.
5. Consultas, indicador de vencidas y reportes con exportación.
6. Anulaciones y ajustes en todos los documentos.
7. Pantalla de perfiles y permisos.

Priorizar flujos completos y demostrables (factura → cuenta por cobrar → abono → ingreso → reporte; gasto →
cuenta por pagar → pago → presupuesto) por encima de pantallas secundarias.
