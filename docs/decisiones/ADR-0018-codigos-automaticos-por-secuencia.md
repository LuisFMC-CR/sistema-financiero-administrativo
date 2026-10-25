# ADR-0018: Códigos automáticos por secuencia SQL y ajustes a clientes, proveedores, productos y categorías

- **Fecha:** 2026-10-11
- **Estado:** Aceptado
- **Complementa:** [ADR-0006](ADR-0006-clientes-proveedores-separados.md),
  [ADR-0009](ADR-0009-facturacion-administrativa.md)

## Contexto

Los catálogos de clientes, proveedores, productos/servicios y categorías financieras pedían el código
en el formulario de creación. La regla de negocio 12 de `CLAUDE.md` exige que el sistema lo asigne
siempre: `CLI-0001`, `PRV-0001`, `PRD-0001`/`SRV-0001` y `ING-001`/`GAS-001`. Las reglas 50 y 51 del
documento académico también exigen que la identificación de clientes y proveedores sea obligatoria y
única, que el proveedor declare su actividad económica, y que un producto o servicio pueda proponer una
tarifa de IVA al facturarse (RF-52).

## Decisión

- **Código por secuencia.** Cada catálogo tiene su propia secuencia de SQL Server, creada por
  migración: `Seq_ClienteCodigo`, `Seq_ProveedorCodigo`, `Seq_ProductoCodigo`, `Seq_ServicioCodigo`
  (productos y servicios cuentan por separado, aunque comparten tabla), `Seq_CategoriaIngresoCodigo` y
  `Seq_CategoriaGastoCodigo`. Las dos últimas arrancan en 6 y 11 para no chocar con el catálogo base
  (`ING-001..005`, `GAS-001..010`) que siembra la migración `SeedBaseChartOfAccounts` (ADR-0017).
- El helper compartido `CatalogPersistence.GenerateSequentialCodeAsync` ejecuta
  `SELECT NEXT VALUE FOR <secuencia>` como un comando ADO.NET directo sobre la conexión (y la
  transacción activa, si hay una) del `DbContext`, **no** mediante `SqlQueryRaw`/`FromSqlRaw`: SQL
  Server no permite `NEXT VALUE FOR` dentro de la subconsulta que EF Core generaría al componerlos.
- El código es inmutable después de crear, igual que en cuentas contables (ADR-0017):
  `UpdateDetails`/`SaveXCommand` de `Customer`, `Supplier`, `CatalogItem` y `FinancialCategory` ya no
  reciben el código.
- `CatalogItem.Type` también queda fijo después de crear, porque el código ya lo codifica
  (`PRD-`/`SRV-`): `SaveCatalogItemCommand` se separa en `CreateCatalogItemCommand` (con `Type`) y
  `UpdateCatalogItemCommand` (sin `Type`), siguiendo el mismo patrón que `LedgerAccountContracts`.
- **Identificación obligatoria y única.** `Customer.Identification` y `Supplier.Identification` pasan
  de opcionales a obligatorias; el índice único deja de ser filtrado (`WHERE Identification IS NOT
  NULL`) porque ya no puede haber filas nulas.
- `Supplier` incorpora `EconomicActivity` (opcional, texto libre).
- `CatalogItem` incorpora `DefaultTaxTypeId` (RF-52): la tarifa de IVA que se propone al agregar el
  artículo a una línea de factura; la línea puede cambiarla. Se valida que exista y esté activa, igual
  que `DefaultIncomeCategoryId`.

## Consecuencias

- Migración `AdjustExistingCatalogs`: crea las seis secuencias; agrega `EconomicActivity` a
  `Proveedores` y `DefaultTaxTypeId` (con FK a `TiposImpuesto`) a `ProductosServicios`; convierte
  `Identification` a `NOT NULL` en `Clientes`/`Proveedores` y reemplaza sus índices únicos filtrados
  por índices únicos simples. No hubo filas con identificación nula en la base real al migrar.
- Las pantallas de creación de los cuatro catálogos ya no piden código; las de edición lo muestran de
  solo lectura, igual que en cuentas contables.
- Queda pendiente, sin decidir: si `FinancialCategory.ParentId`/`LedgerAccountId` también deberían
  volverse inmutables después de crear, siguiendo el mismo criterio que `LedgerAccount` — hoy
  `ChangeParent`/`ChangeLedgerAccount` siguen permitiendo el cambio.
- `docs/02-requisitos-trazabilidad.md` agrega `RF-50`, `RF-51`, `RF-52`, `RN-CAT-010` y `RN-CAT-011`.
