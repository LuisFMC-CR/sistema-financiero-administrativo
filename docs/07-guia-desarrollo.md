# Guía de desarrollo

## Idioma y nombres

- Código e identificadores técnicos en inglés para alinearse con .NET.
- Interfaz, requisitos, casos de uso, documentación y mensajes en español.
- El glosario establece la equivalencia de términos del negocio.
- Nombres expresivos antes que abreviaturas o comentarios compensatorios.

## Documentación del código

- XML docs en tipos y operaciones públicas de Domain y Application, configuraciones, políticas y
  extensiones reutilizables.
- Documentar unidades, moneda, redondeo, nulabilidad e invariantes cuando no sean evidentes.
- Comentarios internos explican el **porqué**, una restricción o un riesgo; no repiten la sintaxis.
- Código generado y migraciones no reciben comentarios manuales.
- Un `TODO` debe incluir incremento o requisito: `TODO(I02/RF-CAT-001)`.
- Si cambia el comportamiento, se actualizan código, prueba y documento relacionado en el mismo cambio.

`GenerateDocumentationFile` está habilitado, pero `CS1591` no es error global para evitar comentarios
vacíos en controladores, DTO y código generado.

## Reglas C#

- `Nullable` y analizadores recomendados habilitados.
- Advertencias tratadas como errores.
- Clases selladas salvo necesidad documentada de herencia.
- Entradas web mediante ViewModels; nunca enlazar entidades directamente para evitar overposting.
- Operaciones asíncronas para acceso a datos.
- `decimal` para dinero; nunca `float` o `double`.
- Excepciones para fallas extraordinarias, no como flujo habitual de validación.
- Registro estructurado sin contraseñas, tokens ni datos financieros innecesarios.

## Base de datos y migraciones

1. Modificar el modelo en Infrastructure.
2. Compilar y ejecutar las pruebas existentes.
3. Crear una migración con un nombre descriptivo.
4. Revisar el código SQL generado, nulabilidad, índices y borrados en cascada.
5. Aplicar primero en desarrollo.
6. Actualizar modelo de datos, diccionario y trazabilidad.

No se edita una migración ya aplicada en un ambiente compartido: se crea una migración correctiva.
Producción nunca migra silenciosamente al iniciar la aplicación.

## Catálogos auditables

- Un catálogo de negocio se desactiva; no se ofrece borrado físico desde la aplicación.
- La reactivación ejecuta nuevamente las invariantes y restricciones de unicidad.
- Creación, última modificación y cambio de estado conservan usuario e instante UTC.
- Cada formulario de edición transporta la versión de concurrencia recibida. Una versión obsoleta
  produce un mensaje controlado y nunca una sobrescritura silenciosa.
- Los valores que representan una identidad histórica, como tipo y moneda de una cuenta, no se enlazan
  como campos editables.
- Las listas aplican búsqueda, filtro, orden y paginación en la consulta; no cargan toda la tabla para
  filtrar en memoria.
- Las consultas de solo lectura no deben realizar seguimiento de cambios cuando este no sea necesario.
- Las restricciones críticas se expresan tanto en Domain como en SQL Server cuando el motor puede
  reforzarlas.
- Las relaciones jerárquicas utilizan borrado restringido y validación explícita de ciclos.

Cliente y proveedor permanecen como agregados separados. No se introduce una entidad genérica de
«tercero» ni se comparten sus identificadores por inferencia.

## Usuarios internos

- Las contraseñas solo se entregan a `UserManager`; nunca se comparan, registran o persisten desde
  código propio.
- Cada cuenta tiene un rol fijo aprobado. Cualquier ampliación a múltiples roles requiere revisar
  ADR-0008 y la matriz completa de permisos.
- Estado inactivo, bloqueo temporal y cambio de contraseña pendiente son condiciones distintas.
- Cambiar correo, rol, estado o contraseña renueva `SecurityStamp`; una edición utiliza
  `ConcurrencyStamp` como versión opaca.
- Las operaciones administrativas compuestas y su evento de auditoría comparten transacción.
- `EventosSeguridad` es append-only: no se actualizan ni eliminan sus filas desde la aplicación.
- Formularios con contraseñas limpian los valores enviados antes de volver a presentar errores y no
  trasladan credenciales mediante URL, `TempData` o logs.

## Secretos y datos

- Desarrollo: User Secrets o autenticación integrada sin contraseña.
- Producción: configuración protegida del servidor.
- Nunca versionar contraseñas, respaldos, `.env`, datos reales o archivos empresariales.
- Semillas y pruebas usan datos ficticios o anonimizados.

## Pruebas

- Una prueba por cada regla financiera o de seguridad crítica.
- Pruebas de persistencia contra SQL Server, no solo dobles en memoria.
- Las pruebas deben ser deterministas, independientes y no depender del orden.
- Una base temporal solo puede eliminarse tras validar un prefijo inequívoco de pruebas.

## Definición de terminado

Una funcionalidad está terminada cuando:

1. cumple criterios de aceptación;
2. compila sin advertencias;
3. posee pruebas proporcionales al riesgo;
4. actualiza requisitos, casos de uso y modelo de datos aplicables;
5. registra mediante ADR cualquier decisión estructural nueva;
6. no introduce secretos, datos reales o dependencias innecesarias.
