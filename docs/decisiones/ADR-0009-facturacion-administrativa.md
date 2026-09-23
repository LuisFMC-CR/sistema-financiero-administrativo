# ADR-0009: Facturación administrativa y trazabilidad tributaria

- **Fecha:** 2026-09-06
- **Estado:** Aceptado

## Contexto

El sistema debe registrar facturas de productos y servicios para control administrativo interno en CRC
o USD, administrar cuentas por cobrar y permitir su visualización y exportación. No se solicitó emitir
comprobantes electrónicos ni integrarse con Hacienda de Costa Rica.

Las tarifas del IVA dependen del tipo de bien o servicio y pueden cambiar. El Ministerio de Hacienda
publica, entre otras, tarifas de 0 %, 0,5 %, 1 %, 2 %, 4 %, 13 %, exenta y 0 % sin derecho a crédito.
La aplicabilidad real de cada una depende de la actividad de la empresa y debe ser validada por su
asesoría contable.

## Decisión

- La factura será un documento administrativo interno; su exportación indicará claramente esa
  naturaleza y no generará clave, XML ni envío a Hacienda.
- El módulo administrará un catálogo de impuestos con tasas configurables. Se cargarán como referencia
  las tarifas de IVA publicadas por Hacienda, sin codificar sus valores dentro de las reglas de factura.
- Una línea puede conservar uno o más impuestos y copia su código, nombre, método, tarifa y monto al
  confirmarse. Las ediciones posteriores del catálogo no cambian documentos confirmados.
- La factura usa los estados `Draft`, `Confirmed` y `Cancelled`. Solo un borrador es editable.
- Una factura confirmada conserva el cliente, sus líneas, moneda, totales y, si está en USD, la tasa
  CRC por USD utilizada. Su saldo se calcula desde los cobros vigentes, en vez de ser un campo editable.
- El cobro utiliza la moneda de la factura y una cuenta financiera de la misma moneda. Antes de anular
  una factura se deben revertir sus cobros vigentes.

## Consecuencias

- La solución conserva trazabilidad suficiente para gestión y reportes internos, pero no sustituye un
  proveedor de facturación electrónica ni asesoría fiscal.
- Nuevos impuestos o cambios legales se actualizan en datos de configuración y no exigen modificar el
  motor de facturas, siempre que usen los métodos admitidos.
- Los impuestos especiales que dependan de partidas arancelarias, importaciones u otras condiciones
  externas no se calcularán automáticamente en esta primera versión; deberán configurarse y validarse
  por la empresa antes de usarse.

## Referencia

- [Tarifas del IVA, Ministerio de Hacienda de Costa Rica](https://www.hacienda.go.cr/docs/TarifasdelIVA.pdf)
- [Anexos y estructuras de comprobantes electrónicos v4.4, Ministerio de Hacienda](https://www.hacienda.go.cr/docs/ANEXOS_Y_ESTRUCTURAS_V4.4.pdf)
