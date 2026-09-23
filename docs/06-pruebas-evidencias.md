# Pruebas y evidencias

## Estrategia

- **Unitarias:** invariantes de dominio, cálculos y restricciones arquitectónicas.
- **Integración HTTP:** autenticación, autorización, cookies, CSRF y rutas MVC.
- **Integración SQL:** migraciones, restricciones, transacciones y concurrencia contra una base exclusiva.
- **Aceptación:** flujos completos con usuarios de la empresa y datos anonimizados.

No se utilizará EF InMemory como sustituto de SQL Server para validar persistencia.

## Resultados del incremento fundacional

Ejecución: 22 de agosto de 2026, .NET SDK 10.0.400 y EF Core 10.0.11.

```powershell
dotnet build SistemaFinanciero.sln --no-restore
dotnet test SistemaFinanciero.sln --no-build --no-restore
```

Resultado: compilación sin advertencias; **13 pruebas superadas, 0 fallidas**.

| ID | Requisito | Tipo | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PR-ARQ-001 | RNF-ARQ-001 | Unitaria | Domain no referencia otros proyectos. | Cumple. | Superada |
| PR-ARQ-002 | RNF-ARQ-001 | Unitaria | Application no referencia proyectos no permitidos. | Cumple. | Superada |
| PR-ARQ-003 | RN-SEG-001 | Unitaria | Existen cuatro roles únicos aprobados. | Cumple. | Superada |
| PR-MON-001 | RNF-MON-001 | Unitaria | Rechaza una tasa igual a cero. | Lanza error de dominio. | Superada |
| PR-MON-002 | RNF-MON-001 | Unitaria | Convierte USD a CRC y redondea. | Cumple. | Superada |
| PR-MON-003 | RNF-MON-001 | Unitaria | CRC no aplica tipo de cambio. | Cumple. | Superada |
| PR-MON-004 | RNF-MON-001 | Unitaria | Convierte CRC a USD y redondea. | Cumple. | Superada |
| PR-SEG-001 | RF-SEG-004 | Integración | Usuario anónimo es redirigido al login. | HTTP 302 a ruta interna. | Superada |
| PR-SEG-002 | RF-SEG-001 | Integración | La pantalla de login es pública. | HTTP exitoso sobre HTTPS simulado. | Superada |
| PR-SEG-003 | RNF-SEG-001/002 | Integración | Opciones de contraseña y bloqueo coinciden. | Cumple. | Superada |
| PR-SEG-004 | RNF-SEG-001 | Integración | Cookie es segura, HttpOnly y Strict. | Cumple. | Superada |
| PR-SEG-005 | RF-SEG-005 | Integración | No hay endpoint de registro público. | Ruta no mapeada. | Superada |
| EV-DAT-001 | RNF-DAT-001 | Verificación SQL | La migración crea el esquema de seguridad. | 7 tablas y una migración registrada. | Verificada |

## Verificación del incremento de catálogos

Ejecución: 22 de agosto de 2026, .NET SDK 10.0.400, EF Core 10.0.11 y SQL Server Express. La solución
compiló con **0 advertencias y 0 errores**. La suite contiene **62 casos automatizados: 36 unitarios y
26 de integración**. Tres de los casos de integración requieren la variable
`SISTEMA_FINANCIERO_RUN_SQL_TESTS=1`; se habilitaron explícitamente y los **3 recorridos SQL fueron
superados**. Cada uno revirtió su transacción y la revisión posterior no encontró datos de prueba.

