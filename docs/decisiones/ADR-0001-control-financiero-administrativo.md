# ADR-0001: Control financiero-administrativo

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

El anteproyecto utiliza el término financiero-contable, pero la empresa requiere una lógica operativa
sencilla y no un sistema contable completo.

## Decisión

El producto controlará facturación interna, cobros, ingresos de efectivo, gastos, cuentas por pagar,
pagos, presupuestos, impuestos, retenciones y reportes administrativos. Las categorías permiten
clasificar movimientos, pero no implementan partida doble.

## Consecuencias

- No existen asientos, débitos, créditos, libro diario, mayor ni estados oficiales.
- Los reportes deben distinguir facturación de cobros y gastos de pagos.
- La documentación no afirmará cumplimiento contable o fiscal que el sistema no proporciona.
- La trazabilidad, inmutabilidad de confirmados y anulaciones siguen siendo obligatorias.
