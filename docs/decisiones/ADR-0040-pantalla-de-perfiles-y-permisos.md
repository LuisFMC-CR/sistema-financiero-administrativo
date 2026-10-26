# ADR-0040: Pantalla de perfiles y permisos

- **Fecha:** 2026-10-26
- **Estado:** Aceptado

## Contexto

Paso 7 de `CLAUDE.md`, el último del roadmap: "pantalla de solo consulta con esta matriz" de
perfiles (sección Perfiles del documento). No administra usuarios ni roles: eso ya existe en
`UsersController` (`/administracion/usuarios`, exclusivo de Administrador, ADR-0008).

## Decisión

- `PermissionsController` (ruta `/seguridad/perfiles-y-permisos`) no agrega ninguna política de
  autorización propia: cualquier usuario autenticado la ve, apoyándose solo en la política de
  respaldo ya existente (`RequireAuthenticatedUser`, `AuthorizationExtensions`). Es intencional: la
  pantalla ayuda a cualquier perfil a entender qué puede y qué no puede hacer, y no muestra
  información sensible.
- Sin servicio ni tabla propia: es una vista estática con la matriz ya aprobada en `CLAUDE.md`
  (Asistente, Finanzas, Gerencia, Administrador), igual texto, para que no se desincronice con el
  documento académico.
- Nuevo encabezado "Seguridad" en el menú lateral, separado de "Administración" (que sigue
  mostrando solo "Usuarios", exclusivo de Administrador).

## Consecuencias

- Nuevo: `Web/Controllers/PermissionsController.cs`, `Web/Views/Permissions/Index.cshtml`.
- Modificado: `Views/Shared/_Layout.cshtml` (encabezado "Seguridad" nuevo en el menú).
- `docs/04-casos-de-uso.md` agrega UC-SEG-005; `docs/02-requisitos-trazabilidad.md` agrega
  RF-SEG-007.
- Verificación: `PermissionsAuthorizationTests` (anónimo redirige a inicio de sesión; el endpoint no
  tiene ninguna política de autorización propia, solo la de respaldo). 285 pruebas unitarias y 293
  de integración pasan.
- Verificación manual: la app arranca y la ruta exige inicio de sesión.
- Con este paso termina completo el roadmap de `CLAUDE.md`.
