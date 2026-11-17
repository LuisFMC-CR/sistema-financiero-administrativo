# ADR-0061: Unidades de medida limitadas a Unidad y Hora

- **Fecha:** 2026-11-16
- **Estado:** Aceptado
- **Relacionado:** ADR-0059 (congelamiento del sistema).

## Contexto

Los productos y servicios elegían su unidad de una lista fija de cuatro valores (`UnitOfMeasureCode`:
1 Unidad, 2 Hora, 3 Servicios profesionales, 4 Otros servicios). El usuario consideró innecesarias las
dos últimas y pidió dejar solo Unidad y Hora. El documento del TFG enumera las cuatro, así que es una
**excepción al congelamiento aprobada por el usuario** (16/11/2026), que actualizará el documento.
Eliminar los valores 3 y 4 obligaría a cambiar la restricción `CK_ProductosServicios_UnitOfMeasure`
con una migración, y el sistema congelado no admite migraciones.

## Decisión

1. `UnitOfMeasureCodeExtensions.Selectable` (Unidad, Hora) e `IsSelectable` definen las unidades
   vigentes. `CatalogItem` (constructor y `UpdateDetails`) rechaza las demás con "Solo se admiten las
   unidades de medida Unidad u Hora."
2. El formulario y el filtro del listado solo ofrecen Unidad y Hora.
   `SelectableUnitOfMeasureAttribute` muestra "Seleccione la unidad de medida Unidad u Hora." junto al
   campo.
3. Los valores 3 y 4 se conservan en la enumeración y en la base, marcados como retirados, solo para
   mostrar artículos antiguos. Al editar uno de ellos, el formulario muestra la unidad con
   "(retirada)" y exige elegir Unidad u Hora para guardar.
4. En la base de desarrollo, el único artículo con unidad retirada (SRV-0053 "Servicio técnico",
   Servicios profesionales) se pasó a Hora por decisión del usuario.

## Consecuencias

- No hay migración ni cambio de la base: la restricción CHECK sigue admitiendo 1 a 4.
- Las líneas de factura y de nota de crédito guardan la unidad como texto, así que los documentos
  existentes no cambian.
- El documento del TFG debe actualizar la lista de unidades de medida.
- Pruebas: `CatalogItemTests` (dominio) y `SelectableUnitOfMeasureAttributeTests` (formulario).
