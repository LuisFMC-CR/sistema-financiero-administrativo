# ADR-0011: Catálogo de tipos de impuesto

- **Fecha:** 2026-09-26
- **Estado:** Aceptado
- **Complementa:** [ADR-0009](ADR-0009-facturacion-administrativa.md) y
  [ADR-0010](ADR-0010-condicion-de-pago-redondeo-y-edicion-de-borradores.md)

## Contexto

El ADR-0009 decidió que la facturación administrará un catálogo de impuestos con tarifas configurables,
cargadas como referencia desde las tarifas publicadas por el Ministerio de Hacienda, y sin codificar
sus valores en las reglas de la factura. El ADR-0010 dejó a la línea de factura calculando cada
impuesto a partir de una `InvoiceTaxSpecification`, a la espera de un catálogo que la produjera.

## Decisión

### Catálogo

- Se crea `TaxType`, un catálogo plano con código único, nombre, método de cálculo, tarifa, descripción
  opcional, baja lógica, auditoría y control de concurrencia, siguiendo el patrón de los demás
  catálogos.
- Los métodos son los que ya soporta la línea de factura: porcentaje sobre el neto de la línea y monto
  fijo por unidad.
- La tarifa no puede ser negativa y conserva cuatro decimales. Un porcentaje no puede superar 100 %; el
  monto fijo no tiene ese tope. La regla vive en el dominio y se repite en la base con
  `CK_TiposImpuesto_Rate`.
- Un impuesto exento o de tarifa 0 % es un tipo con tarifa cero, diferenciado por su código y nombre.
  La distinción entre exento y 0 % con o sin derecho a crédito es informativa para la empresa: el
  sistema no calcula créditos fiscales.
- **El método de cálculo es inmutable** después de crear el tipo, igual que el tipo y la moneda de las
  cuentas contables de efectivo: cambiarlo altera el significado de la tarifa. Código, nombre, tarifa y
  descripción sí pueden modificarse. El servidor lee siempre el método del registro guardado y nunca
  del formulario de edición.
- Los tipos se desactivan y reactivan; no se eliminan.

### Permisos

- Consultan Gerencia, Finanzas y Asistente, que necesita seleccionarlos al preparar borradores.
  Solo Finanzas crea, modifica, activa, desactiva y carga las tarifas de referencia. Se reutilizan las
  políticas existentes `ViewBusinessCatalogs` y `ManageBusinessCatalogs`.

### Tarifas de referencia

- `ReferenceTaxTypes` conserva, como datos de la capa de aplicación, las tarifas de IVA que menciona el
  ADR-0009: 13 %, 4 %, 2 %, 1 %, 0,5 %, 0 %, exento y 0 % sin derecho a crédito. Están separadas de las
  reglas de la factura.
- Finanzas las carga con un botón explícito (`POST`, con confirmación). El servicio crea únicamente las
  que falten, identificadas por su código, y no modifica ni reactiva las existentes. Quedan a nombre
  de quien las cargó.
- Cada descripción indica que la tarifa es de referencia y que su uso debe validarse con la asesoría
  contable, porque la aplicabilidad depende de la actividad de la empresa.
- Se descartaron dos alternativas: sembrarlas en una migración, porque las tablas exigen un usuario
  creador que aún no existe al migrar; y hacerlo al arrancar el sistema, porque añade un
  comportamiento automático y sin auditoría de quién lo decidió.

### Relación con las facturas

- `InvoiceTaxSpecification` e `InvoiceLineTax` incorporan `TaxTypeId`, opcional, con clave foránea
  restrictiva a `catalogos.TiposImpuesto`.
- La línea sigue conservando su propia copia de código, nombre, método, tarifa, base y monto. El enlace
  solo registra el origen. Modificar o desactivar un tipo no cambia las facturas existentes, y la base
  impide eliminar un tipo que una línea ya utilizó.

## Consecuencias

- Dos migraciones: `AddTaxTypes` (tabla `catalogos.TiposImpuesto`) y `AddInvoiceLineTaxTaxTypeId`
  (columna, índice y clave foránea).
- Un impuesto no puede aplicarse a una línea ya guardada sin reemplazarla, tal como establece el
  ADR-0010; el catálogo produce las especificaciones, pero el selector de impuestos en la pantalla de
  facturas se construye con el servicio de facturación.
- El servicio expone `GetActiveOptionsAsync`, que devuelve los tipos activos con su método y tarifa para
  ese selector.
- Las tarifas de referencia deben revisarse cuando la normativa cambie: se actualizan editando los
  tipos, sin modificar reglas de la factura.
- No se guarda historial de los cambios de tarifa: cada tipo conserva solo el usuario y el instante de
  su última modificación. Las facturas ya emitidas no lo necesitan, porque conservan su fotografía. Si
  se requiriera auditar cada cambio, se registraría en la bitácora.
- Las pruebas cubren el dominio, la lista de referencia, el controlador, la autorización, la validación
  de formularios, el modelo de persistencia y tres escenarios contra SQL Server.
