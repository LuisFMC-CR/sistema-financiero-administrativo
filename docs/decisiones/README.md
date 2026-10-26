# Registro de decisiones arquitectónicas

Cada ADR conserva el contexto y las consecuencias de una decisión relevante. Un ADR aceptado no se
reescribe para ocultar el historial: una decisión posterior lo reemplaza mediante otro ADR.

| ADR | Decisión | Estado |
|---|---|---|
| [ADR-0001](ADR-0001-control-financiero-administrativo.md) | Control financiero-administrativo | Aceptado |
| [ADR-0002](ADR-0002-monolito-modular.md) | Monolito modular | Aceptado |
| [ADR-0003](ADR-0003-sql-server-ef-core.md) | SQL Server y EF Core | Aceptado |
| [ADR-0004](ADR-0004-identity-y-roles.md) | Identity y roles internos | Actualizado por ADR-0022 |
| [ADR-0005](ADR-0005-manejo-multimoneda.md) | Manejo CRC/USD | Reemplazado por ADR-0015 |
| [ADR-0006](ADR-0006-clientes-proveedores-separados.md) | Clientes y proveedores separados | Aceptado |
| [ADR-0007](ADR-0007-ciclo-vida-concurrencia-catalogos.md) | Ciclo de vida y concurrencia de catálogos | Aceptado |
| [ADR-0008](ADR-0008-administracion-interna-usuarios.md) | Administración interna de usuarios | Aceptado |
| [ADR-0009](ADR-0009-facturacion-administrativa.md) | Facturación administrativa y trazabilidad tributaria | Aceptado |
| [ADR-0010](ADR-0010-condicion-de-pago-redondeo-y-edicion-de-borradores.md) | Condición de pago, redondeo, base gravable y edición de borradores de factura | Actualizado por ADR-0020 |
| [ADR-0011](ADR-0011-catalogo-de-tipos-de-impuesto.md) | Catálogo de tipos de impuesto | Actualizado por ADR-0016 |
| [ADR-0012](ADR-0012-catalogo-de-cuentas-contables.md) | Catálogo de cuentas contables | Actualizado por ADR-0017 |
| [ADR-0013](ADR-0013-catalogo-de-tipos-de-retencion.md) | Catálogo de tipos de retención | Aceptado |
| [ADR-0014](ADR-0014-parametros-del-sistema.md) | Parámetros del sistema | Aceptado |
| [ADR-0015](ADR-0015-retiro-multimoneda.md) | Retiro del manejo multimoneda | Aceptado |
| [ADR-0016](ADR-0016-simplificacion-impuestos-retenciones.md) | Simplificación de impuestos y retenciones | Aceptado |
| [ADR-0017](ADR-0017-catalogo-base-protegido-cuentas-contables.md) | Catálogo base protegido de cuentas contables | Aceptado |
| [ADR-0018](ADR-0018-codigos-automaticos-por-secuencia.md) | Códigos automáticos por secuencia SQL | Aceptado |
| [ADR-0019](ADR-0019-numeracion-factura-al-confirmar.md) | Numeración de la factura al confirmar | Aceptado |
| [ADR-0020](ADR-0020-plazos-de-pago-fijos.md) | Plazos de pago fijos y vencimiento calculado | Aceptado |
| [ADR-0021](ADR-0021-servicio-y-pantallas-de-facturacion.md) | Servicio y pantallas de facturación (sin cuentas por cobrar ni ingresos) | Aceptado |
| [ADR-0022](ADR-0022-ampliacion-de-acceso-de-gerencia.md) | Ampliación del acceso de Gerencia a lo operativo financiero | Aceptado |
| [ADR-0023](ADR-0023-cuentas-por-cobrar-ingresos-y-bitacora.md) | Cuentas por cobrar, ingresos y bitácora de operaciones | Aceptado |
| [ADR-0024](ADR-0024-abonos-y-retenciones.md) | Abonos a varias facturas y retenciones | Aceptado |
| [ADR-0025](ADR-0025-ingreso-manual.md) | Ingreso manual | Aceptado |
| [ADR-0026](ADR-0026-pantallas-de-cuentas-por-cobrar-abonos-e-ingresos.md) | Pantallas de cuentas por cobrar, abonos e ingresos | Aceptado |
| [ADR-0027](ADR-0027-gastos-cuentas-por-pagar-y-codigo-de-ingresos.md) | Gastos, cuentas por pagar y código automático de ingresos | Aceptado |
| [ADR-0028](ADR-0028-pagos-a-proveedores.md) | Pagos a proveedores | Aceptado |
| [ADR-0029](ADR-0029-pantallas-de-gastos-cuentas-por-pagar-y-pagos.md) | Pantallas de gastos, cuentas por pagar y pagos | Aceptado |
| [ADR-0030](ADR-0030-presupuesto-mensual.md) | Presupuesto mensual por categoría de gasto | Aceptado |
| [ADR-0031](ADR-0031-pantalla-de-presupuesto-y-alerta-al-confirmar-un-gasto.md) | Pantalla de presupuesto y alerta al confirmar un gasto | Aceptado |
| [ADR-0032](ADR-0032-indicador-de-cuentas-vencidas.md) | Indicador de cuentas vencidas | Aceptado |
| [ADR-0033](ADR-0033-pantalla-de-consultas.md) | Pantalla de Consultas | Aceptado |
| [ADR-0034](ADR-0034-infraestructura-de-reportes-ingresos-y-gastos.md) | Infraestructura de reportes, y reportes de ingresos y de gastos por categoría | Aceptado |
| [ADR-0035](ADR-0035-reportes-presupuesto-y-comparativo-mensual.md) | Reportes de presupuesto contra lo ejecutado y comparativo mensual | Aceptado |
| [ADR-0036](ADR-0036-reportes-estado-de-cuenta-y-cuentas-por-pagar-por-proveedor.md) | Reportes de estado de cuenta de cliente y cuentas por pagar por proveedor | Aceptado |
| [ADR-0037](ADR-0037-ajustes-manuales-de-saldo.md) | Ajustes manuales de saldo | Aceptado |
| [ADR-0038](ADR-0038-fecha-de-cierre.md) | Parámetro de fecha de cierre | Aceptado |
| [ADR-0039](ADR-0039-validacion-de-fecha-de-cierre.md) | Validación de la fecha de cierre en las operaciones existentes | Aceptado |
| [ADR-0040](ADR-0040-pantalla-de-perfiles-y-permisos.md) | Pantalla de perfiles y permisos | Aceptado |
| [ADR-0041](ADR-0041-datos-de-la-empresa-y-factura-en-pdf.md) | Datos de la empresa y factura interna en PDF | Aceptado |
