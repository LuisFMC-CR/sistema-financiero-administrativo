# Paquete final para Claude Code: correcciones de cierre (v9)

## Contexto

El documento del TFG quedó en su versión final. Este paquete corrige los últimos errores del sistema para que el código haga exactamente lo que dice el documento. Después de este paquete, el sistema queda **congelado**: solo se aceptan correcciones que no cambien nada de lo que el documento describe.

Trabaja sobre `main` (HEAD `9e8606d`). Haz un commit por bloque, en el orden indicado. Al terminar, ejecuta `dotnet build` y `dotnet test` (unitarias e integración contra SQL Server) y entrégame:

- el resultado de cada uno (total, aprobadas, fallidas);
- la lista de archivos cambiados por bloque;
- cualquier diferencia que encuentres entre este paquete y el código y que te haya obligado a decidir algo.

### Resumen de los 10 bloques

1. Fecha de negocio en hora local, no UTC.
2. El ingreso de un abono se registra por el monto recibido (sin la retención).
3. El flujo de efectivo se calcula desde los asientos, no desde una suma manual de movimientos.
4. La retención de un abono anulado deja de contar como "ya retenida".
5. El reporte "IVA del período" solo suma el IVA acreditable.
6. El límite de autorización y los datos de la empresa solo los cambia Gerencia.
7. Asistente pierde el acceso a traspasos, liquidaciones de IVA y pagos de renta.
8. El período fiscal admite operar en los meses aún abiertos del período anterior.
9. Diagnóstico y corrección de las 22 pruebas de integración que fallan.
10. `CLAUDE.md`, `README.md` y `docs/` quedan alineados con el documento final, y el sistema queda congelado.

### Restricciones (aplican a todos los bloques)

- **Sin cambios al modelo de datos:** no agregues migraciones, columnas, tablas, índices ni datos semilla.
- **Sin pantallas, rutas ni reportes nuevos.** Las columnas de los reportes existentes se mantienen.
- **Permisos:** no cambies ningún permiso, salvo lo que indican los bloques 6 y 7.
- **Textos de pantalla:** no cambies los existentes, salvo los mensajes nuevos que pide este paquete.
- **Códigos RF del código:** no renombres los RF que aparecen en comentarios, pruebas o `docs/`. La equivalencia con el documento va en el bloque 10.
- **Si una prueba existente contradice este paquete, corrige la prueba**, no la regla, y anótalo en el resumen.
- **Si algo del paquete no se puede hacer sin romper una restricción, detente y pregúntame.**

---

## Bloque 1. Fecha de negocio en hora local

**Problema.** Varias fechas de negocio salen de la hora UTC o de `DateTime.Now`, fuera del `TimeProvider`. Desde las 6 p. m. de Costa Rica la fecha UTC ya es la del día siguiente. Por eso un ingreso de contado confirmado de noche queda con fecha del día siguiente (y, el último día del mes, en el mes siguiente), y las cuentas vencidas se calculan con un día de adelanto.

**Cambios.**

1. `InvoiceService` (confirmación de factura de contado). El ingreso toma `invoice.IssueDate`, la misma fecha del asiento de la factura. Hoy toma `DateOnly.FromDateTime(confirmedAtUtc.UtcDateTime)`.
2. `PayableService` y `ReceivableService`. "Hoy" debe ser `DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime)`, no `GetUtcNow().UtcDateTime`.
3. Controladores con `DateTime.Now`: `VatSettlementsController`, `ReportsController` (4 lugares), `HomeController`, `JournalEntriesController` e `IncomeTaxPaymentsController`. Pasan a usar el `TimeProvider` inyectado (`GetLocalNow()`).
4. Modelos de vista con valores por defecto `DateOnly.FromDateTime(DateTime.Now)`:
   - **Lista:** `ExpenseViewModels`, `SupplierPaymentViewModels`, `ReceivablePaymentViewModels` (2), `CashTransferViewModels`, `IncomeTaxViewModels`, `IncomeViewModels` e `InvoiceViewModels`.
   - **Cambio:** quita el inicializador y asigna la fecha en la acción GET del controlador, con el `TimeProvider`.
5. Verifica que no quede ningún `DateTime.Now`, `DateTime.Today`, `DateTime.UtcNow` ni `GetUtcNow().UtcDateTime` para calcular una fecha de negocio. Los sellos de auditoría (`...AtUtc`) siguen en UTC.

