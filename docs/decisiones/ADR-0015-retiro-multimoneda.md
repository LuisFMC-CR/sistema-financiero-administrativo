# ADR-0015: Retiro del manejo multimoneda

- **Fecha:** 2026-10-11
- **Estado:** Aceptado
- **Reemplaza:** [ADR-0005](ADR-0005-manejo-multimoneda.md)

## Contexto

El ADR-0005 diseñó un manejo CRC/USD con tipo de cambio diario. Al retomar el proyecto, el documento
académico y el usuario confirmaron que la empresa opera solo en colones: no hay cobros, pagos ni
reportes en dólares, y mantener la conversión agregaba complejidad sin un caso de uso real (regla de
negocio 5 de `CLAUDE.md`).

## Decisión

- El sistema opera únicamente en colones (CRC). Se retira todo soporte multimoneda.
- Se eliminan del código: `CurrencyCode`, `ExchangeRate`, `DailyExchangeRate` (entidad, servicio,
  controlador y vistas), el precio de referencia en USD de `CatalogItem`
  (`ReferencePriceUsd`) y la moneda de las cuentas de efectivo (`LedgerAccount.Currency`).
- `Invoice` ya no tiene `Currency` ni `ConfirmedCrcPerUsd`; `Invoice.Confirm` deja de recibir una tasa.
- Las migraciones `RemoveCurrencySupport` y `RemoveCatalogItemUsdPrice` quitan las columnas, la tabla
  `TiposCambioDiarios` y sus restricciones; no hay script de conversión de datos porque no había tasas
  ni precios en USD reales que preservar.

## Consecuencias

- Se simplifican `Invoice`, `LedgerAccount` y `CatalogItem`: menos parámetros, menos validaciones
  cruzadas entre moneda y tasa.
- Las pruebas y vistas que dependían de la moneda se eliminaron o se reescribieron sin ese eje.
- Si la empresa necesitara operar en otra moneda en el futuro, se requeriría un nuevo ADR: este retiro
  no deja un mecanismo de conversión parcial ni campos ocultos reutilizables.
- `docs/02-requisitos-trazabilidad.md` marca como **Descartado** los RF/RN de tipo de cambio
  (`RF-MON-001`, `RF-MON-002`, `RN-MON-001` a `RN-MON-003`, `RNF-MON-001`, `RN-INV-004`), con nota de
  reemplazo, en vez de borrarlos de la matriz.
