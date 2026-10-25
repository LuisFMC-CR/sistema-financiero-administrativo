# ADR-0024: Abonos a varias facturas y retenciones

- **Fecha:** 2026-10-24
- **Estado:** Aceptado

## Contexto

Continúa el paso 2 de `CLAUDE.md` ("facturación, cuentas por cobrar, abonos... e ingresos"), después
de `Receivable`/`Income` (ADR-0023). Este bloque agrega `ReceivablePayment` (abono) y `Withholding`
(retención), incluido el reparto de un abono a varias facturas del mismo cliente con un `GroupId`
común (regla de negocio 2) y la retención por línea (regla 3).

## Decisión

- **Una fila de `ReceivablePayment` por factura, no una por abono.** Un abono a varias facturas crea
  tantas filas como facturas, todas con el mismo `GroupId`, la misma referencia y la misma cuenta de
  efectivo. Esto es lo que permite "cada uno se anula de forma individual" (regla 2) sin arrastrar a
  los demás: anular una fila del grupo no toca las otras.
- **`Withholding` es una fotografía inmutable**, igual que `InvoiceLineTax` con el IVA: copia código,
  nombre y tarifa del `WithholdingType` al momento del abono. Es 0..1 por `ReceivablePayment` (FK
  única); no tiene estado propio porque su vigencia depende enteramente de la del abono que la
  contiene.
- **El ingreso de un abono también exige categoría**, igual que el de una factura de contado
  (ADR-0023), aunque la regla de negocio 2 no la liste entre los datos que se capturan al registrar
  un abono (solo menciona cliente, monto, cuenta de efectivo y referencia). Se mantiene la regla
  general de la regla 2 ("todo ingreso indica categoría") de forma consistente con el bloque anterior:
  `RegisterReceivablePaymentCommand` pide una sola categoría para todo el abono (todas sus filas la
  comparten), igual que comparten cuenta de efectivo y referencia.
- **El ingreso de un abono vale el monto más la retención**, no solo el efectivo recibido: ambos
  rebajan el saldo de la cuenta por cobrar (regla 2/3), así que ambos cuentan como ingreso reconocido.
- **El límite de autorización (regla 7) se compara contra ese mismo total** (monto más retención de
  todas las líneas del abono), no solo contra el efectivo. Si `SystemParameters` no está configurado
  todavía, se trata como límite cero: cualquier abono registrado por un perfil que no sea Gerencia se
  rechaza hasta que Gerencia configure el límite real. Es la opción más segura ante la ausencia de un
  valor, y evita que un olvido de configuración se traduzca en abonos sin control.
- **La advertencia de referencia duplicada (regla 10) no bloquea**: `RegisterAsync` devuelve un
  indicador (`DuplicateReferenceWarning`) junto con el resultado, y quien construya la pantalla decide
  cómo mostrarlo.

## Consecuencias

- Nuevas tablas `finanzas.Abonos` y `finanzas.Retenciones` (migración
  `AddReceivablePaymentsAndWithholdings`), que también agrega la clave foránea diferida de
  `Ingresos.ReceivablePaymentId` (pendiente desde ADR-0023).
- `IReceivablePaymentService` (`RegisterAsync`, `CancelAsync`, `GetAsync`) e `IReceivableService`
  (`GetAsync`, `GetPendingByCustomerAsync`) quedan listos para que el bloque de pantallas (paso 5 del
  roadmap) construya el formulario de registro, con el reparto propuesto de la más antigua a la más
  nueva como lógica de la propia pantalla (la consulta ya viene ordenada por vencimiento).
- `docs/02-requisitos-trazabilidad.md` actualiza RF-INV-006 y RN-INV-006, y agrega RF-INV-009,
  RN-INV-014 y RN-INV-015.
- Pendiente del bloque siguiente: el ingreso manual (`Income.Origin = Manual`) y, después, las
  pantallas web de todo este paso 2.