**Pruebas.**

- Usa `FakeTimeProvider` con la zona `America/Costa_Rica` (en Windows, `Central America Standard Time`) y una hora local de 23:30 del último día de un mes. En esas condiciones:
  - una factura de contado confirmada genera un ingreso con la fecha de la factura y en ese mes;
  - una cuenta que vence ese día no aparece como vencida.

## Bloque 2. El ingreso de un abono es el dinero recibido

**Problema.** `ReceivablePaymentService` crea el ingreso del abono por `allocation.Total`, que es el monto recibido más la retención. La retención no entra al banco: el asiento la lleva a la cuenta 1.3.02. Por eso el reporte "Ingresos recibidos" y el listado de ingresos muestran más dinero del que entró a caja o banco.

El documento dice (CU-12, paso 7) que el sistema "registra el ingreso **por el monto recibido**". El diccionario de datos dice, en `Ingresos.Amount`: "Monto recibido en la cuenta de caja o banco; en los ingresos originados por abonos no incluye la retención."

**Cambios.**

1. El ingreso del abono se crea por `payment.Amount`, es decir, sin la retención.
2. El saldo de la cuenta por cobrar sigue rebajándose por el total (monto + retención).
3. El asiento no cambia.
4. Revisa todo uso de `Income.Amount` para confirmar que nada asume que incluye la retención: anulación del abono, nota de crédito, reportes y consultas.
5. No migres datos existentes; la base de desarrollo se puede regenerar.

**Pruebas.**

- Un abono de ₡98 000 con retención de ₡2 000 sobre una cuenta de ₡100 000 debe dar:
  - ingreso de ₡98 000;
  - saldo de la cuenta en ₡0;
  - asiento con débito a efectivo por 98 000, débito a 1.3.02 por 2 000 y crédito a cuentas por cobrar por 100 000.
- El reporte de ingresos del período suma ₡98 000.

## Bloque 3. Flujo de efectivo desde los asientos

**Problema.** `ReportService.GetCashFlowReportAsync` arma el flujo con ingresos, pagos, gastos de contado, traspasos y liquidaciones. Eso deja tres errores:

- incluye la retención (bloque 2);
- omite las devoluciones de las notas de crédito y los pagos del impuesto sobre la renta;
- no cuadra con el saldo de las cuentas de efectivo.

La regla del proyecto dice que el flujo sale **de los asientos**.

**Cambios.**

1. Calcula el flujo a partir de `AsientoLineas` de las cuentas de efectivo (`CashKind != null`) o solo de la cuenta filtrada:
   - **entradas:** débitos;
   - **salidas:** créditos;
   - **mes:** el de la fecha del asiento.
2. Excluye los asientos que tienen reversión y sus asientos de reversión. Como la reversión usa la fecha original, el par se compensa en el mismo mes.
3. Traspasos:
   - **sin filtro de cuenta:** excluye los asientos de traspaso (`SourceType = 8`);
   - **con filtro:** los débitos de un traspaso van a "traspasos recibidos" y los créditos a "traspasos enviados".
4. "Pagos de IVA" son los créditos a efectivo de asientos con `SourceType = 9`.
5. Conserva el `CashFlowReportResult`, las columnas, la pantalla, el PDF y el Excel. Solo cambia de dónde salen los montos:
   - total de entradas = todos los débitos considerados;
   - total de salidas = todos los créditos considerados.

**Pruebas.**

- En un mes con los siguientes movimientos, el neto debe ser igual a la variación del saldo de las cuentas de efectivo en la balanza de comprobación del mismo mes:
  - una factura de contado;
  - un abono con retención;
  - un gasto de contado;
  - un pago a proveedor;
  - una nota de crédito con devolución;
  - un traspaso;
  - una liquidación con pago;
  - un pago de renta;
  - una operación anulada.
- La misma igualdad debe cumplirse con filtro de cuenta.

## Bloque 4. Retención después de anular un abono

**Problema.** En `ReceivablePaymentService`, el cálculo de `alreadyWithheld` suma la base de todas las retenciones de la cuenta, incluidas las de abonos anulados. Después de anular un abono con retención, el sistema no deja registrar de nuevo esa retención.

**Cambio.** Suma solo las retenciones de abonos vigentes: agrega `payment.Status == ReceivablePaymentStatus.Active` a la condición.

**Prueba.** Registra un abono con retención sobre toda la base, anúlalo y registra otro abono con la misma retención. El segundo abono se acepta.

