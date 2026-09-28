# ADR-0012: Catálogo de cuentas contables

- **Fecha:** 2026-09-27
- **Estado:** Aceptado
- **Complementa:** [ADR-0009](ADR-0009-facturacion-administrativa.md)

## Contexto

El sistema tenía cuentas financieras (cajas y bancos con moneda fija) y categorías financieras de
ingreso y gasto, pero ninguna forma de clasificar contablemente cada categoría. La regla aprobada pide un
único catálogo de cuentas contables, con tipo, jerarquía y estado, en el que las cajas y los bancos sean
cuentas de Activo «de efectivo» con moneda fija, y en el que cada categoría apunte a una cuenta de su
mismo tipo. Es solo clasificación: el alcance excluye la partida doble, los asientos, el diario, el mayor
y los estados financieros.

## Decisión

### Catálogo

- Se crea `LedgerAccount` (`catalogos.CuentasContables`) con código único, nombre, tipo (Activo, Pasivo,
  Patrimonio, Ingreso o Gasto), cuenta superior opcional, estado, referencia y descripción opcionales,
  auditoría y control de concurrencia.
- Una cuenta de efectivo se identifica con un único campo, `CashKind` (Caja o Banco), que es nulo cuando
  la cuenta no es de efectivo. Así no puede haber un indicador y un subtipo contradictorios. Una cuenta de
  efectivo es de tipo Activo y tiene moneda CRC o USD; una que no es de efectivo no tiene moneda. Se
  conserva la distinción entre caja y banco que existía en las cuentas financieras.
- Tipo, subtipo de efectivo y moneda son inmutables. Código, nombre, cuenta superior, referencia y
  descripción se pueden modificar. El servidor toma siempre el tipo, el subtipo y la moneda del registro
  guardado, nunca del formulario de edición.
- No se almacena saldo. Los saldos se calcularán desde los movimientos confirmados.
- Las cuentas se desactivan y reactivan; no se eliminan.

### Jerarquía

- La cuenta superior debe existir, estar activa y ser **del mismo tipo** que la cuenta hija. La jerarquía
  no admite autorreferencias ni ciclos.
- **Una cuenta de efectivo no puede tener cuentas hijas**, para que su moneda y sus movimientos futuros no
  sean ambiguos. Sí puede colgar de una cuenta agrupadora de tipo Activo.
- No se desactiva una cuenta con cuentas hijas activas, y reactivar una hija exige que su superior esté
  activa. Es el mismo patrón que la jerarquía de categorías.

### Categorías

- Toda categoría financiera apunta a una cuenta contable (`LedgerAccountId`, obligatorio y con clave
  foránea restrictiva). La cuenta debe existir, estar activa y ser de tipo Ingreso para las categorías
  de ingreso y de tipo Gasto para las de gasto.
- Esa coincidencia de tipos se valida en la aplicación porque involucra dos tablas. Como ni la naturaleza
  de la categoría ni el tipo de la cuenta pueden cambiar, la relación permanece coherente después de
  validarla al asignarla.
- No se desactiva una cuenta que usan categorías activas, y una categoría no se reactiva si su cuenta está
  inactiva.

### Permisos

- Consultan Gerencia, Finanzas y Asistente, y solo Finanzas modifica, con las políticas
  `ViewBusinessCatalogs` y `ManageBusinessCatalogs` que ya existían.

### Migración de datos

- **Cuentas financieras.** Cada una se copió a `CuentasContables` como cuenta de Activo de efectivo, con el
  mismo identificador, código, nombre, moneda, referencia, estado y auditoría. El tipo antiguo (Caja o
  Banco) pasó a ser el subtipo.
- **Categorías existentes.** Como la cuenta es obligatoria, la migración asignó cada categoría a una
  cuenta raíz **definitiva**, sin etiquetas de provisional: «Ingresos» (código `INGRESOS`, tipo Ingreso) o
  «Gastos» (código `GASTOS`, tipo Gasto). Solo se crea la cuenta de las naturalezas que tenían categorías,
  a nombre del creador de la primera categoría de esa naturaleza. Son cuentas de nivel superior de las que
  pueden colgar otras, y Finanzas puede reasignar las categorías.
- **Tabla anterior.** Una migración posterior eliminó `finanzas.CuentasFinancieras` y su código. Antes de
  hacerlo comprueba que cada cuenta financiera tenga su equivalente (mismo identificador, tipo Activo,
  mismo subtipo y misma moneda); si falta alguna, se interrumpe con un mensaje claro y no elimina nada. Si
  se revierte, devuelve a la tabla anterior todas las cuentas de efectivo, incluidas las creadas después.
- Cada migración se probó en una base de prueba con datos de ejemplo antes de aplicarla a la base de
  desarrollo, incluido el caso en que falta un equivalente.

### Relación con ADR-0009

- Donde el ADR-0009 dice que el cobro usa «una cuenta financiera de la misma moneda», debe leerse: una
  **cuenta contable de efectivo** de la misma moneda. Lo mismo aplicará a los pagos. El servicio ya expone
  `GetActiveCashAccountsAsync`, que devuelve las cajas y bancos activos, opcionalmente de una moneda.

## Alternativas consideradas

- **Cuentas financieras y contables como catálogos separados.** Se descartó: duplicaría la identidad de
  las cajas y los bancos y obligaría a mantener un vínculo entre dos catálogos. La regla aprobada pide uno
  solo.
- **Dejar la cuenta de la categoría como columna opcional en la base.** Se descartó: la regla dice que toda
  categoría tiene una cuenta, y con una columna opcional solo la aplicación la garantizaría.
- **Cuentas «provisionales» para las categorías existentes.** Se descartó: se prefirió que las cuentas
  raíz sean definitivas y usables desde el primer día, sin etiquetas que alguien deba limpiar después.
- **Jerarquía sin restricción de tipo.** Se descartó: permite jerarquías incoherentes, como un gasto bajo
  un pasivo.

## Consecuencias

- Tres migraciones: `AddLedgerAccounts`, `AddCategoryLedgerAccount` y `DropFinancialAccounts`.
  `FinancialAccount` y `FinancialAccountType` dejan de existir en el código.
- El formulario de categorías exige elegir una cuenta contable, filtrada por la naturaleza elegida.
- Aún no existen ingresos ni pagos: cuando se construyan, elegirán su cuenta de efectivo entre las de
  `GetActiveCashAccountsAsync`, filtradas por la moneda del documento.
- El catálogo empieza casi vacío. No se carga un plan de cuentas de referencia: si la empresa lo desea, se
  puede ofrecer más adelante como se hizo con las tarifas de impuesto.
- La lógica de jerarquía (ciclos, descendientes) quedó duplicada entre los servicios de categorías y de
  cuentas. Puede unificarse en una limpieza posterior.
- Las pruebas cubren el dominio, el servicio y la jerarquía contra SQL Server, las restricciones de la
  base, el controlador, la autorización, la validación de formularios y el renderizado de las pantallas con
  datos reales.
