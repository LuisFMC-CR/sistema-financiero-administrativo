# ADR-0022: Ampliación del acceso de Gerencia a lo operativo financiero

- **Fecha:** 2026-10-21
- **Estado:** Aceptado

## Contexto

ADR-0004 definió a Gerencia como un perfil que solo consulta reportes y anula documentos. En el uso
real del sistema, Gerencia necesita poder realizar cualquier operación financiera u operativa que hoy
hacen Finanzas o Asistente (por ejemplo, crear un cliente) sin depender de otro usuario, conservando
además sus permisos exclusivos (anular, ajustar saldos, definir parámetros, abonos y pagos sobre el
límite de autorización).

Se evaluó si esto debía extenderse también a Usuarios y Configuración técnica (hoy exclusivos de
Administrador). Se decidió que no: el documento separa explícitamente la administración técnica de la
información financiera ("Administrador... sin acceso a información financiera"), y esa segregación de
funciones se conserva en sentido contrario para no mezclar la gestión de cuentas de acceso con la
operación financiera.

## Decisión

Gerencia obtiene, además de lo que ya tenía, todas las políticas que ya cumplían Finanzas y Asistente:

- `Catalogos.Administrar` (productos y servicios, categorías financieras, cuentas contables, tipos de
  impuesto y de retención).
- `Terceros.Administrar` (clientes y proveedores).
- `Movimientos.CrearBorrador` (crear y editar borradores de factura/gasto).
- `Movimientos.Confirmar` (confirmar documentos).

Gerencia no obtiene `Usuarios.Administrar` ni `Configuracion.Administrar`: esas siguen siendo
exclusivas de Administrador.

## Consecuencias

- `AuthorizationExtensions.AddSystemAuthorization` agrega el rol `Gerencia` a las cuatro políticas
  anteriores.
- Los perfiles quedan así: Asistente (borradores propios y catálogos permitidos) ⊂ Finanzas (+
  catálogos financieros, confirmar) ⊂ Gerencia (+ anular, ajustar, parámetros, límite de autorización).
  Administrador queda aparte, sin acceso financiero.
- Se actualizan `CatalogAuthorizationTests` e `InvoiceAuthorizationTests` para exigir exactamente estos
  conjuntos de roles por política.
- `docs/02-requisitos-trazabilidad.md` y la tabla de perfiles de `CLAUDE.md` se actualizan para
  reflejar que Gerencia ya no se limita a consultar y anular.