## Bloque 5. "IVA del período" muestra solo IVA acreditable

**Problema.** `GetVatPeriodReportAsync` suma en compras el IVA de todos los gastos confirmados, incluidos los marcados como no acreditables. Ese IVA es costo y no va a la cuenta 1.3.01. Por eso el reporte no coincide con la liquidación.

El documento (CU-22) dice que el reporte muestra "el IVA cobrado y **acreditable** por tarifa".

**Cambios.**

1. En la columna de IVA de compras, suma solo los gastos con `VatCreditable == true`.
2. La base de compras por tarifa sigue incluyendo todos los gastos confirmados.
3. No agregues columnas.

**Prueba.** En un mes sin registros tardíos, IVA cobrado neto − IVA acreditable del reporte = IVA por pagar − IVA acreditable de la liquidación de ese mes, antes del arrastre. El IVA de un gasto no acreditable no aparece en la columna de IVA acreditable.

## Bloque 6. El límite de autorización y los datos de la empresa solo los cambia Gerencia

**Problema.** `ManageSystemParameters` incluye a Finanzas y protege dos acciones:

- `Save`, que guarda juntos el límite y los días de alerta, por lo que Finanzas puede subir el límite que la restringe a ella misma;
- `UpdateCompany` (`POST datos-empresa`), con la que Finanzas puede cambiar la cédula jurídica, la sucursal o la terminal. Esos datos forman la clave y el consecutivo de los comprobantes y salen impresos en cada factura.

Según el documento (CU-06, Tabla 10 y diseño de entradas):

- **solo Gerencia:** el límite de autorización, los datos de la empresa, el período fiscal, la escala de renta y la reapertura o el cierre de meses;
- **Finanzas:** solo los días de anticipación; los demás parámetros los ve en solo lectura.

**Cambios.**

1. `UpdateCompany` pasa a `ManageAccountingPeriods` (solo Gerencia), igual que el período fiscal y la escala de renta.
2. En `SystemParametersService` (crear y actualizar), si el actor no es Gerencia:
   - rechaza cualquier cambio de `AuthorizationLimitCrc` con el mensaje "Solo Gerencia puede cambiar el límite de autorización.";
   - rechaza la creación inicial de los parámetros.
3. La pantalla muestra a Finanzas en solo lectura el límite, los datos de la empresa, el período fiscal y la escala. Solo los días de anticipación quedan editables para Finanzas.

**Pruebas (HTTP y de servicio).**

- Finanzas cambia los días: se acepta.
- Finanzas envía un límite distinto: se rechaza y el valor no cambia.
- Finanzas envía `POST datos-empresa`: recibe 403 y los datos no cambian.
- Gerencia cambia el límite y los datos de la empresa: se acepta.

## Bloque 7. Asistente no ve traspasos, liquidaciones ni pagos de renta

**Problema.** `CashTransfersController`, `VatSettlementsController` e `IncomeTaxPaymentsController` protegen el listado y el detalle con `ViewBusinessCatalogs`, que incluye al Asistente. El menú los oculta, pero la ruta responde.

En el documento (CU-24, CU-25 y CU-26), estos casos de uso son solo de Finanzas y Gerencia.

**Cambio.** Protege el listado y el detalle de los tres con `ConfirmFinancialTransactions` (Gerencia y Finanzas). Las acciones de registrar y anular no cambian.

**Prueba.** Asistente recibe 403 en `GET /traspasos`, `/liquidacion-iva` y `/pagos-renta`, y en sus detalles.

## Bloque 8. Período fiscal: meses abiertos del período anterior

**Problema.** El 1 de enero el sistema rechaza toda operación hasta que Gerencia cambie el período fiscal. Si Gerencia lo cambia antes, ya no se puede registrar ni corregir nada del período anterior, ni siquiera reabriendo diciembre.

El documento final (RF-55 y CU-06) dice: "fuera del cual no pueden registrarse operaciones, **salvo en los meses aún abiertos del período inmediatamente anterior**".

**Cambio.** En `ClosingDateGuard`, el rango permitido pasa a ser `[FiscalPeriodStartDate − 12 meses, FiscalPeriodEndDate]`.

