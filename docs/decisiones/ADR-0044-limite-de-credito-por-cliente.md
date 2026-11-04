# ADR-0044: Límite de crédito propio por cliente (alerta informativa, nunca bloquea)

- **Fecha:** 2026-11-03
- **Estado:** Aceptado

## Contexto

El usuario pidió un límite de crédito para clientes, por analogía con el límite de autorización de
abonos y pagos (`SystemParameters.AuthorizationLimitCrc`). Se preguntó explícitamente el alcance y el
comportamiento, y la respuesta fue la opción más simple de las dos presentadas en cada caso:

- **Nivel**: un límite propio por cliente (no un parámetro global compartido por todos).
- **Comportamiento al superarlo**: solo una alerta informativa al confirmar la factura a crédito que
  lo deja por encima; nunca bloquea la confirmación, ni siquiera para Gerencia.

Esto es deliberadamente más simple que el límite de autorización: no hay excepción de Gerencia ni
bloqueo alguno, porque el usuario lo pidió así de forma explícita.

## Decisión

- **`Customer.CreditLimit`** (`decimal?`, Domain): nulo significa que el cliente no tiene límite
  configurado. Cuando tiene un valor, se redondea a 2 decimales y debe ser `>= 0` (0 es válido, mismo
  criterio que el presupuesto en 0, regla 6). Se valida en el constructor y en `UpdateDetails`, con el
  mismo patrón que `SystemParameters.RequireNonNegativeLimit`.
- **Columna `catalogos.Clientes.CreditLimit`**: `decimal(18,2)` nulo, con
  `CK_Clientes_CreditLimit` (`NULL OR >= 0`). Migración `AddCustomerCreditLimit`.
- **Alcance de la interfaz**: el campo se agregó solo a `CustomerInputViewModel` (no a la clase base
  `BusinessContactInputViewModel`), porque es exclusivo de clientes; `SupplierInputViewModel` no lo
  hereda y los proveedores no lo tienen.
- **`IInvoiceService.CheckCustomerCreditLimitAsync(customerId, invoiceTotalAmount)`** (nuevo) →
  `CustomerCreditLimitCheck(HasLimit, CreditLimit, OutstandingBalanceBeforeThisInvoice, ExceedsLimit)`,
  mismo patrón que `ExpenseBudgetCheck`/`CheckBudgetAsync` del presupuesto. `InvoiceService` consulta
  directamente `Receivable.Balance` (estados Pendiente o Parcialmente pagada) del cliente, sin pasar
  por `IReceivableService`, igual criterio que `ExpenseService` consulta el presupuesto directamente.
- **Solo aplica a crédito**: a diferencia de la alerta de presupuesto (que `ExpensesController` llama
  sin condición, de contado o crédito), aquí la comprobación solo tiene sentido para una factura a
  crédito, porque una de contado nunca genera cuenta por cobrar. `InvoicesController.Confirm` la llama
  únicamente cuando `!model.IsCash`, en el `GET` y en las dos ramas de fallo del `POST`.
- **Nunca bloquea**: `ConfirmAsync` no cambia; la alerta es puramente informativa en
  `Views/Invoices/Confirm.cshtml` (tres estados: sin límite configurado, dentro del límite, o lo
  supera), el mismo criterio visual que la alerta de presupuesto de gastos.
- **Se recalcula en cada confirmación**: como la comprobación suma el saldo vigente de
  `Receivable.Balance` en el momento de confirmar, automáticamente queda correcta ante abonos,
  anulaciones y ajustes de saldo anteriores, sin necesitar ningún enlace adicional con esos flujos.

## Consecuencias

- Nuevo: migración `AddCustomerCreditLimit`.
- Modificado: `Customer.cs` (Domain); `CustomerConfiguration.cs`; `CustomerCatalogContracts.cs`
  (`SaveCustomerCommand.CreditLimit`, `CustomerModel.CreditLimit`); `CustomerCatalogService.cs`;
  `BusinessContactViewModels.cs` (`CustomerInputViewModel.CreditLimit`); `CustomersController.cs`
  (`ToCommand` ahora tipado a `CustomerInputViewModel`); `Views/Customers/_Form.cshtml`;
  `InvoiceContracts.cs` (`CustomerCreditLimitCheck`, `IInvoiceService.CheckCustomerCreditLimitAsync`);
  `InvoiceService.cs`; `InvoiceViewModels.cs` (`InvoiceConfirmViewModel`); `InvoicesController.cs`
  (acción `Confirm`); `Views/Invoices/Confirm.cshtml`.
- `docs/02-requisitos-trazabilidad.md` agrega RF-99.
- Verificación: 2 pruebas unitarias de dominio nuevas (rechaza límite negativo; permite 0 y volver a
  nulo) y 2 pruebas SQL reales nuevas (sin límite configurado nunca excede; suma el saldo pendiente de
  varias facturas a crédito y no bloquea la confirmación aunque lo supere). 331 pruebas unitarias y
  375 de integración pasan.
- Verificación manual: con Gerencia, se configuró un límite de ₡150 a un cliente de prueba, se le
  confirmó una factura a crédito de ₡40 000 y la alerta de advertencia apareció y permitió confirmar
  igual; la factura quedó Confirmada con su cuenta por cobrar.
