# ADR-0059: Correcciones de cierre (paquete v9) y congelamiento del sistema

- **Fecha:** 2026-11-10
- **Estado:** Aceptado
- **Modifica:** ADR-0049 (permisos de Parámetros), ADR-0054 (rango del período fiscal) y ADR-0050
  (acceso del Asistente a traspasos, liquidaciones y pagos de renta).

## Contexto

El documento del TFG quedó en su versión final. El paquete v9
(`docs/prompt_claude_code_version_final_v9.md`) corrige los últimos puntos en que el código no hacía
exactamente lo que dice el documento, sin cambiar el modelo de datos ni agregar pantallas, rutas o
reportes. También quedaban sin documentar la estimación del impuesto sobre la renta (RF-99 de
`CLAUDE.md`) y los abonos del Asistente, y 22 pruebas de integración contra SQL Server fallaban.

## Decisión

1. **Fecha de negocio local.** Toda fecha de negocio sale de `TimeProvider.GetLocalNow()`. El ingreso
   de una factura de contado toma la fecha de la factura (antes, la fecha UTC de la confirmación, que
   desde las 6 p. m. ya era el día siguiente). Los controladores reciben el `TimeProvider` y los
   modelos de vista ya no usan `DateTime.Now`.
2. **Ingreso del abono = dinero recibido.** El ingreso se crea por `payment.Amount`, sin la retención
   (CU-12, paso 7). El saldo baja por monto + retención y el asiento no cambia.
3. **Flujo de efectivo desde los asientos.** Débitos y créditos de las cuentas de efectivo, sin
   asientos anulados ni sus reversiones; sin filtro de cuenta se excluyen los traspasos. El resultado
   y las exportaciones no cambian de forma.
4. **Retención de abonos anulados.** Deja de contar como ya retenida.
5. **IVA del período.** La columna de IVA de compras suma solo el IVA acreditable.
6. **Parámetros.** El límite de autorización y los datos de la empresa solo los cambia Gerencia:
   `UpdateCompany` usa `ManageAccountingPeriods` y `SystemParametersService` recibe
   `actorIsManagement`, rechaza la creación inicial y cualquier cambio del límite si el actor no es
   Gerencia. Finanzas sigue cambiando los días de anticipación.
7. **Asistente.** El listado y el detalle de traspasos, liquidaciones de IVA y pagos de renta exigen
   `ConfirmFinancialTransactions`.
8. **Período fiscal anterior.** `ClosingDateGuard` acepta fechas en [inicio − 12 meses, fin]; en el
   período anterior solo un mes abierto (el actual o uno reabierto).
9. **Grupos de cuentas contables.** Por decisión del usuario (10/11/2026), las cuentas nuevas se crean
   solo dentro de 1.1, 4.1, 4.2, 4.3, 5.1, 5.2 y 5.3, como dice `CLAUDE.md` (antes el código permitía
   2.1, 2.2 y 3.1 y no permitía 5.3).
10. **Pruebas SQL aisladas.** `SqlTestIsolation` neutraliza, dentro de la transacción revertida de
    cada prueba, las liquidaciones vigentes y las cuentas abiertas de la base de desarrollo; el reloj
    fijo `FixedLocalTimeProvider` cumple el papel de `FakeTimeProvider` sin agregar el paquete NuGet
    `Microsoft.Extensions.TimeProvider.Testing`.
11. **Congelamiento.** Después de este paquete solo se aceptan correcciones que no cambien lo que el
    documento describe (pantallas, campos, reglas, permisos, reportes, modelo de datos).

## Consecuencias

- Los reportes de ingresos y el listado de ingresos muestran el dinero que realmente entró; el flujo
  de efectivo cuadra con la balanza e incluye devoluciones de notas de crédito y pagos de renta.
- Los ingresos de abonos registrados antes de este cambio conservan el monto con retención: no se
  migran datos (la base de desarrollo se puede regenerar).
- Finanzas ve el límite y los datos de la empresa en solo lectura.
- Se mantienen dos significados de RF-99 (límite de crédito del cliente en `docs/02` y estimación de
  renta en `CLAUDE.md` y el código); la tabla de equivalencias de `docs/02` los distingue.
- Queda anotado, sin cambio de código: con la tarifa "No sujeto" inactiva, cambiar un borrador a un
  cliente "No sujeto" conserva las líneas con su tarifa anterior sin aviso (las líneas nuevas sí se
  bloquean). El usuario decidió dejarlo así.
- Suite: 417 pruebas unitarias y 430 de integración, todas aprobadas con SQL Server.
