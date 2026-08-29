# Requisitos y trazabilidad

## Convenciones

- `RF`: requisito funcional.
- `RN`: regla de negocio.
- `RNF`: requisito no funcional.
- `UC`: caso de uso.
- `PR`: prueba.
- `ADR`: decisión arquitectónica.
- `EV`: evidencia.

Estados: **Propuesto**, **Aprobado**, **En desarrollo**, **Implementado**, **Verificado** o **Descartado**.

## Matriz del incremento fundacional

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-SEG-001 | Un usuario interno puede iniciar sesión con correo y contraseña. | Anteproyecto | UC-SEG-001 | `AccountController.Login` | Pendiente con usuario real | Implementado |
| RF-SEG-002 | Un usuario autenticado puede cerrar su sesión mediante POST. | Seguridad | UC-SEG-002 | `AccountController.Logout` | Pendiente con usuario real | Implementado |
| RF-SEG-003 | Un usuario autenticado puede cambiar su contraseña. | Seguridad | UC-SEG-003 | `AccountController.ChangePassword` | Pendiente con usuario real | Implementado |
| RF-SEG-004 | Los recursos nuevos exigen autenticación salvo excepción explícita. | Seguridad | UC-SEG-001 | Política global | PR-SEG-001 | Verificado |
| RF-SEG-005 | No existe registro público de usuarios. | Alcance aprobado | UC-SEG-004 | Identity sin UI predeterminada | PR-SEG-005 | Verificado |
| RF-SEG-006 | El primer administrador y los roles se crean mediante un comando explícito e idempotente. | Seguridad | UC-SEG-004 | `IdentityBootstrapper` | Pendiente de bootstrap local | Implementado |
| RN-SEG-001 | Solo existen Administrador, Gerencia, Finanzas y Asistente. | Aprobación del usuario | UC-SEG-004 | `SystemRoles` | PR-ARQ-003 | Verificado |
| RN-SEG-002 | Un usuario inactivo no puede iniciar sesión. | Control interno | UC-SEG-001 | `AccountController.Login` | Pendiente | Implementado |
| RN-SEG-003 | Los permisos se validan en el servidor mediante políticas. | Control interno | Todos | `SystemPolicies` y configuración web | Pendiente por rol | Implementado |
| RNF-SEG-001 | Las contraseñas se administran con Identity y nunca se almacenan en texto. | Seguridad | Todos | ASP.NET Core Identity | PR-SEG-003 | Verificado |
| RNF-SEG-002 | Cinco fallos bloquean la cuenta durante 15 minutos. | Seguridad | UC-SEG-001 | Opciones de Identity | PR-SEG-003 | Verificado |
| RNF-DAT-001 | La persistencia utiliza SQL Server Express mediante EF Core. | Anteproyecto | No aplica | `FinancialDbContext` | EV-DAT-001 | Verificado |
| RNF-ARQ-001 | Domain no depende de infraestructura o presentación. | ADR-0002 | No aplica | Referencias de proyectos | PR-ARQ-001 | Verificado |
| RNF-MON-001 | La conversión histórica utiliza CRC por USD y redondeo uniforme. | ADR-0005 | No aplica | `ExchangeRate` | PR-MON-001 a PR-MON-004 | Verificado |
| RNF-DOC-001 | Cada incremento actualiza código, pruebas y documentación relacionada. | Instructivo | No aplica | Documentos `docs/` | Revisión por incremento | En desarrollo |

## Matriz del incremento de catálogos

