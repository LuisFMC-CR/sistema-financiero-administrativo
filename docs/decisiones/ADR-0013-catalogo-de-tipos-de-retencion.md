# ADR-0013: Catálogo de tipos de retención

- **Fecha:** 2026-09-27
- **Estado:** Aceptado
- **Complementa:** [ADR-0011](ADR-0011-catalogo-de-tipos-de-impuesto.md)

## Contexto

La regla de negocio 3 pide un catálogo de tipos de retención con tarifas configurables, análogo al de
tipos de impuesto: «Una retención se registra al aplicar un abono o un pago: tipo, porcentaje, base (por
defecto el neto sin impuestos del documento, editable) y monto. La retención también rebaja el saldo.»
A diferencia de un impuesto, la regla nombra explícitamente «porcentaje» y no «método»: una retención no
tiene la variante de monto fijo por unidad que sí existe para los impuestos.

Los abonos y los pagos, donde se aplicará una retención, todavía no existen (son el paso 2 de los
pendientes en `CLAUDE.md`). Este ADR cubre solo el catálogo.

## Decisión

- Se crea `WithholdingType` (`catalogos.TiposRetencion`) con código único, nombre, tarifa porcentual (0 a
  100, cuatro decimales), descripción opcional, baja lógica, auditoría y control de concurrencia. Sigue el
  mismo patrón que `TaxType`, pero sin un campo de método de cálculo: como siempre es porcentual, la
  tarifa se valida directamente contra el rango 0 a 100, en el dominio y con `CK_TiposRetencion_Rate` en
  la base.
- La base y el monto de una retención concreta no viven en este catálogo: son datos del abono o del pago
  al que se aplique, según la regla de negocio 3. El catálogo solo aporta el tipo y su tarifa vigente al
  momento de aplicarla; ese registro se construirá junto con los abonos y pagos.
- No se cargan tarifas de retención de referencia. A diferencia del IVA en el ADR-0009, ninguna regla
  aprobada pide precargar valores de Hacienda para las retenciones, así que no se agrega esa función.
- Consultan Gerencia, Finanzas y Asistente; solo Finanzas modifica. Mismas políticas que los demás
  catálogos financieros.

### Menú: nombre del módulo «Impuestos y Retenciones»

`CLAUDE.md` pide usar los nombres de los módulos de la Tabla 4 «en el menú y en las pantallas». El menú
agrupa ahora «Tipos de impuesto» y «Tipos de retención» bajo el encabezado **Impuestos y Retenciones**,
en vez de dejarlos sueltos bajo «Catálogos». Ese mismo encabezado agrupará, cuando se construyan, la
aplicación de retenciones a abonos y pagos.

## Alternativas consideradas

- **Reutilizar `TaxType` con un método de cálculo restringido a porcentaje.** Se descartó: mezclaría dos
  catálogos de dominio distinto (impuestos de factura y retenciones de cobro) en una sola entidad, y la
  regla de negocio los describe como catálogos separados.
- **Cargar tarifas de retención de referencia, como se hizo con el IVA.** Se descartó por ahora: ninguna
  regla aprobada las pide, y agregar valores sin ese respaldo sería inventar alcance.
- **Dejar «Tipos de impuesto» y «Tipos de retención» sueltos en el menú.** Se descartó: el nombre del
  módulo «Impuestos y Retenciones» de la Tabla 4 no aparecería en ninguna pantalla.

## Consecuencias

- Una migración: `AddWithholdingTypes`, sin datos que migrar.
- El catálogo queda desconectado de cualquier documento hasta que existan los abonos y los pagos. Su
  `WithholdingTypeOption` ya está listo para el selector que se construya entonces.
- RN-RET-002 (que modificar o desactivar un tipo no altera abonos o pagos existentes) queda «Propuesto»
  en la trazabilidad: no puede verificarse hasta que exista esa fotografía histórica.
- Las pruebas cubren el dominio, el controlador, la autorización, la validación de formularios, el
  modelo de persistencia y un escenario contra SQL Server.
