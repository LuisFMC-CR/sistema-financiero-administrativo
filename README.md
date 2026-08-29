# Sistema Financiero Administrativo

Sistema web interno para centralizar el control financiero-administrativo de **Soporte Experto**.

> Este sistema no implementa contabilidad por partida doble, no genera estados financieros oficiales
> y no sustituye los comprobantes electrónicos ni los servicios del Ministerio de Hacienda.

## Estado actual

La solución incluye una base fundacional con:

- solución modular en .NET 10;
- persistencia mediante Entity Framework Core 10 y SQL Server Express;
- autenticación interna con ASP.NET Core Identity, sin registro público;
- roles y políticas de autorización centralizados;
- inicio de sesión, cierre de sesión y cambio de contraseña;
- migración inicial del esquema de seguridad;
- objeto de valor para la conversión histórica CRC/USD;
- pruebas unitarias y de integración fundacionales;
- documentación y trazabilidad iniciales.

El incremento de catálogos incorpora:

- clientes y proveedores como catálogos separados;
- catálogo único de productos y servicios, con precios de referencia opcionales en CRC y USD;
- categorías financieras jerárquicas de ingreso y gasto;
- cuentas financieras de caja o banco, con tipo y moneda fijos;
- un tipo de cambio manual por fecha, expresado en CRC por USD;
- baja lógica, auditoría y control de concurrencia para preservar la trazabilidad.

Los movimientos financieros utilizarán estos catálogos en incrementos posteriores. Una cuenta
financiera no almacena un saldo editable y el catálogo de productos no administra inventario.

El incremento de administración de usuarios incorpora:

- alta, búsqueda, edición, activación y baja lógica de cuentas internas;
- exactamente uno de los cuatro roles aprobados por usuario;
- restablecimiento administrativo y desbloqueo sin recuperación pública;
- contraseña temporal con cambio obligatorio antes de utilizar otros módulos;
- protección de la cuenta propia y del último Administrador activo;
- concurrencia mediante `ConcurrencyStamp`, revocación mediante `SecurityStamp` y bitácora append-only.

Las migraciones `AddBusinessCatalogs` y `AddUserAdministration` están aplicadas en
`SistemaFinanciero_Dev`. La solución compila sin advertencias y posee 122 casos automatizados. Seis de
ellos requieren habilitación explícita para ejecutar sus recorridos transaccionales contra SQL Server;
todos finalizaron correctamente sin conservar datos de prueba.

## Tecnologías

- .NET SDK 10.0.400 y destino `net10.0`.
- ASP.NET Core MVC con Razor.
- Entity Framework Core 10.0.11.
- ASP.NET Core Identity.
- SQL Server 2025 Express.
- xUnit para pruebas automatizadas.

Las versiones de paquetes se administran en `Directory.Packages.props`; no deben utilizarse versiones
flotantes.

## Estructura

```text
src/
  SistemaFinanciero.Domain/          Reglas e invariantes del negocio
  SistemaFinanciero.Application/     Casos de uso, contratos y políticas
  SistemaFinanciero.Infrastructure/  SQL Server, EF Core e Identity
  SistemaFinanciero.Web/             MVC, vistas y composición
tests/
  SistemaFinanciero.UnitTests/
  SistemaFinanciero.IntegrationTests/
docs/                                Documentación técnica y académica viva
```

Las dependencias permitidas están explicadas en [arquitectura](docs/03-arquitectura.md).

## Preparar un entorno local

Requisitos:

- .NET 10 SDK;
- SQL Server Express accesible como `.\SQLEXPRESS`;
- autenticación integrada de Windows;
- certificado HTTPS de desarrollo de ASP.NET Core.

Desde la raíz del repositorio:

```powershell
dotnet tool restore
dotnet restore SistemaFinanciero.sln
dotnet build SistemaFinanciero.sln --no-restore
dotnet ef database update --project src\SistemaFinanciero.Infrastructure --startup-project src\SistemaFinanciero.Web -- --environment Development
dotnet test SistemaFinanciero.sln --no-build --no-restore
```

Las migraciones se aplican explícitamente; la aplicación no modifica el esquema al arrancar.

Para repetir únicamente los escenarios transaccionales contra SQL Server:

```powershell
$env:SISTEMA_FINANCIERO_RUN_SQL_TESTS = "1"
dotnet test tests\SistemaFinanciero.IntegrationTests --filter Category=SqlServer
Remove-Item Env:\SISTEMA_FINANCIERO_RUN_SQL_TESTS
```

Cada escenario revierte su transacción y no conserva clientes, proveedores, cuentas, tasas, usuarios,
roles, asignaciones ni eventos de seguridad de prueba.

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

El comando crea idempotentemente los cuatro roles y el administrador. Retire inmediatamente la
sección `BootstrapAdmin` de los secretos locales. Nunca copie esos valores en archivos versionados,
capturas, incidencias o documentación.

Después de iniciar sesión, el Administrador utiliza **Administración → Usuarios** para crear los
perfiles operativos. La contraseña temporal se entrega por un canal interno y el sistema obliga al
propietario a cambiarla antes de mostrarle cualquier módulo funcional.

### Ejecutar la aplicación

```powershell
dotnet run --project src\SistemaFinanciero.Web
```

La conexión está únicamente en `appsettings.Development.json` y usa autenticación de Windows con
`TrustServerCertificate=True`. Producción falla de forma explícita mientras no reciba una conexión
protegida y certificados confiables mediante configuración externa.

## Documentación

- [Visión, alcance y glosario](docs/01-vision-alcance-glosario.md)
- [Requisitos y trazabilidad](docs/02-requisitos-trazabilidad.md)
- [Arquitectura](docs/03-arquitectura.md)
- [Casos de uso](docs/04-casos-de-uso.md)
- [Modelo de datos](docs/05-modelo-datos.md)
- [Pruebas y evidencias](docs/06-pruebas-evidencias.md)
- [Guía de desarrollo](docs/07-guia-desarrollo.md)
- [Decisiones arquitectónicas](docs/decisiones/)

## Protección de información

El repositorio solo utilizará datos ficticios o anonimizados. No se versionan contraseñas, cadenas de
producción, respaldos, documentos empresariales, identificaciones ni información financiera real.

Los ejemplos de clientes, proveedores, cuentas y tasas deben ser completamente ficticios. Los números
de cuenta completos y otros datos bancarios sensibles no forman parte de este incremento.
