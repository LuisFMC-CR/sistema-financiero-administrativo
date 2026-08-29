# ADR-0004: Identity y roles internos

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

La información financiera requiere cuentas individuales, permisos en servidor y conservación del
historial. La aplicación será interna y no dispone de un servicio de correo para recuperación pública.

## Decisión

Se usa ASP.NET Core Identity con interfaz MVC propia. Solo existen Administrador, Gerencia, Finanzas y
Asistente. Las capacidades se autorizan mediante políticas; no hay registro público ni autoeliminación.

## Consecuencias

- Administrador gestiona usuarios y configuración técnica, sin heredar automáticamente capacidades
  financieras.
- Finanzas prepara y confirma operaciones; Asistente prepara borradores; Gerencia consulta y anula.
- Contraseñas, bloqueo, cookies y tokens son administrados por Identity.
- El primer administrador se crea explícitamente desde secretos no versionados.
- Las bajas serán lógicas y deberán invalidar sesiones existentes.