| ID | Requisito | Tipo | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PR-CAT-001 | RF-CAT-001, RF-MON-001, RNF-CAT-001 | Integración | Los roles financieros autorizados consultan búsquedas, estados y páginas ordenadas. | Búsqueda y filtro por estado verificados; la página 2 de 21 contactos conserva orden estable y paginación en servidor. | Superada |
| PR-CAT-002 | RF-CAT-002, RN-CAT-001 | Unitaria/Integración | Cliente se crea y mantiene como entidad independiente. | Creación, lectura, actualización y baja lógica verificadas en dominio y SQL Server. | Superada |
| PR-CAT-003 | RF-CAT-003, RN-CAT-001 | Unitaria/Integración | Proveedor se crea y mantiene como entidad independiente. | Creación y búsqueda verificadas sin compartir identidad ni ciclo de vida con clientes. | Superada |
| PR-CAT-004 | RF-CAT-004, RN-CAT-003/004/005 | Unitaria/Integración | Valida tipo, precios opcionales positivos y categoría de ingreso predeterminada. | Dominio rechaza tipos y precios inválidos; SQL conserva cuatro decimales y la relación opcional válida. | Superada |
| PR-CAT-005 | RF-CAT-005, RN-CAT-006/009 | Unitaria/Integración | Rechaza padre de otro tipo, ciclos y cambios de estado que dejarían dependencias activas inválidas. | Se rechazaron padre de otro tipo, ciclo, padre inactivo y baja con hija o artículo activo. | Superada |
| PR-CAT-006 | RF-CAT-006, RN-CAT-007 | Unitaria/Integración | Valida código, tipo y moneda fijos y ausencia de saldo editable. | Tipo y moneda no se pueden modificar; el modelo y la tabla no contienen un campo de saldo. | Superada |
| PR-CAT-007 | RN-CAT-002, RNF-AUD-001 | Unitaria/Integración SQL | Baja y reactivación son lógicas y conservan auditoría. | Los cambios actualizan estado, usuario, instante y versión sin ejecutar borrado físico. | Superada |
| PR-CAT-008 | RN-SEG-004 | Integración HTTP | Los perfiles pueden consultar o escribir únicamente según la matriz de permisos. | Matrices de políticas verificadas para los cuatro roles, rutas anónimas redirigidas al login y acceso HTTP de Finanzas a los formularios de productos/servicios y tipos de cambio. | Superada |
| PR-CAT-009 | RN-CAT-008, RNF-DAT-002 | Integración SQL | Códigos únicos, checks y relaciones rechazan estados inválidos aun fuera de la interfaz. | Duplicados reales fueron rechazados; metadatos EF y SQL confirman índices, checks y claves restrictivas. | Superada |
| PR-CAT-010 | RNF-DAT-003 | Integración SQL | Una versión obsoleta genera conflicto y no pierde la actualización vigente. | Una segunda actualización con `rowversion` obsoleto devolvió conflicto y preservó el primer cambio. | Superada |
| PR-MON-005 | RN-MON-001 | Unitaria/Integración | Rechaza una tasa no positiva y conserva seis decimales. | Tasas no positivas rechazadas; configuración y recorrido SQL validan escala de seis decimales. | Superada |
| PR-MON-006 | RN-MON-002 | Unitaria/Integración SQL | La fecha es única y la fuente es obligatoria. | Dominio exige fuente y SQL Server rechazó una segunda tasa para la misma fecha. | Superada |
| PR-MON-007 | RN-MON-001 | Integración SQL | La tasa realiza un recorrido exacto por SQL Server sin perder precisión. | `510.1234567` se almacenó y recuperó como `510.123457`, según la escala declarada. | Superada |
| EV-DAT-002 | RNF-DAT-002/003, RNF-AUD-001 | Verificación SQL | La migración del incremento crea tablas, restricciones, auditoría y `rowversion`. | `AddBusinessCatalogs` aplicada: 6 tablas, 13 checks, 8 índices únicos, 14 claves foráneas y 6 columnas `rowversion`. | Verificada |

## Verificación del incremento de administración de usuarios

Ejecución: 22 de agosto de 2026, .NET SDK 10.0.400, EF Core 10.0.11 y SQL Server Express. La solución
compiló con **0 advertencias y 0 errores**. La suite completa contiene **125 casos automatizados: 48
unitarios y 77 de integración**. Seis casos requieren habilitación SQL: los tres de catálogos y tres
recorridos nuevos de Identity. Los **6 escenarios SQL fueron superados** y sus transacciones se
revirtieron; las pruebas no agregan ni alteran los registros existentes en la base local.

