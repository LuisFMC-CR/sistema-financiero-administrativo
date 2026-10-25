# ADR-0025: Ingreso manual

- **Fecha:** 2026-10-24
- **Estado:** Aceptado

## Contexto

Cierra la parte de dominio del paso 2 de `CLAUDE.md`: `Income` ya cubre el origen automático de
factura de contado (ADR-0023) y de abono (ADR-0024); falta el origen manual (regla de negocio 2),
reservado a "otras entradas de la operación (reintegros, intereses que acredite el banco); nunca
préstamos ni aportes".

## Decisión

- **`Income.CreateManual` reutiliza la misma entidad y la misma tabla**, sin una clase ni un esquema
  nuevos: solo cambia el origen (`IncomeOrigin.Manual`), igual categoría y cuenta de efectivo que los
  demás orígenes, y el mismo medio de pago que ya exigía la factura de contado.
- **La descripción pasa a ser obligatoria cuando el origen es manual.** No existe un campo de "tipo"
  estructurado que distinga un reintegro de un interés bancario, así que la descripción es el único
  rastro de qué fue el ingreso y por qué no es un préstamo ni un aporte (prohibidos por la regla 2).
  Se agrega `CK_Ingresos_Description` en la base de datos, repitiendo esa misma validación, igual que
  ya se hace con el origen y el medio de pago.
- **Solo un ingreso manual se anula directamente.** Uno de factura o de abono se anula únicamente
  cancelando su documento de origen (regla 4: "lo generado automáticamente solo se anula desde su
  origen"); `IIncomeService.CancelAsync` rechaza cualquier intento sobre un ingreso que no sea
  manual, sin modificar nada.
- **Sin transacción explícita.** A diferencia de `InvoiceService`/`ReceivablePaymentService` (que
  combinan una secuencia SQL cruda con cambios de EF Core), `IncomeService` solo agrega el `Income` y
  su entrada de bitácora en el mismo `SaveChangesAsync`, que ya es atómico por sí mismo.

## Consecuencias

- Nueva migración `AddManualIncomeDescriptionCheck` (solo agrega el `CHECK`; la tabla `Ingresos` ya
  existía desde ADR-0023).
- `IIncomeService` (`GetAsync`, `CreateManualAsync`, `CancelAsync`) queda listo para la pantalla de
  Ingresos del bloque siguiente del roadmap (paso 5: pantallas web).
- `docs/02-requisitos-trazabilidad.md` agrega RF-INV-010.
- Con esto termina la parte de dominio y servicios del paso 2 de `CLAUDE.md`; solo falta construir las
  pantallas (Cuentas por Cobrar, registro de abonos, Ingresos) sobre los servicios ya existentes.
