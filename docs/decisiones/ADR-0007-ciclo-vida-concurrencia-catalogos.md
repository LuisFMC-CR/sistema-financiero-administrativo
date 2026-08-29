# ADR-0007: Ciclo de vida y concurrencia de catálogos

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

Clientes, proveedores, artículos, categorías, cuentas y tasas serán referenciados por operaciones
financieras. El borrado físico rompería trazabilidad y una actualización simultánea podría perder
cambios sin que el usuario lo advierta.

El sistema también debe identificar quién realizó cambios administrativos sin incorporar una
plataforma externa de auditoría.

## Decisión

- Clientes, proveedores, productos/servicios, categorías y cuentas utilizan estado lógico
  activo/inactivo y no se eliminan desde los casos de uso ordinarios.
- La tasa diaria no posee estado lógico: existe una fila por fecha y cualquier corrección actualiza esa
  fila con auditoría y concurrencia.
- Se conservan creación, última modificación, usuario responsable e instantes UTC.
- Las entidades mutables utilizan una columna SQL Server `rowversion` como token de concurrencia
  optimista.
- Una actualización con versión obsoleta falla de forma controlada y exige recargar; no se aplica una
  estrategia automática de «última escritura gana».
- La reactivación valida de nuevo las invariantes y restricciones vigentes.
- Las claves foráneas que preservan historia o jerarquías restringen el borrado en cascada.

## Consecuencias

- Los registros maestros inactivos permanecen disponibles para consultas históricas y auditoría, pero no para
  operaciones nuevas.
- Los formularios de edición deben devolver el token de concurrencia sin exponerlo como dato editable.
- Application coordina el usuario actual y los instantes; Domain conserva las invariantes y EF Core
  configura el mecanismo físico de concurrencia.
- Las listas deben filtrar activos por defecto y permitir consultar inactivos explícitamente.
- La base requiere más columnas e índices, compensados por mayor integridad y trazabilidad.
