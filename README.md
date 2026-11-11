# Sistema Financiero Contable

Sistema web interno para el control financiero-administrativo de **Soporte Experto**, una empresa
pequeña de servicios informáticos. Es el prototipo del trabajo final de graduación (UIA): 4 usuarios,
una PC como servidor y red local, operando **solo en colones**.

Cada operación genera su asiento contable automático (partida doble). El sistema no emite ni transmite
comprobantes electrónicos, no genera balance general y no administra préstamos, planilla, inventario
ni otras monedas.

**Versión final.** El documento del TFG está en su versión final y el sistema quedó congelado con el
paquete v9 (ADR-0059): solo se aceptan correcciones que no cambien lo que el documento describe.

## Qué hace

| Módulo | Funciones principales |
|---|---|
| Gestionar Cuentas Contables | Catálogo jerárquico (activo de efectivo, pasivo, patrimonio, ingreso y gasto), cuentas de sistema y traspasos entre cuentas de efectivo propias |
| Facturación | Facturas internas en borrador con líneas, confirmación con número consecutivo, PDF y datos preparados para el formato 4.4 de Hacienda; notas de crédito internas |
| Cuentas por Cobrar | Saldos por factura a crédito, abonos individuales o repartidos entre varias facturas, retención del 2 % del sector público y ajustes de saldo |
| Ingresos | Ingresos de facturas de contado, de abonos y registros manuales |
| Gastos | Gastos a proveedores con IVA acreditable o no acreditable y alerta de presupuesto |
| Cuentas por Pagar | Saldos por gasto a crédito, pagos individuales o a varias cuentas y ajustes de saldo |
| Presupuesto | Presupuesto mensual por cuenta de gasto con semáforo de ejecución; lo ejecutado es el monto sin IVA de los gastos confirmados |
| Impuestos y Retenciones | Tarifas de IVA por condición del cliente, retención del 2 %, liquidación mensual del IVA y pagos de impuesto sobre la renta |
| Consultas | Listados con filtros propios y detalle con registros relacionados, asiento e historial; indicador de cuentas vencidas en el inicio |
| Reportes | En pantalla, PDF y Excel (lista abajo) |
| Mantenimiento | Clientes, proveedores, productos y servicios, parámetros del sistema, período fiscal y reapertura de meses |
| Seguridad | Usuarios, perfiles, bitácora de operaciones y bitácora de seguridad |

**Reportes:** ingresos recibidos, gastos por cuenta de gasto, presupuesto contra lo ejecutado, presupuesto
anual a la fecha, comparativo mensual, flujo de efectivo mensual, estado de cuenta de cliente, cuentas
por cobrar por cliente y cuentas por pagar por proveedor con antigüedad de saldos, libro diario, libro
mayor, balanza de comprobación, estado de resultados, IVA del período, retenciones aplicadas por
clientes y estimación del impuesto sobre la renta.

### Reglas que conviene conocer

- **Estados:** los documentos pasan de Borrador a Confirmado y de ahí, solo con Gerencia y motivo, a
  Anulado. Nada financiero se borra: una anulación genera un asiento de reversión.
- **Cierre mensual:** todo mes anterior al actual (según la fecha local) queda cerrado
  automáticamente; Gerencia puede reabrirlo con motivo. No se opera fuera del período fiscal vigente,
  salvo en los meses aún abiertos del período inmediatamente anterior.
- **Límite de autorización:** solo Gerencia lo cambia. Abonos, pagos, gastos de contado, traspasos,
  devoluciones de notas de crédito, pagos de la liquidación del IVA y pagos de renta sobre el límite
  solo los registra Gerencia.
- **Ingresos y flujo de efectivo:** el ingreso de un abono es el dinero recibido, sin la retención; el
  flujo de efectivo se calcula desde los asientos de las cuentas de efectivo.
- **IVA:** precios sin IVA, una tarifa por línea según la condición de IVA del cliente; liquidación
  interna mes a mes.
- **Impuesto sobre la renta:** se estima por período fiscal con la escala de tramos configurada en
  Parámetros (tramos progresivos para pequeñas empresas o tarifa general si la renta bruta supera el
  umbral), sobre la utilidad del estado de resultados, menos las retenciones del 2 % y los pagos
  registrados a Hacienda. Los pagos generan asiento contra la cuenta 1.3.03 y salen de una cuenta de
  efectivo.

### Perfiles

| Perfil | Alcance |
|---|---|
| Asistente | Consulta catálogos y documentos, mantiene clientes y proveedores y sus propios borradores, registra ingresos manuales y abonos hasta el límite. No confirma, no paga a proveedores, no ve traspasos, liquidaciones de IVA, pagos de renta, reportes ni presupuesto. |
| Finanzas | Catálogos financieros, confirmación de documentos, abonos, pagos y traspasos hasta el límite, liquidaciones, notas de crédito, ajustes de saldo, presupuesto, consultas y reportes. En Parámetros solo cambia los días de anticipación. |
| Gerencia | Todo lo anterior, más operaciones sobre el límite, anulaciones y los parámetros: límite de autorización, datos de la empresa, período fiscal, reaperturas y escala de renta. |
| Administrador | Solo usuarios y configuración técnica; sin acceso a información financiera. |

Los permisos se validan siempre en el servidor, además de ocultar opciones.

## Tecnologías