El incremento fue verificado mediante pruebas unitarias, integración HTTP, inspección del modelo EF y
tres escenarios transaccionales contra SQL Server. La evidencia física se registra como `EV-DAT-002`.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CAT-001 | Gerencia, Finanzas y Asistente pueden consultar, buscar y filtrar los catálogos autorizados. | Alcance aprobado | UC-CAT-001 | Servicios `SearchAsync` y vistas `Index` | PR-CAT-001, PR-CAT-008 | Verificado |
| RF-CAT-002 | Finanzas y Asistente pueden crear, modificar, desactivar y reactivar clientes. | Alcance aprobado | UC-CAT-002 | `CustomersController` / `CustomerCatalogService` | PR-CAT-002, PR-CAT-007, PR-CAT-008 | Verificado |
| RF-CAT-003 | Finanzas y Asistente pueden crear, modificar, desactivar y reactivar proveedores. | Alcance aprobado | UC-CAT-003 | `SuppliersController` / `SupplierCatalogService` | PR-CAT-003, PR-CAT-007, PR-CAT-008 | Verificado |
| RF-CAT-004 | Finanzas puede administrar el catálogo único de productos y servicios y sus precios de referencia. | Alcance aprobado | UC-CAT-004 | `CatalogItemsController` / `CatalogItemService` | PR-CAT-004, PR-CAT-008 | Verificado |
| RF-CAT-005 | Finanzas puede administrar categorías financieras jerárquicas de ingreso y gasto. | Alcance aprobado | UC-CAT-005 | `FinancialCategoriesController` / `FinancialCategoryService` | PR-CAT-005, PR-CAT-008 | Verificado |
| RF-CAT-006 | Finanzas puede administrar cuentas financieras de caja o banco. | Alcance aprobado | UC-CAT-006 | `FinancialAccountsController` / `FinancialAccountService` | PR-CAT-006, PR-CAT-008 | Verificado |
| RF-MON-001 | Gerencia, Finanzas y Asistente pueden consultar los tipos de cambio por fecha. | ADR-0005 | UC-MON-001 | `ExchangeRatesController` / `DailyExchangeRateService` | PR-CAT-001 | Verificado |
| RF-MON-002 | Finanzas puede registrar y corregir una tasa manual por fecha, indicando su fuente. | Alcance aprobado | UC-MON-001 | `ExchangeRatesController` / `DailyExchangeRateService` | PR-MON-005, PR-MON-006, PR-MON-007 | Verificado |
| RN-CAT-001 | Cliente y proveedor son entidades separadas aun cuando representen a la misma persona física o jurídica. | ADR-0006 | UC-CAT-002, UC-CAT-003 | `Customer` y `Supplier` | PR-CAT-002, PR-CAT-003 | Verificado |
| RN-CAT-002 | Los registros maestros se desactivan o reactivan; no se eliminan físicamente. | ADR-0007 | UC-CAT-002 a UC-CAT-006 | Entidades y acciones `SetActive` | PR-CAT-007 | Verificado |
| RN-CAT-003 | Cada artículo del catálogo es exactamente Producto o Servicio y no administra existencias. | Alcance aprobado | UC-CAT-004 | `CatalogItem` | PR-CAT-004 | Verificado |
| RN-CAT-004 | Los precios CRC y USD son opcionales e independientes; cualquier precio informado debe ser positivo y admite cuatro decimales. | Alcance aprobado | UC-CAT-004 | `CatalogItem` y checks SQL | PR-CAT-004 | Verificado |
| RN-CAT-005 | Un producto o servicio puede indicar opcionalmente una categoría activa de ingreso predeterminada. | Alcance aprobado | UC-CAT-004 | `CatalogItemService` | PR-CAT-004 | Verificado |
| RN-CAT-006 | El tipo Ingreso/Gasto de una categoría es fijo; su padre debe tener el mismo tipo y la jerarquía no admite autorreferencias ni ciclos. | Alcance aprobado | UC-CAT-005 | `FinancialCategoryService` | PR-CAT-005 | Verificado |
| RN-CAT-007 | Una cuenta financiera posee código, tipo Caja/Banco y moneda CRC/USD; tipo y moneda no cambian después de crearla y no almacena saldo. | Alcance aprobado | UC-CAT-006 | `FinancialAccount` | PR-CAT-006 | Verificado |
| RN-CAT-008 | El código es obligatorio, se normaliza en mayúsculas y es único dentro de cada catálogo. | Control de integridad | UC-CAT-002 a UC-CAT-006 | Entidades e índices SQL | PR-CAT-009 | Verificado |
| RN-CAT-009 | Una categoría no se desactiva si tiene hijas activas o artículos activos que la usan; reactivar una hija exige que su padre esté activo. | Integridad jerárquica | UC-CAT-005 | `FinancialCategoryService` | PR-CAT-005 | Verificado |
| RN-MON-001 | La tasa expresa CRC por USD, es positiva y conserva seis decimales. | ADR-0005 | UC-MON-001 | `ExchangeRate` / `DailyExchangeRate` | PR-MON-005, PR-MON-007 | Verificado |
| RN-MON-002 | Solo puede existir una tasa operativa por fecha de negocio y su fuente es obligatoria. | Alcance aprobado | UC-MON-001 | Entidad e índice `UX_TiposCambioDiarios_EffectiveDate` | PR-MON-006 | Verificado |
| RN-MON-003 | Corregir una tasa maestra no recalculará documentos confirmados, los cuales conservarán la tasa utilizada. | ADR-0005 | UC-MON-001 | Uso futuro por movimientos | Prueba en incremento transaccional | Aprobado |
| RN-SEG-004 | Finanzas y Asistente mantienen clientes/proveedores; solo Finanzas modifica artículos, precios, categorías, cuentas y tasas. | Alcance aprobado | UC-CAT-002 a UC-MON-001 | `AuthorizationExtensions` | PR-CAT-008 | Verificado |
| RNF-CAT-001 | Los listados utilizan orden estable, búsqueda, filtro de estado y paginación del lado del servidor. | Calidad de uso | UC-CAT-001 | `CatalogQuery`, `PageQuery` y servicios SQL | PR-CAT-001 | Verificado |
| RNF-DAT-002 | SQL Server refuerza valores permitidos, positividad, unicidad y relaciones mediante restricciones e índices. | Control de integridad | Todos | Configuraciones EF Core | PR-CAT-009, PR-MON-006, EV-DAT-002 | Verificado |
| RNF-DAT-003 | Los catálogos mutables detectan actualizaciones concurrentes mediante `rowversion`. | ADR-0007 | Casos de mantenimiento | Configuraciones EF Core y servicios | PR-CAT-010 | Verificado |
| RNF-AUD-001 | Creación, modificación y, cuando corresponde, cambio de estado conservan usuario responsable e instante UTC. | ADR-0007 | Casos de mantenimiento | Entidades y FKs de auditoría | PR-CAT-007 | Verificado |