- Una fecha dentro del período vigente sigue las reglas actuales.
- Una fecha del período anterior solo se acepta si su mes está abierto, ya sea porque es el mes actual o porque Gerencia lo reabrió. La lógica de mes cerrado que ya existe cubre esto.
- Mensaje para fechas fuera del rango: "No es posible operar con fecha dd/MM/yyyy: está fuera del período fiscal vigente y del período anterior."
- La regla de los pagos de renta (fecha no anterior al inicio del período al que se aplican) no cambia.

**Pruebas.** Con período 01/01/2027–31/12/2027 y fecha local del 05/01/2027:

- una operación del 20/12/2026 se rechaza (mes cerrado), y se acepta si Gerencia reabre diciembre de 2026;
- una del 15/11/2025 se rechaza;
- una del 05/01/2027 se acepta.

Con fecha local del 28/12/2026, período 2027 ya configurado y una operación del 28/12/2026: se acepta.

## Bloque 9. Pruebas de integración que fallan

`CLAUDE.md` registra 22 de 411 pruebas de integración contra SQL Server que fallaban antes de RF-99, 8 de ellas de la liquidación del IVA sin diagnosticar.

**Cambios.**

1. Antes de tocar nada, corre `dotnet test` y agrupa las 22 fallas por causa común (no las corrijas una por una a ciegas). Las causas habituales en este tipo de suite son:
   - datos que quedan de una prueba anterior porque no se limpia la base entre pruebas (acumulación en `SistemaFinanciero_Dev` o en la base de pruebas que use el pipeline);
   - fechas fijas en el código de la prueba (`new DateOnly(2024, ...)`) que ya quedaron fuera del período fiscal o de un mes abierto tras los bloques 1 y 8;
   - pruebas que dependen del orden de ejecución o comparten una cuenta/cliente/factura con otra prueba (falta de aislamiento);
   - pruebas de liquidación de IVA que aún asumen el IVA no acreditable en el cálculo (bloque 5) o la retención dentro del ingreso (bloque 2) — revísalas primero si tocan esas rutas.
2. Corrige la preparación de las pruebas (datos de arranque, fechas, aislamiento), no las reglas de negocio.
3. Si una falla revela un error real del sistema que no está cubierto por los bloques 1 a 8, corrígelo solo si la corrección no cambia nada de lo que describe el documento (pantallas, campos, reglas, permisos, reportes). Si cambiaría algo del documento, detente y pregúntame antes de tocar código o pruebas.
4. Entrega, por cada una de las 22 pruebas: su nombre, la causa real (no una conjetura) y si la corregiste en la prueba o en el código.

**Meta.** `dotnet test` en verde, sin pruebas omitidas.

## Bloque 10. Alinear `CLAUDE.md`, `README.md` y `docs/` con el documento final

1. **`CLAUDE.md`, congelamiento.** Reemplaza "si el documento y CLAUDE.md difieren, manda CLAUDE.md" por esta regla: "el documento del TFG está en su versión final; el sistema está congelado y solo admite correcciones que no cambien lo que el documento describe (pantallas, campos, reglas, permisos, reportes, modelo de datos)."
2. **`CLAUDE.md`, partes desactualizadas:**
   - **Categorías financieras:** quita las menciones (esquema `catalogos`, regla 13 "una categoría por cuenta de detalle", perfil de Finanzas y filtros).
   - **Catálogo base:** el de la regla 13 debe ser el actual (1 Activo … 1.3.03, 2 Pasivo, 3 Patrimonio, 4 Ingresos, 5 Gastos), tomado de la migración.
   - **Tarifas (regla 3):** las tarifas no se crean; solo se editan y se activan o inactivan.
   - **Presupuesto (regla 6):** el ejecutado es el **monto sin IVA** de los gastos confirmados (RF-40 del documento). Verifica que `BudgetService` y la alerta de `ExpenseService` usan `NetAmount`; no cambies el código.
   - **Pagos a proveedores:** no llevan retención (RF-15 del documento).
   - **Límite (regla 7):** cubre abonos, pagos, gastos de contado, traspasos, devoluciones de notas de crédito, pagos de liquidación de IVA y pagos de renta. Solo Gerencia lo cambia.
   - **Perfiles:** Gerencia define el límite, los datos de la empresa, el período fiscal y la escala de renta; Finanzas solo cambia los días de anticipación y consulta los demás parámetros.
   - **Período fiscal (regla 16):** agrega la excepción del bloque 8.
   - **Notas pendientes:** quita "El documento aún no refleja…" y la sección "Estado y orden de trabajo" desactualizada. Sustitúyela por un estado breve: versión final, número de pruebas y resultado.
