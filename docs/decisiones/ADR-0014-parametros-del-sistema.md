# ADR-0014: Parámetros del sistema

- **Fecha:** 2026-09-27
- **Estado:** Aceptado

## Contexto

Dos reglas de negocio dependen de un valor configurable único, no de un catálogo con altas y bajas:

- Regla 7: «Límite de autorización. Parámetro configurable (en CRC). Los abonos y pagos por encima del
  límite solo los registra Gerencia.»
- Regla 9: «Cuentas vencidas... (parámetro: 7 días)...»

La tabla de perfiles asigna a Gerencia «definir los parámetros (límite y días de alerta)». Las
convenciones de datos piden que «las acciones sensibles (confirmar, anular, ajustar, cambios de
parámetros) queden en la bitácora».

## Decisión

### Un solo registro, no un catálogo

- `SystemParameters` (`finanzas.ParametrosSistema`) admite como máximo una fila, con un identificador
  técnico **fijo** (`SystemParameters.SingletonId`) en vez de un `Guid.NewGuid()` por fila. Así la
  unicidad la garantiza la clave primaria, sin necesitar una restricción adicional.
- No tiene baja lógica: los parámetros del sistema siempre están vigentes. Antes de la primera
  configuración, simplemente no existe la fila, y el servicio devuelve nulo.
- La fila no se crea por una migración con un usuario inventado, por el mismo motivo que se evitó para
  las categorías en el ADR-0012: la crea Gerencia la primera vez que guarda valores, con su propio
  usuario como creador.

### Bitácora propia, no la de seguridad

`SecurityAuditEvent`, la bitácora existente, exige una cuenta de usuario objetivo (`TargetUserId`) y
está pensada para acciones de administración de cuentas. Un cambio de parámetros no tiene una cuenta
objetivo, así que se creó `SystemParameterChange` (`finanzas.ParametrosSistemaHistorial`): un registro
inmutable, de solo inserción, con el valor anterior y el nuevo de cada campo, quién lo hizo y cuándo. El
primer registro (la configuración inicial) tiene valores anteriores nulos, porque no existía una
configuración previa.

`FinancialDbContext` protegía solo `SecurityAuditEvent` contra modificaciones o borrados; el método se
generalizó (`EnsureAppendOnlyLogsAreNotMutated`) para proteger igual a `SystemParameterChange`.

### Permisos

- Consultan Gerencia y Finanzas, mediante la política `ViewFinancialReports` que ya existía (evita crear
  una política nueva solo para lectura).
- Solo Gerencia modifica, mediante una política nueva, `ManageSystemParameters`, exclusiva de ese rol:
  ninguna política existente coincidía con «solo Gerencia, nadie más».

### Menú: módulo «Mantenimiento»

La Tabla 4 incluye «Mantenimiento» como módulo, y ninguna pantalla lo usaba todavía. Los parámetros del
sistema se agregaron bajo un encabezado de menú **Mantenimiento**, la interpretación más directa de ese
nombre para una pantalla de configuración general. Si el documento académico define «Mantenimiento» con
otro alcance, este encabezado puede renombrarse o reorganizarse sin tocar el resto del módulo.

## Alternativas consideradas

- **Reutilizar `SecurityAuditEvent` para el cambio de parámetros.** Se descartó: exige una cuenta de
  usuario objetivo que no existe en este caso, y forzarla (por ejemplo, con el propio actor) sería
  engañoso.
- **Solo conservar `UpdatedAtUtc`/`UpdatedByUserId` en la propia fila, sin historial.** Se descartó: es
  lo que ya hacen categorías y cuentas contables, pero pierde el valor anterior y el historial de
  cambios previos, que la palabra «bitácora» de las convenciones de datos sugiere conservar.
- **Sembrar una fila inicial por migración.** Se descartó: no hay un usuario técnico apropiado para
  asignarle como creador antes de que exista el primer inicio de sesión real.

## Consecuencias

- Una migración: `AddSystemParameters`, con dos tablas nuevas y sin datos que migrar.
- Las pantallas de abonos, pagos y del indicador de cuentas vencidas (pasos 2, 3 y 5 pendientes) leerán
  estos parámetros; hasta entonces, el catálogo queda configurado pero sin consumidores.
- El historial no tiene pantalla propia de consulta todavía, igual que la bitácora de seguridad: se
  suma a los índices por instante y actor, preparados para el futuro módulo de Consultas.
- Las pruebas cubren el dominio, el controlador, la autorización, el modelo de persistencia y un
  escenario contra SQL Server, incluida la inmutabilidad del historial.
