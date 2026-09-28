# Visión, alcance y glosario

## Visión

Centralizar los procesos financieros administrativos de Soporte Experto en una aplicación web interna,
trazable y accesible según las responsabilidades de cada usuario.

## Alcance confirmado

- Una empresa y una sede.
- Operaciones transaccionales en CRC y USD; CRC es la moneda base de control.
- Catálogos separados de clientes y proveedores.
- Catálogo único de productos y servicios. Una factura puede contener ambos.
- Productos facturables sin control de inventario.
- Precios de referencia opcionales en CRC y USD para productos y servicios.
- Categorías financieras jerárquicas de ingreso y gasto, cada una vinculada a una cuenta contable de su
  mismo tipo.
- Catálogo de cuentas contables jerárquico (Activo, Pasivo, Patrimonio, Ingreso y Gasto) que solo
  clasifica. Las cajas y los bancos son cuentas de Activo «de efectivo», con subtipo y moneda fijos,
  sin saldo editable.
- Registro manual de una tasa CRC por USD para cada fecha de negocio.
- Facturación administrativa interna de contado y crédito.
- Cuentas por cobrar, abonos e ingresos de efectivo.
- Gastos de contado y crédito, cuentas por pagar y pagos.
- Presupuestos en CRC por período y categoría.
- Impuestos y retenciones parametrizables, sujetos a validación con la empresa.
- Consultas, reportes administrativos, seguridad y bitácora.
- Roles: Administrador, Gerencia, Finanzas y Asistente.
- Los movimientos confirmados no se eliminan físicamente.

## Exclusiones explícitas

- Partida doble, asientos, diario, mayor y cierres contables.
- Estados financieros o declaraciones fiscales oficiales.
- Emisión, firma o transmisión de comprobantes ante Hacienda.
- Inventario, bodegas, costos, lotes y existencias.
- Conciliación bancaria.
- Nómina, aplicación móvil y despliegue en nube.
- Anticipos, saldos a favor y sobrepagos en la primera versión.

## Supuestos pendientes de validar con la empresa

- Fuente empresarial definitiva del tipo de cambio; mientras se valida, la fuente se registra como
  texto obligatorio junto con cada tasa manual.
- Necesidad de cobros o pagos en moneda distinta a la del documento.
- Impuestos, retenciones, redondeos y precios con o sin impuesto incluido.
- Notas de crédito/débito o solamente anulaciones completas.
- Gastos sin proveedor, como caja chica.
- Aprobaciones de gastos según monto.
- Fecha de corte y saldos iniciales que deberán migrarse.

## Glosario inicial

| Término | Definición dentro del sistema |
|---|---|
| Factura interna | Documento administrativo de venta; no es un comprobante electrónico oficial. |
| Facturado | Valor de los productos o servicios incluidos en facturas confirmadas. |
| Cuenta por cobrar | Saldo pendiente originado por una factura de crédito. |
| Cobro o abono | Entrada efectiva de dinero aplicada a una o varias facturas. |
| Ingreso de efectivo | Dinero recibido; se reporta separadamente de lo facturado. |
| Gasto | Documento que reconoce administrativamente una compra o erogación. |
| Cuenta por pagar | Saldo pendiente originado por un gasto de crédito. |
| Pago | Salida efectiva de dinero aplicada a una o varias obligaciones. |
| Cliente | Persona física o jurídica a la que se factura. Se administra separadamente de proveedores. |
| Proveedor | Persona física o jurídica asociada a gastos. Se administra separadamente de clientes. |
| Producto o servicio | Elemento facturable del catálogo único. Su tipo no implica control de inventario. |
| Precio de referencia | Importe opcional en CRC o USD que facilita una captura futura; no es un tipo de cambio ni modifica documentos históricos. |
| Cuenta contable | Elemento del catálogo jerárquico de clasificación, de tipo Activo, Pasivo, Patrimonio, Ingreso o Gasto. Solo clasifica: el sistema no lleva partida doble, asientos ni saldos contables. |
| Cuenta de efectivo | Cuenta contable de tipo Activo marcada como caja o banco, con moneda fija; es la que recibe o entrega dinero en ingresos y pagos. |
| Categoría financiera | Clasificación jerárquica de ingreso o gasto vinculada a una cuenta contable de su mismo tipo; no representa una cuenta de partida doble. |
| Tipo de impuesto | Impuesto con tarifa configurable, porcentual o de monto fijo por unidad, que una línea de factura aplica y conserva como fotografía. |
| Tipo de retención | Retención con tarifa porcentual configurable, que se aplica al registrar un abono o un pago. |
| Parámetros del sistema | Configuración única con el límite de autorización en CRC y los días de alerta de vencimiento; la define Gerencia. |
| Moneda base | CRC, utilizada para presupuestos y consolidación de reportes. |
| Tipo de cambio diario | Cantidad de CRC equivalente a un USD para una fecha de negocio y fuente determinadas. |
| Activo/Inactivo | Estado lógico de un registro maestro. Un registro inactivo se conserva para trazabilidad y deja de ofrecerse en nuevas operaciones. |
| Concurrencia optimista | Control que detecta si otro usuario modificó un registro antes de guardar y evita sobrescribirlo silenciosamente. |
| Confirmar | Convertir un borrador revisable en un movimiento inmutable con efectos financieros. |
| Anular | Invalidar un movimiento confirmado conservando original, motivo, fecha y usuario. |