3. **`docs/02-requisitos-trazabilidad.md`.** Agrega una tabla de equivalencias entre los códigos RF del repositorio y los del documento final (Anexo A). Haz la correspondencia por contenido, por ejemplo:
   - RF-99 del repositorio → RF-84 y RF-85 del documento;
   - liquidación de IVA → RF-83;
   - traspasos → RF-82;
   - nota de crédito → RF-80 y RF-81;
   - asientos → RF-78 y RF-79.
   
   Haz lo mismo con los casos de uso (Anexo B). Si un RF del repositorio no tiene equivalente, indícalo.
4. **`README.md`:** que refleje la versión final, sin cambiar su estructura.

---
## Anexo A. Requerimientos funcionales del documento final (RF-01 a RF-85)

Texto abreviado; el enunciado completo está en el documento. Cada requerimiento empieza con "El sistema debe".

- RF-01: permitir registrar cuentas contables de detalle dentro de los grupos del catálogo base habilitados para ello, indicando su nombre y la cuenta superior a la que…
- RF-02: validar que la cuenta superior esté activa, sea del mismo tipo que la nueva cuenta y no sea una cuenta de efectivo ni una cuenta de sistema, y que el código asignado no…
- RF-03: permitir modificar el nombre y la referencia de las cuentas de detalle, sin cambiar su código, su tipo ni su cuenta superior, e impedir la modificación y la inactivación…
- RF-04: representar la caja y las cuentas bancarias de la empresa como cuentas de efectivo, indicando si son caja o banco y su referencia bancaria opcional, para utilizarlas…
- RF-05: permitir activar e inactivar las cuentas de detalle sin eliminarlas, de modo que una cuenta inactiva conserve su historial y no pueda utilizarse en nuevas operaciones, e…
- RF-06: permitir consultar el catálogo de cuentas contables por código, nombre, tipo o estado.
- RF-07: generar automáticamente una cuenta por cobrar al confirmar una factura a crédito, con su cliente, monto y fecha de vencimiento.
- RF-08: permitir registrar abonos parciales o totales a una cuenta por cobrar, indicando la fecha, el monto, la cuenta de caja o banco que recibe el dinero y la referencia del…
- RF-09: permitir registrar, junto con un abono, la retención aplicada por el cliente, indicando el tipo de retención, el porcentaje y la base del cálculo, únicamente cuando el…
- RF-10: actualizar automáticamente el saldo y el estado de la cuenta por cobrar (pendiente, parcialmente pagada o pagada) al registrar o anular un abono, sin permitir que el…
- RF-11: permitir consultar las cuentas por cobrar por cliente, número de factura, estado, fecha de vencimiento o saldo, e identificar las cuentas vencidas y próximas a vencer.
- RF-12: permitir consultar el historial de abonos y retenciones de cada cuenta por cobrar, con el saldo resultante después de cada abono.
- RF-13: registrar como cuenta por pagar cada obligación con un proveedor al confirmar un gasto a crédito, con su monto y fecha de vencimiento.
- RF-14: permitir registrar pagos parciales o totales a una cuenta por pagar, indicando la fecha, el monto, el medio de pago y la cuenta de caja o banco de la que sale el dinero.
- RF-15: permitir aplicar un mismo pago a varias cuentas por pagar pendientes del mismo proveedor; los pagos a proveedores no llevan retenciones.
- RF-16: actualizar automáticamente el saldo y el estado de la cuenta por pagar (pendiente, parcialmente pagada o pagada) al registrar o anular un pago, sin permitir que el monto…
- RF-17: permitir consultar las cuentas por pagar por proveedor, estado, fecha de vencimiento o saldo, e identificar las cuentas vencidas y próximas a vencer.
- RF-18: permitir consultar el historial de pagos de cada proveedor y de cada cuenta por pagar.
- RF-19: registrar automáticamente un ingreso al confirmar una factura de contado o al registrar un abono, relacionándolo con la factura o la cuenta por cobrar que lo origina.
- RF-20: permitir registrar ingresos manuales por otras entradas de dinero de la operación que no provienen de facturas, indicando fecha, concepto, monto, cuenta de ingreso y…
- RF-21: asociar cada ingreso con una cuenta contable de ingreso y con la cuenta de caja o banco que recibe el dinero.
- RF-22: permitir registrar la referencia del movimiento bancario o del comprobante que respalda cada ingreso.
- RF-23: permitir consultar los ingresos por período, cuenta de ingreso, cuenta de caja o banco, origen (factura de contado, abono o registro manual), estado o monto.
- RF-24: mostrar, en el reporte de ingresos del período consultado, el total de los ingresos sin incluir los ingresos anulados.
- RF-25: permitir registrar los gastos por compras de bienes y servicios a proveedores, indicando fecha, concepto, proveedor, cuenta de gasto, monto sin IVA, tarifa de IVA, si el…
- RF-26: permitir seleccionar el plazo de pago del gasto, de contado o a crédito a 30, 60 o 90 días; si es de contado, debe registrar al confirmarlo la cuenta de caja o banco de…
- RF-27: permitir guardar un gasto como borrador y confirmarlo posteriormente, sin permitir modificaciones una vez confirmado.
- RF-28: asociar cada gasto con una cuenta contable de gasto y, cuando el IVA no sea acreditable, incluirlo como parte del costo del gasto.
- RF-29: comparar cada gasto que se confirma con el presupuesto disponible de su cuenta de gasto y período, y mostrar una alerta cuando el monto ejecutado supere el presupuesto…
- RF-30: permitir consultar los gastos por período, cuenta de gasto, proveedor, estado o monto.
- RF-31: permitir crear facturas internas en borrador indicando cliente, fecha y plazo de pago, de contado o a crédito a 30, 60 o 90 días, y calcular la fecha de vencimiento…
- RF-32: permitir agregar, modificar y eliminar líneas de la factura mientras se encuentre en borrador, indicando el producto o servicio del catálogo, la cantidad, el precio…
- RF-33: calcular automáticamente, por línea y para el total de la factura, el subtotal, los descuentos, el monto neto, el IVA y el monto total.
- RF-34: permitir confirmar una factura asignándole un número consecutivo interno y generando la cuenta por cobrar, si es a crédito, o el ingreso, si es de contado, para lo cual…
- RF-35: impedir la modificación de facturas confirmadas y permitir su anulación únicamente mediante una nota de crédito por el total de la factura, indicando el motivo.
- RF-36: permitir consultar las facturas por número, cliente, fecha, plazo de pago, estado o monto.
- RF-37: permitir visualizar y exportar a PDF el documento de cada factura confirmada o anulada, con los datos de la empresa y del cliente, el número, la clave y el consecutivo…
- RF-38: permitir registrar el presupuesto mensual de cada cuenta de gasto de último nivel, mostrar en las cuentas superiores la suma de sus cuentas hijas e impedir crear o…
- RF-39: permitir modificar los montos presupuestados de un período, conservando para cada cambio el monto anterior, el monto nuevo, el usuario y la fecha.
- RF-40: calcular el monto ejecutado de cada cuenta de gasto y período a partir del monto sin IVA de los gastos confirmados, sin incluir los borradores ni los gastos anulados.
- RF-41: mostrar, por cuenta de gasto y período, el monto presupuestado, el monto ejecutado, el monto disponible, el porcentaje utilizado y un indicador visual de ejecución.
- RF-42: permitir identificar las cuentas de gasto cuyo gasto ejecutado supera el presupuesto definido para el período, y mostrar en la pantalla de inicio la cantidad de…
- RF-43: incluir las tarifas del IVA que utiliza la empresa, cargadas en la puesta en marcha con su nombre, su porcentaje y su código de Hacienda, y asignarles un código de forma…
- RF-44: incluir el tipo de retención que aplican los clientes del sector público por obligación legal al pagar una factura, cargado en la puesta en marcha con su porcentaje y su…
- RF-45: permitir modificar los porcentajes, la base mínima de la retención y el estado de las tarifas de IVA y de los tipos de retención, sin alterar los montos de las…
- RF-46: calcular automáticamente el IVA de cada línea de factura y de cada gasto sobre el monto neto, después de aplicar los descuentos. En las facturas, la tarifa de cada línea…
- RF-47: calcular automáticamente el monto de cada retención registrada en un abono a partir de su base y su porcentaje.
- RF-48: conservar en cada operación la tarifa o el porcentaje, la base y el monto calculado del IVA o de la retención aplicados.
- RF-49: permitir consultar el IVA y las retenciones registrados por período, tarifa o tipo de retención y operación.
- RF-50: permitir registrar los datos maestros requeridos por la aplicación, como clientes, proveedores, productos y servicios, asignándoles un código de forma automática.
- RF-51: permitir modificar y consultar los datos maestros existentes.
- RF-52: permitir inactivar los datos maestros sin eliminarlos físicamente, de modo que conserven su historial, y no permitir identificaciones duplicadas.
- RF-53: permitir registrar en los datos maestros la información fiscal y de crédito necesaria para la facturación y el cobro, como la condición de IVA y el límite de crédito de…
- RF-54: incluir los datos iniciales necesarios para su puesta en marcha, como el catálogo base de cuentas contables, las tarifas de impuestos, los tipos de retención y los…
- RF-55: permitir definir los parámetros de operación: el límite de autorización para abonos y pagos, los días de anticipación para identificar las cuentas próximas a vencer, los…
- RF-56: impedir la eliminación y la modificación directa de los registros financieros confirmados, y permitir su anulación indicando el motivo, o su corrección mediante una nota…
- RF-57: permitir registrar ajustes de saldo en las cuentas por cobrar y por pagar, indicando el monto y el motivo, y conservando la trazabilidad de la operación original.
- RF-58: permitir consultar la información registrada en los diferentes módulos, como facturas, ingresos, gastos, cuentas por cobrar, cuentas por pagar y asientos contables…
- RF-59: mostrar en la pantalla de inicio la cantidad y el monto de las cuentas por cobrar y por pagar vencidas y próximas a vencer, según el parámetro de días de anticipación, y…
- RF-60: permitir consultar el detalle de una operación junto con sus registros relacionados, como la cuenta por cobrar, los abonos, las retenciones, los ingresos y el asiento…
- RF-61: permitir consultar el historial de una operación, con el usuario y la fecha de su creación, confirmación, anulación o ajuste, y el motivo cuando corresponda.
- RF-62: generar reportes de ingresos y gastos de un período, agrupados por cuenta contable, origen o proveedor, con totales expresados en colones.
- RF-63: generar reportes de presupuesto, que comparen lo presupuestado contra lo ejecutado por mes y por año, con el monto disponible y las variaciones.
- RF-64: generar reportes de cuentas por cobrar y por pagar, y el estado de cuenta de un cliente, con facturas, abonos, retenciones, pagos, saldos y fechas de vencimiento.
- RF-65: generar reportes contables, como el libro diario, el libro mayor, la balanza de comprobación y el estado de resultados, a partir de los asientos registrados.
- RF-66: generar reportes de flujo de efectivo, de IVA y retenciones del período y de la estimación del impuesto sobre la renta, para apoyar la declaración de impuestos.
- RF-67: generar un reporte comparativo mensual de ingresos y gastos para un rango de períodos seleccionado.
- RF-68: permitir consultar cada reporte en pantalla, filtrarlo por criterios como período, cuenta, cliente o proveedor, y exportarlo a formato PDF y Excel.
- RF-69: permitir iniciar sesión con correo electrónico y contraseña, y bloquear temporalmente la cuenta después de varios intentos fallidos consecutivos.
- RF-70: permitir cerrar la sesión de forma manual y cerrarla automáticamente después de un período de inactividad.
- RF-71: permitir al Administrador gestionar los usuarios, es decir, crearlos, modificarlos, activarlos y desactivarlos, y asignar a cada uno un perfil: Administrador, Gerencia…
- RF-72: permitir al Administrador restablecer la contraseña de un usuario mediante una contraseña temporal, que deberá cambiarse obligatoriamente en el siguiente inicio de…
- RF-73: permitir a cada usuario cambiar su propia contraseña.
- RF-74: restringir el acceso a los módulos y operaciones según el perfil del usuario, validando los permisos en el servidor en cada solicitud.
- RF-75: mostrar a cada usuario únicamente las opciones del menú que su perfil tiene permitidas.
- RF-76: permitir registrar abonos, pagos a proveedores, gastos de contado, traspasos, devoluciones de notas de crédito, pagos de la liquidación del IVA y pagos del impuesto…
- RF-77: registrar en una bitácora los eventos de seguridad y las acciones sensibles, como inicios de sesión, intentos fallidos, cambios de usuarios, confirmaciones, anulaciones…
- RF-78: generar de forma automática el asiento contable de partida doble de cada operación que se confirma, como facturas, abonos, ingresos, gastos, pagos a proveedores, ajustes…
- RF-79: generar, al anular o revertir una operación, un asiento de reversión con la fecha original y conservar el asiento inicial, sin eliminar ni modificar los asientos ya…
- RF-80: permitir a los usuarios con perfil de Finanzas y Gerencia registrar una nota de crédito por el total de una factura confirmada, indicando el motivo y la fecha, de modo…
- RF-81: revertir en la nota de crédito las retenciones aplicadas a la factura, generar su asiento contable, asignarle un código, una clave numérica y un consecutivo con el…
- RF-82: permitir a los usuarios con perfil de Finanzas y Gerencia registrar traspasos de dinero entre cuentas de efectivo de la empresa, como cajas y bancos, indicando la fecha…
- RF-83: permitir a los usuarios con perfil de Finanzas y Gerencia registrar la liquidación mensual del IVA, que compara el IVA por pagar con el IVA acreditable del mes y calcula…
- RF-84: calcular una estimación del impuesto sobre la renta de cada período fiscal a partir del estado de resultados, aplicando la escala de tramos vigente o, cuando la renta…
- RF-85: permitir a los usuarios con perfil de Finanzas y Gerencia registrar los pagos del impuesto sobre la renta a Hacienda, indicando el período fiscal al que se aplican, la…

