# ADR-0051: Indicadores de vencimientos para Asistente

- **Fecha:** 2026-11-07
- **Estado:** Aceptado

## Contexto

ADR-0032 limitaba todos los indicadores de inicio a Gerencia y Finanzas mediante `Reportes.Ver`.
Después de ADR-0050, Asistente tiene acceso de solo consulta a Cuentas por Cobrar y Cuentas por Pagar,
pero el dashboard no mostraba los vencimientos porque los agrupaba con el indicador de presupuesto.

## Decisión

- Gerencia, Finanzas y Asistente ven en el inicio las cuentas por cobrar y pagar vencidas y próximas a
  vencer. El acceso sigue la política `ViewReceivablesAndPayables` (`Cuentas.Ver`).
- El indicador de presupuestos excedidos permanece exclusivo de Gerencia y Finanzas bajo
  `ViewFinancialReports` (`Reportes.Ver`).
- `HomeController` calcula solo los indicadores autorizados para el perfil y la vista presenta cada
  tarjeta independientemente. No se conceden permisos de pago, abono, ajuste ni consulta de reportes.

## Consecuencias

- El modelo de inicio permite presentar los indicadores de vencimiento y de presupuesto de forma
  independiente.
- Se actualizan `RF-CON-001`, `CLAUDE.md` y esta decisión; los cálculos existentes de vencimientos
  permanecen sin cambios.
- `AssistantFinancialAccessTests` verifica que Asistente puede consultar CxC/CxP y no puede acceder a
  presupuesto ni reportes.
