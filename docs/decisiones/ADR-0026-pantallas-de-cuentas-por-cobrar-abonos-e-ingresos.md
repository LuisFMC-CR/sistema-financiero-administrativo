# ADR-0026: Pantallas de cuentas por cobrar, abonos e ingresos

- **Fecha:** 2026-10-24
- **Estado:** Aceptado

## Contexto

Cierra el paso 2 de `CLAUDE.md`. Los bloques anteriores (ADR-0023 a ADR-0025) dejaron listos
`IReceivableService`, `IReceivablePaymentService` e `IIncomeService`; faltaba construir las pantallas
sobre ellos: Cuentas por Cobrar, registro de abonos (con reparto a varias facturas y retención) e
Ingresos.

## Decisión

- **El registro de un abono es un asistente de dos pasos**, no un formulario único.
  `ReceivablePaymentsController.Start` (GET) pide cliente, monto total, cuenta de efectivo, categoría,
  referencia y fecha; `Start` (POST) consulta `IReceivableService.GetPendingByCustomerAsync` y
  propone el reparto de la más antigua a la más nueva (regla 2) repartiendo el monto entre los saldos
  pendientes; el resultado se muestra editable (monto y retención por línea) antes de que
  `Register` lo registre de verdad. Si el registro falla, se regresa al paso 1 con el cliente
  preseleccionado en vez de reconstruir el paso 2 con los datos ya editados: mantiene el controlador
  simple a cambio de que el usuario deba volver a repartir si algo falla (caso esperado poco
  frecuente: conflicto de concurrencia o límite de autorización).
- **La base de la retención se propone como el neto de la factura** (regla 3), por lo que
  `ReceivableModel` ganó el campo `InvoiceNetAmount` (antes no existía: bastaba con el monto total).
- **Cuentas por Cobrar e Ingresos son pantallas de solo Gerencia y Finanzas** (política
  `Reportes.Ver`): Asistente "no ve... saldos" (perfiles). Registrar un abono o un ingreso manual usa
  `Movimientos.Confirmar`; anular cualquiera de los dos usa `Movimientos.Anular`, igual que el resto
  del sistema.
- **La ruta de registro no se llama "registrar".** La prueba de seguridad
  `UserAdministrationAuthorizationTests.Endpoints_DoNotOfferRegistrationOrPasswordRecovery` rechaza
  cualquier ruta del sistema que contenga "registr" (para impedir un registro público de cuentas, RF
  fuera de alcance). Coincide por casualidad con "registrar un abono", así que la ruta final es
  `/cuentas-por-cobrar/abonos/aplicar`.
- **`IReceivableService.SearchAsync` e `IIncomeService.SearchAsync`**, diferidos en ADR-0023/ADR-0024
  hasta que existiera la pantalla que los necesitara, se agregan ahora junto con sus pantallas.

## Consecuencias

- Controladores nuevos: `ReceivablesController` (`/cuentas-por-cobrar`), `ReceivablePaymentsController`
  (`/cuentas-por-cobrar/abonos`), `IncomesController` (`/ingresos`).
- `docs/02-requisitos-trazabilidad.md` agrega RF-INV-011 y amplía RF-INV-006, RF-INV-009 y RF-INV-010
  con sus pantallas.
- Verificación: `ReceivablesAuthorizationTests` e `IncomesAuthorizationTests` (sin SQL Server) exigen
  exactamente las políticas de la matriz de perfiles en cada ruta, siguiendo el mismo patrón que
  `InvoiceAuthorizationTests`. No se agregaron pruebas de controlador con el patrón de "servicio
  grabador" (`WithholdingTypesControllerTests`) porque el repositorio no lo usa para controladores de
  documentos financieros (`InvoicesController` tampoco lo tiene): la cobertura de ese nivel ya la dan
  las pruebas de servicio contra SQL Server real.
- Verificación manual: se confirmó que la aplicación arranca, que las rutas nuevas exigen inicio de
  sesión y que el modelo de EF Core sigue siendo válido en tiempo de ejecución (formulario de inicio
  de sesión real contra la base de datos). No se hizo un recorrido completo en el navegador con una
  cuenta de Gerencia o Finanzas real: el entorno de este asistente no tiene esas credenciales y
  modificarlas en la base de datos de desarrollo habría alterado cuentas reales, algo que no se debe
  hacer sin permiso explícito. Queda pendiente que el usuario pruebe el asistente de abonos y el
  ingreso manual en el navegador con sus propias credenciales.