- .NET 10 (SDK 10.0.400), ASP.NET Core MVC con Razor y ASP.NET Core Identity.
- Entity Framework Core 10 sobre SQL Server 2025 Express.
- QuestPDF (licencia Community) para PDF y ClosedXML para Excel.
- xUnit para pruebas automatizadas.
- Bootstrap y jQuery Validation servidos localmente desde `wwwroot/lib` (sin CDN ni Internet).

Las versiones de paquetes están fijadas en `Directory.Packages.props`.

## Estructura

```text
.config/dotnet-tools.json            Herramienta dotnet-ef fijada para el repositorio
src/
  SistemaFinanciero.Domain/          Entidades, estados e invariantes (toda regla de negocio)
  SistemaFinanciero.Application/     Contratos de servicios, comandos, modelos y políticas
  SistemaFinanciero.Infrastructure/  EF Core, transacciones, asientos, Identity y migraciones
  SistemaFinanciero.Web/             Controladores MVC, vistas, PDF y Excel
tests/
  SistemaFinanciero.UnitTests/         Dominio y arquitectura
  SistemaFinanciero.IntegrationTests/  Servicios contra SQL Server, permisos HTTP y exportaciones
docs/                                Documentación académica, decisiones (ADR) y paquetes de trabajo
```

Es un monolito modular: `Domain` ← `Application` ← `Infrastructure` ← `Web`. Las dependencias
permitidas se verifican con pruebas de arquitectura y se explican en
[arquitectura](docs/03-arquitectura.md).

## Preparar un entorno local

Requisitos: .NET 10 SDK, SQL Server Express accesible como `.\SQLEXPRESS` con autenticación de
Windows y el certificado HTTPS de desarrollo de ASP.NET Core.

Desde la raíz del repositorio (PowerShell):

```powershell
dotnet tool restore
dotnet build SistemaFinanciero.sln
dotnet ef database update --project src\SistemaFinanciero.Infrastructure --startup-project src\SistemaFinanciero.Web -- --environment Development
dotnet test SistemaFinanciero.sln
```

Las migraciones se aplican siempre de forma explícita; la aplicación no modifica el esquema al
arrancar. Las migraciones también cargan los datos iniciales: catálogo contable base, tarifas de
IVA, retención del 2 % y escala del impuesto sobre la renta; los plazos de pago son una lista fija.

Las pruebas contra SQL Server se habilitan aparte, siempre revierten sus datos y se aíslan de los
datos ya cargados en la base de desarrollo. En la versión final pasan las 847 (417 unitarias y 430 de
integración):

```powershell
$env:SISTEMA_FINANCIERO_RUN_SQL_TESTS = "1"
dotnet test tests\SistemaFinanciero.IntegrationTests --filter Category=SqlServer
Remove-Item Env:\SISTEMA_FINANCIERO_RUN_SQL_TESTS
```

### Crear el primer administrador

No existe una contraseña predeterminada. En Visual Studio, abra **Administrar secretos de usuario**
sobre `SistemaFinanciero.Web` y agregue temporalmente:

```json
{
  "BootstrapAdmin": {
    "FullName": "Nombre del administrador",
    "Email": "correo-interno@empresa.example",
    "Password": "<contraseña-local-de-al-menos-12-caracteres>"
  }
}
```

Después ejecute una sola vez:

```powershell
dotnet run --project src\SistemaFinanciero.Web -- --bootstrap-admin
```

El comando crea los cuatro roles y el administrador. Retire luego la sección `BootstrapAdmin` de los
secretos locales. El Administrador crea los demás usuarios en **Administración → Usuarios**; cada usuario
debe cambiar su contraseña temporal antes de usar el sistema.

### Ejecutar la aplicación

```powershell
dotnet run --project src\SistemaFinanciero.Web
```

La cadena de conexión solo está en `appsettings.Development.json`. En producción (IIS en Windows 11,
HTTPS con certificado interno e IP fija, SQL Server en el mismo equipo y sin exponer su puerto) la
aplicación falla de forma explícita mientras no reciba una conexión protegida y certificados
confiables por configuración externa.

### Seguridad

Contraseñas de 12 caracteres o más, bloqueo de 15 minutos tras 5 intentos fallidos, sesión de 30
minutos de inactividad con cookies HttpOnly, Secure y SameSite=Strict, HTTPS, protección CSRF y
mensajes de error sin detalles técnicos.

## Documentación

- [Visión, alcance y glosario](docs/01-vision-alcance-glosario.md)
- [Requisitos y trazabilidad](docs/02-requisitos-trazabilidad.md)
- [Arquitectura](docs/03-arquitectura.md)
- [Casos de uso](docs/04-casos-de-uso.md)
- [Modelo de datos](docs/05-modelo-datos.md)
- [Pruebas y evidencias](docs/06-pruebas-evidencias.md)
- [Guía de desarrollo](docs/07-guia-desarrollo.md)
- [Decisiones arquitectónicas (ADR)](docs/decisiones/)
- [Paquetes de trabajo](docs/paquetes/) y el [paquete final v9](docs/prompt_claude_code_version_final_v9.md): instrucciones ya aplicadas, como historial

## Protección de información

El repositorio solo contiene datos ficticios. No se versionan contraseñas, cadenas de producción,
respaldos, documentos de la empresa, identificaciones ni información financiera real.
