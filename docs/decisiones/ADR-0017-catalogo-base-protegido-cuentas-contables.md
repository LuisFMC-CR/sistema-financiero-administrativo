# ADR-0017: Catálogo base protegido de cuentas contables

- **Fecha:** 2026-10-11
- **Estado:** Aceptado
- **Complementa:** [ADR-0012](ADR-0012-catalogo-de-cuentas-contables.md)

## Contexto

El ADR-0012 creó `LedgerAccount` con cinco tipos (Activo, Pasivo, Patrimonio, Ingreso, Gasto) y permitía
crear cuentas raíz libremente. Al revisar la regla de negocio 1 de `CLAUDE.md` con el usuario, el
sistema no lleva partida doble ni pasivo ni patrimonio: solo clasifica efectivo, ingresos y gastos. La
regla 13 exige además un catálogo base que se instale con el sistema, con sus grupos protegidos para
que el usuario no pueda alterar la clasificación raíz.

## Decisión

- `LedgerAccountType` se reduce a `Asset = 1`, `Income = 4`, `Expense = 5` (se quitan `Liability` y
  `Equity`; los valores numéricos no se renumeraron, para no alterar datos existentes).
- `LedgerAccount` incorpora `IsProtected`. El constructor público del dominio siempre lo deja en
  `false`; solo la siembra inicial, por SQL directo dentro de la migración, lo pone en `true`. El
  dominio nunca lo expone como parámetro de creación.
- Después de creada, una cuenta solo permite cambiar el nombre y la referencia bancaria. El código, el
  tipo, la cuenta superior y el subtipo de efectivo quedan fijos (regla 12); una cuenta protegida
  tampoco admite esos dos cambios ni desactivarse.
- El código es jerárquico (`3.1.07`) y lo calcula el servicio como el siguiente hijo libre de la cuenta
  superior, con reintento ante colisión de concurrencia.
- **Usuario técnico de siembra:** las filas sembradas necesitan un `CreatedByUserId` válido, pero el
  primer administrador real todavía no existe cuando corre la migración. Se crea un usuario técnico fijo
  (`sistema@interno.local`, `IsActive = 0`, sin contraseña ni rol, GUID fijo
  `00000000-0000-0000-0000-000000000001`) en la misma migración, usado solo como autor de los datos
  sembrados.

## Consecuencias

- Migración `SeedBaseChartOfAccounts`: inserta el usuario técnico, las 26 cuentas del catálogo base
  (regla 13) y las 15 categorías financieras iniciales (una por cuenta de detalle), todas con GUIDs
  fijos y legibles por rama (`1xxxxxxx-...` Activo, `2xxxxxxx-...` Ingresos, `3xxxxxxx-...` Gastos).
  También agrega `IsProtected` y angosta `CK_CuentasContables_Type` a `(1, 4, 5)`.
- El usuario solo agrega cuentas de detalle dentro de los grupos protegidos; nunca cuentas raíz nuevas
  ni cambia el catálogo base.
- Las pruebas de escenario (`SqlServerLedgerAccountScenarioTests`, `SqlServerCatalogScenarioTests`)
  buscan las cuentas raíz reales por código (`GetByCodeAsync`) en vez de crearlas, porque el servicio ya
  no permite crear cuentas sin padre.
- `docs/02-requisitos-trazabilidad.md` corrige `RN-CTA-001`/`RN-CTA-002` (sin Pasivo/Patrimonio ni
  moneda) y agrega `RN-CTA-007`/`RN-CTA-008` para el código jerárquico y el catálogo base protegido.
