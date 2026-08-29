# ADR-0005: Manejo multimoneda CRC/USD

- **Fecha:** 2026-08-22
- **Estado:** Aceptado

## Contexto

La empresa opera en colones y dólares, pero necesita una lógica administrativa comprensible y reportes
comparables.

## Decisión

- CRC es la moneda base.
- CRC y USD son las únicas monedas transaccionales.
- El tipo de cambio representa CRC por un USD.
- Cada documento contiene una sola moneda y conserva la tasa utilizada al confirmarse.
- Presupuestos y consolidaciones se expresan en CRC.
- En la primera versión, el cobro o pago utiliza la moneda del documento.

## Consecuencias

- Nunca se suman importes CRC y USD sin conversión explícita.
- Los reportes muestran valor original y equivalente histórico en CRC.
- Las tasas deben ser positivas, fechadas, trazables y no alterar documentos anteriores.
- Pagos cruzados, anticipos y revaluación quedan fuera hasta demostrar su necesidad.
- La política real de fuente, compra/venta y redondeo deberá validarse con la empresa.
