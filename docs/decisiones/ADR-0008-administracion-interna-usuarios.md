# ADR-0008: Administración interna de usuarios

- **Estado:** Aceptado
- **Fecha:** 22 de agosto de 2026
- **Decisión relacionada:** ADR-0004

## Contexto

El registro público está fuera del alcance y el Administrador no hereda permisos financieros. Después
del bootstrap se necesita un flujo controlado para crear los perfiles operativos que utilizarán los
catálogos. La solución debe conservar una lógica de permisos comprensible, revocar accesos oportunamente
y evitar mecanismos propios de almacenamiento de contraseñas.

No existe todavía un servicio de correo institucional integrado. Por ello, la recuperación pública y
los enlaces enviados por correo no forman parte de este incremento.

## Decisión

- Cada usuario posee exactamente uno de los cuatro roles fijos: Administrador, Gerencia, Finanzas o
  Asistente. No se administran nombres de roles desde la interfaz.
- Solo Administrador puede listar, crear, editar, activar, desactivar, desbloquear y restablecer cuentas.
- Las cuentas se desactivan de forma lógica; no existe borrado físico.
- El Administrador define una contraseña temporal que cumple la política de Identity. El sistema nunca
  la registra ni la vuelve a mostrar y obliga al propietario a cambiarla antes de usar otros módulos.
- Un Administrador no puede desactivarse, cambiar su propio rol ni restablecerse desde el flujo
  administrativo. Tampoco puede desactivarse o degradarse al último Administrador activo.
- `ConcurrencyStamp` se utiliza como versión opaca para detectar formularios obsoletos. `SecurityStamp`
  invalida las sesiones después de cambios de correo, rol, estado o contraseña.
- Las operaciones compuestas se ejecutan en transacciones y la bitácora append-only conserva acción,
  actor, objetivo, instante UTC y, cuando corresponde, el nuevo rol. Nunca conserva contraseñas,
  tokens ni hashes.
- El cambio de contraseña propia permanece disponible para todos los roles. No se agrega registro ni
  recuperación pública.

## Consecuencias

- La matriz de permisos sigue siendo excluyente y fácil de explicar; un usuario que cambie de funciones
  recibe un rol nuevo en lugar de acumular permisos.
- La obligación de cambiar la contraseña añade un estado explícito y una comprobación transversal en
  la capa web, pero evita que una contraseña conocida por el Administrador permanezca como credencial
  habitual.
- El Administrador debe entregar la contraseña temporal por un canal interno acordado fuera del sistema.
- Una bitácora persistente permite demostrar cambios de seguridad antes de construir el módulo general
  de auditoría; su consulta se incorporará en un incremento posterior.
- Si en el futuro se requieren responsabilidades simultáneas, la regla de un único rol deberá revisarse
  explícitamente junto con la matriz de permisos.
