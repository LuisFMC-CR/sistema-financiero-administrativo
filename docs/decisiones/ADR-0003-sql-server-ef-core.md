# ADR-0003: SQL Server y Entity Framework Core

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

El anteproyecto establece Microsoft SQL Server. El requisito institucional que menciona MySQL
Workbench corresponde a una plantilla desactualizada y no define el motor de ejecución.

## Decisión

Se utiliza SQL Server Express con Entity Framework Core. El esquema se controla mediante migraciones
versionadas y revisadas antes de aplicarse.

## Consecuencias

- Desarrollo usa `.\SQLEXPRESS` y autenticación integrada de Windows.
- No se utiliza `EnsureCreated` ni scripts manuales como fuente principal del esquema.
- La aplicación no ejecuta migraciones automáticamente en producción.
- El diseño físico y diccionario deberán mantenerse sincronizados con las migraciones.
