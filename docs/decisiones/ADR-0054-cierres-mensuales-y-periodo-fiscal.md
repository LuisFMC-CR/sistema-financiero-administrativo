# ADR-0054: Cierres mensuales automáticos y período fiscal anual

- Estado: Aceptado
- Fecha: 2026-11-07
- Reemplaza: ADR-0038 y ADR-0039 en lo referente al cierre y su validación

## Contexto

La fecha de cierre manual requería que Gerencia avanzara una fecha para cada cierre. Se solicita que
los meses anteriores se cierren automáticamente con el reloj común del sistema y que Parámetros
muestre el período fiscal anual en lugar de la fecha manual. Se acordó que el primer período es
01/01/2026–31/12/2026, que cada período abarca exactamente doce meses, que el motivo al cambiarlo es
opcional y que las reaperturas permanecen vigentes hasta que Gerencia cierre de nuevo ese mes.

## Decisión

1. Se usa `TimeProvider.GetLocalNow()` como fuente única de la fecha de negocio. Un mes anterior al
   mes local actual está cerrado automáticamente. El mes actual y los meses futuros permanecen
   abiertos.
2. Gerencia puede reabrir un mes pasado cerrado indicando un motivo. La excepción permanece vigente
   hasta un cierre explícito posterior de Gerencia. Los cambios de estado son eventos append-only;
   se conserva además el estado actual por período para que el guard consulte rápidamente.
3. Toda operación financiera existente que valida su fecha de negocio usa el mismo guard. Los meses
   reabiertos se permiten y los cerrados de nuevo se rechazan.
4. Gerencia configura inicio y cierre fiscal como un rango inclusivo de exactamente doce meses,
   alineado a meses calendario (inicio el día 1 y cierre el último día). El motivo es opcional; cada
   cambio conserva valores anteriores y nuevos, usuario e instante UTC.
5. Se conserva `ClosingDate` y el historial `ClosingDateChanges` como legado para no perder datos ni
   borrar auditoría. Ninguno participa en las validaciones nuevas. Una migración agrega las fechas y
   las nuevas tablas; cuando existe una fila de parámetros previa sin período, carga el período
   inicial acordado.
6. La fecha de cada operación debe pertenecer al período fiscal configurado. Los meses pasados
   dentro del período requieren además la excepción de reapertura mensual. El reporte de presupuesto
   anual usa los doce meses calendario del período fiscal; el mes de acumulado se selecciona como
   ordinal 1–12 desde el inicio fiscal.

## Consecuencias

- Gerencia ya no desplaza manualmente una fecha para producir el cierre automático de cada mes.
- Una reapertura puede mantenerse por tiempo indefinido, pero queda visible y puede cerrarse de nuevo
  desde Parámetros.
- Reabrir un mes no habilita fechas fuera del período fiscal configurado.
- El servidor aplica la fecha local; los instantes de auditoría se conservan en UTC.
- El esquema incorpora `PeriodosContablesReabiertos`, `PeriodosContablesHistorial` y
  `PeriodoFiscalHistorial`, además de `FiscalPeriodStartDate` y `FiscalPeriodEndDate` en
  `ParametrosSistema`.
- La migración se generó, pero no se aplica a la base de Desarrollo en esta tarea.

## Validación

- Pruebas unitarias de rango fiscal y transiciones del período.
- Pruebas de controlador para leer y guardar el período fiscal.
- Escenarios SQL para reapertura, cierre posterior y validación de operaciones; requieren aplicar la
  migración antes de ejecutarse.