Las reglas aún pendientes sobre impuestos, saldos iniciales y documentos financieros se agregarán en
sus respectivos incrementos; no forman parte de estos catálogos.

## Matriz del incremento de administración de usuarios

El incremento extiende Identity sin agregar registro ni recuperación pública. Los roles continúan
siendo valores fijos y la bitácora de seguridad no conserva contraseñas, tokens ni hashes.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-USR-001 | Administrador puede listar, buscar y filtrar usuarios por estado y rol mediante paginación del servidor. | ADR-0008 | UC-USR-001 | `UsersController.Index` / `UserAdministrationService.SearchAsync` | PR-USR-001, PR-USR-010 | Verificado |
| RF-USR-002 | Administrador puede crear una cuenta activa con nombre, correo único, contraseña temporal y un rol aprobado. | ADR-0008 | UC-USR-001 | `UsersController.Create` / `UserAdministrationService.CreateAsync` | PR-USR-002 | Verificado |
| RF-USR-003 | Administrador puede modificar nombre, correo y rol de una cuenta existente. | ADR-0008 | UC-USR-001 | `UsersController.Edit` / `UserAdministrationService.UpdateAsync` | PR-USR-003 | Verificado |
| RF-USR-004 | Administrador puede desactivar y reactivar cuentas sin borrarlas físicamente. | ADR-0008 | UC-USR-001 | `UsersController.SetActive` | PR-USR-004, PR-USR-010 | Verificado |
| RF-USR-005 | Administrador puede asignar una contraseña temporal sin conocer ni exponer el hash almacenado. | ADR-0008 | UC-USR-002 | `UsersController.ResetPassword` | PR-USR-005 | Verificado |
| RF-USR-006 | Administrador puede eliminar un bloqueo temporal sin cambiar la contraseña. | Control interno | UC-USR-002 | `UsersController.Unlock` | PR-USR-006 | Verificado |
| RF-USR-007 | Una cuenta con contraseña temporal debe cambiarla antes de utilizar cualquier otro módulo. | ADR-0008 | UC-SEG-003 | `MustChangePassword` y middleware web | PR-USR-007 | Verificado |
| RN-USR-001 | Cada usuario posee exactamente uno de los cuatro roles aprobados; los roles no tienen CRUD. | ADR-0008 | UC-USR-001 | `SystemRoles` / `UserAdministrationService` | PR-USR-002, PR-USR-003 | Verificado |
| RN-USR-002 | Correo y nombre de usuario coinciden y son únicos después de la normalización de Identity. | Seguridad | UC-USR-001 | `UserManager` e índices Identity | PR-USR-002 | Verificado |
| RN-USR-003 | No puede desactivarse ni degradarse al último Administrador activo. | Disponibilidad | UC-USR-001 | `UserAdministrationService` | PR-USR-008 | Verificado |
| RN-USR-004 | Un Administrador no puede desactivarse, cambiar su rol ni restablecerse desde el flujo administrativo. | Menor privilegio | UC-USR-001/002 | `UserAdministrationService` | PR-USR-008 | Verificado |
| RN-USR-005 | Inactividad y bloqueo temporal son estados distintos; reactivar no implica desbloquear. | Control interno | UC-USR-001/002 | Identity y acciones separadas | PR-USR-004, PR-USR-006 | Verificado |
| RN-USR-006 | Cambiar correo, rol, estado o contraseña invalida las sesiones anteriores de la cuenta afectada. | Seguridad | UC-USR-001/002 | `SecurityStamp` | PR-USR-003 a PR-USR-006 | Verificado |
| RNF-USR-001 | Operaciones compuestas son transaccionales y los formularios obsoletos se rechazan mediante `ConcurrencyStamp`. | Integridad | UC-USR-001/002 | Servicio Identity y EF Core | PR-USR-009 | Verificado |
| RNF-USR-002 | Cambios administrativos registran acción, actor, objetivo e instante UTC sin secretos. | Auditoría | UC-USR-001/002 | `EventosSeguridad` | PR-USR-002 a PR-USR-006 | Verificado |
| RNF-USR-003 | No existen endpoints públicos de registro, recuperación de contraseña o borrado de usuarios. | Superficie mínima | Todos | Rutas MVC explícitas | PR-USR-010 | Verificado |