| ID | Requisito | Tipo | Resultado esperado | Resultado obtenido | Estado |
|---|---|---|---|---|---|
| PR-USR-001 | RF-USR-001 | Integración SQL | Busca y filtra usuarios con orden y paginación estables. | Filtro por Finanzas devolvió 22 cuentas sintéticas y 2 filas correctas en la página 2 de 20. | Superada |
| PR-USR-002 | RF-USR-002, RN-USR-001/002 | Unitaria/Integración SQL | Crea una cuenta activa, correo normalizado, hash no textual y un único rol. | Alta real confirmada; contraseña verificada solo mediante Identity, duplicado rechazado y segundo rol bloqueado por SQL. | Superada |
| PR-USR-003 | RF-USR-003, RN-USR-006 | Integración SQL | Actualiza perfil y rol e invalida sesiones previas. | Nombre, correo y rol cambiaron atómicamente; `SecurityStamp` cambió y se registraron ambos eventos. | Superada |
| PR-USR-004 | RF-USR-004, RN-USR-005/006 | Integración SQL | Desactiva/reactiva sin borrar ni confundir bloqueo. | Estado y sello cambiaron; reactivar conservó `LockoutEnd` y los intentos fallidos existentes. | Superada |
| PR-USR-005 | RF-USR-005, RN-USR-006 | Integración SQL | Restablece contraseña, limpia bloqueo e impone cambio obligatorio. | Contraseña anterior dejó de validar, la temporal validó, se limpió el bloqueo y apareció el claim obligatorio. | Superada |
| PR-USR-006 | RF-USR-006, RN-USR-005 | Integración SQL | Desbloquea sin modificar la contraseña. | `LockoutEnd` quedó nulo, el contador quedó en cero y se agregó `UserUnlocked`. | Superada |
| PR-USR-007 | RF-USR-007 | Integración HTTP/SQL | Una contraseña temporal solo permite cambio propio y logout. | Claim emitido y middleware verificado; el cambio propio eliminó el estado y sustituyó el hash. | Superada |
| PR-USR-008 | RN-USR-003/004 | Integración SQL | Protege automodificación peligrosa y último Administrador activo. | Se rechazaron rol/restablecimiento/baja propios y la baja del único Administrador que permanecía activo. | Superada |
| PR-USR-009 | RNF-USR-001 | Integración SQL | Operaciones compuestas son atómicas y versiones obsoletas no sobrescriben. | Se usaron transacciones/savepoints; `ConcurrencyStamp` obsoleto devolvió conflicto sin cambio parcial. | Superada |
| PR-USR-010 | RNF-USR-003 | Integración HTTP | Solo Administrador accede y no existen rutas públicas o destructivas. | Política exacta, redirecciones anónimas y ausencia de registro, recuperación y DELETE verificadas. | Superada |
| PR-USR-011 | RNF-SEG-001, RNF-USR-002 | Integración de controlador | Ningún formulario vuelve a mostrar una contraseña enviada cuando existe un error. | Login y cambio propio eliminan los valores sensibles del modelo y de `ModelState` sin perder los mensajes de validación. | Superada |
| EV-DAT-003 | RN-USR-001/002, RNF-USR-002 | Verificación SQL | La migración crea estado temporal, bitácora y restricciones físicas. | `AddUserAdministration` aplicada: una tabla, una columna, cuatro checks, un índice único de rol y dos FKs restrictivas. | Verificada |

## Evidencia futura

Cada caso registrará precondición, datos, pasos, esperado, obtenido, versión y evidencia. Las capturas se
guardarán en `docs/evidencias/<incremento>/` únicamente cuando sean necesarias y nunca contendrán datos
reales. Los fragmentos de código para el informe se tomarán de una versión estable, no se duplicarán de
forma anticipada.