## Anexo B. Casos de uso del documento final

Código | Caso de uso | Actores | Requerimientos

- CU-01 | Iniciar y cerrar sesión | Usuario | RF-69, RF-70
- CU-02 | Cambiar contraseña | Usuario | RF-72, RF-73
- CU-03 | Administrar usuarios | Administrador | RF-71, RF-72
- CU-04 | Gestionar clientes y proveedores | Asistente, Finanzas, Gerencia | RF-50 a RF-53
- CU-05 | Gestionar productos y servicios | Finanzas, Gerencia | RF-50 a RF-52, RF-54
- CU-06 | Definir parámetros de operación | Finanzas, Gerencia | RF-55, RF-84
- CU-07 | Gestionar cuentas contables | Finanzas, Gerencia | RF-01 a RF-06, RF-78, RF-79
- CU-08 | Gestionar tipos de impuesto y retención | Finanzas, Gerencia | RF-43 a RF-46, RF-49
- CU-09 | Registrar factura en borrador | Asistente, Finanzas, Gerencia | RF-31 a RF-33, RF-46
- CU-10 | Confirmar factura | Finanzas, Gerencia | RF-07, RF-19, RF-34, RF-48, RF-78
- CU-11 | Consultar y exportar facturas | Finanzas, Gerencia | RF-36, RF-37
- CU-12 | Registrar abono | Asistente, Finanzas, Gerencia | RF-08 a RF-10, RF-19, RF-47, RF-76, RF-78
- CU-13 | Registrar ingreso manual | Asistente, Finanzas, Gerencia | RF-20 a RF-22
- CU-14 | Registrar gasto en borrador | Asistente, Finanzas, Gerencia | RF-25, RF-27, RF-46
- CU-15 | Confirmar gasto | Finanzas, Gerencia | RF-13, RF-26, RF-28, RF-29, RF-48, RF-78
- CU-16 | Registrar pago a proveedor | Finanzas, Gerencia | RF-14 a RF-16, RF-76, RF-78
- CU-17 | Anular operación confirmada | Gerencia | RF-35, RF-56, RF-79
- CU-18 | Ajustar saldo de cuenta por cobrar o por pagar | Finanzas, Gerencia | RF-57, RF-78
- CU-19 | Registrar presupuesto mensual | Finanzas, Gerencia | RF-38, RF-39
- CU-20 | Consultar ejecución presupuestaria | Finanzas, Gerencia | RF-40 a RF-42
- CU-21 | Consultar operaciones y trazabilidad | Finanzas, Gerencia | RF-11, RF-12, RF-17, RF-18, RF-23, RF-24, RF-30, RF-58 a RF-61
- CU-22 | Generar y exportar reportes | Finanzas, Gerencia | RF-62 a RF-68
- CU-23 | Registrar nota de crédito | Finanzas, Gerencia | RF-35, RF-80, RF-81
- CU-24 | Registrar traspaso entre cuentas de efectivo | Finanzas, Gerencia | RF-82
- CU-25 | Registrar liquidación mensual del IVA | Finanzas, Gerencia | RF-83
- CU-26 | Registrar pago del impuesto sobre la renta | Finanzas, Gerencia | RF-84, RF-85
