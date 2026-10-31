# ADR-0043: La línea de factura exige un artículo del catálogo

- **Fecha:** 2026-10-27
- **Estado:** Aceptado

## Contexto

El usuario reportó que, al agregar una línea a una factura, pedir la unidad de medida, la
descripción, el precio y la tarifa de IVA escritos a mano genera errores de tipográfia y confusión en
los usuarios. Pidió que, al elegir un artículo del catálogo, esos datos se completen solos y que la
persona solo necesite digitar la cantidad y el descuento.

Antes de este cambio, `CatalogItemId` era opcional en `InvoiceLineInputCommand`
(`IInvoiceService.AddLineAsync`/`ReplaceLineAsync`): se guardaba solo como referencia y no influía en
ningún otro campo de la línea, que siempre se escribía a mano (RF-INV-002, "permite descripciones de
venta no catalogadas"). Se consultó al usuario qué hacer con esa posibilidad de línea totalmente
libre; eligió eliminarla por completo.

## Decisión

- **`InvoiceLineInputCommand`** (Application) cambia de forma: `CatalogItemId` pasa de `Guid?` a
  `Guid` obligatorio; `Description` y `UnitOfMeasure` se eliminan del comando (ya no se piden: la
  línea siempre toma el nombre y la unidad del artículo); `UnitPrice` y `TaxTypeId` pasan a `decimal?`
  y `Guid?` porque ahora son un respaldo, no un dato siempre manual.
- **`InvoiceService.AddLineAsync`/`ReplaceLineAsync`** cargan el `CatalogItem` activo primero
  (`ResolveLineInputAsync`, que sustituye a la antigua `ValidateCatalogItemAsync`) y resuelven:
  `Description = catalogItem.Name`, `UnitOfMeasure = catalogItem.UnitOfMeasure` siempre (el artículo ya
  los exige como obligatorios, así que no hay caso de respaldo); `UnitPrice = catalogItem.ReferencePriceCrc
  ?? command.UnitPrice` y `TaxTypeId = catalogItem.DefaultTaxTypeId ?? command.TaxTypeId`, cada uno con
  su propio error claro si el artículo no trae el valor y tampoco se indicó a mano. El dominio
  (`Invoice`, `InvoiceLine`) no cambia: la invariante nueva vive en el servicio, igual criterio que la
  fecha de cierre (`ClosingDateGuard`) y la referencia repetida (`DuplicateReferenceGuard`).
- **`CatalogItemOption`** (Application, selector de artículo) gana `UnitOfMeasure` y
  `ReferencePriceCrc` para que la pantalla sepa, sin otra consulta, qué campos puede ocultar.
- **`_LineForm.cshtml`/`site.js`**: se quitan los campos de Descripción y Unidad de medida; el
  selector de artículo ya no admite "Sin artículo del catálogo"; los campos de Precio y Tarifa de IVA
  se ocultan con JavaScript cuando el artículo elegido ya los tiene definidos, y solo aparecen cuando
  hace falta completarlos. Cantidad y Descuento siguen siempre visibles y manuales.
- No se tocan `InvoiceLine.CatalogItemId` (sigue `Guid?` en el dominio) ni la columna
  `FacturaLineas.CatalogItemId` (sigue nula): las líneas de factura guardadas antes de este cambio
  pueden conservar un `CatalogItemId` nulo; la obligatoriedad solo rige para líneas nuevas.

## Consecuencias

- Modificado: `InvoiceContracts.cs` (`InvoiceLineInputCommand`), `CatalogItemContracts.cs`
  (`CatalogItemOption`), `CatalogItemService.GetActiveOptionsAsync`, `InvoiceService.cs`
  (`AddLineAsync`, `ReplaceLineAsync`, `ResolveLineInputAsync` en lugar de `ValidateCatalogItemAsync`),
  `InvoiceViewModels.cs` (`InvoiceLineInputViewModel`, `InvoiceLineCatalogItemOptionViewModel`),
  `InvoicesController.cs` (`ToLineCommand`, `EditLine` GET, `PopulateLineOptionsAsync`),
  `_LineForm.cshtml`, `site.js`.
- Pruebas: los escenarios reales existentes que agregaban una línea con `CatalogItemId` nulo ahora
  crean primero un artículo de catálogo (mismo nombre que la descripción libre que usaban antes, para
  no alterar sus aserciones) y lo usan como `CatalogItemId`. Se agregan 5 pruebas SQL reales nuevas en
  `SqlServerInvoiceServiceScenarioTests`: artículo con precio y tarifa propios (se ignoran los valores
  manuales), artículo sin precio de referencia que usa el precio manual, artículo sin precio de
  referencia y sin precio manual (rechazado), artículo sin tarifa predeterminada y sin tarifa manual
  (rechazado), artículo inactivo (rechazado). 292 pruebas unitarias y 311 de integración pasan.
- `docs/02-requisitos-trazabilidad.md` actualiza RF-INV-002 (ya no permite líneas sin catalogar) y
  RF-52 (el precio y la tarifa ya no se "proponen": se aplican, y solo se piden si el artículo no los
  tiene); `docs/05-modelo-datos.md` actualiza la descripción de `FacturaLineas.CatalogItemId`.
- Verificación manual: la app arranca; al agregar una línea con un artículo que tiene precio y tarifa
  propios, esos campos permanecen ocultos y la línea se guarda con los valores del catálogo; con un
  artículo sin alguno de esos datos, solo aparece el campo que falta.
