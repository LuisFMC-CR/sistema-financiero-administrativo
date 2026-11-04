# ADR-0045: Filtro de facturas por fecha de emisión y plazo de pago

- **Fecha:** 2026-11-03
- **Estado:** Aceptado

## Contexto

El usuario pidió revisar los filtros de las columnas de búsqueda en todas las pantallas (regla de
negocio 11: "cada listado tiene filtros sobre su propia tabla"). Una auditoría encontró que la
pantalla de Facturas solo filtraba por texto libre y estado, aunque `InvoiceQuery` ya tenía
`IssueDateFrom`/`IssueDateTo` (agregados para la pantalla de Consultas) y la tabla muestra columnas
de "Emisión" y "Plazo" sin ningún filtro propio que las use. De las dos mejoras propuestas en esa
auditoría para Facturas, el usuario aprobó solo esta.

## Decisión

- **`InvoiceQuery.PaymentTerm`** (nuevo, `PaymentTerm?`): filtra por plazo exacto cuando se indica;
  `InvoiceService.SearchAsync` agrega la cláusula `Where` correspondiente, mismo patrón que los
  filtros de fecha ya existentes.
- **`InvoicesController.Index`** recibe `issueDateFrom`, `issueDateTo` y `paymentTerm` como parámetros
  de consulta y los pasa a `InvoiceQuery`; estos ya existían en el contrato, solo faltaba exponerlos
  en esta pantalla (no en Consultas, que ya los usa).
- **Vistas**: `InvoiceFilterViewModel`/`InvoiceIndexViewModel`/`InvoicePaginationViewModel` ganan los
  tres campos; `_Filters.cshtml` agrega un selector de plazo y dos campos de fecha; `_Pagination.cshtml`
  los agrega como `asp-route-*` para que "Anterior"/"Siguiente" no los pierdan; el enlace "Limpiar" ya
  apuntaba a `Index` sin parámetros, así que sigue limpiando los tres campos nuevos sin cambios.

## Consecuencias

- Modificado: `InvoiceContracts.cs` (`InvoiceQuery.PaymentTerm`); `InvoiceService.cs`;
  `InvoicesController.cs` (`Index`); `InvoiceViewModels.cs`; `Views/Invoices/_Filters.cshtml`,
  `_Pagination.cshtml`, `Index.cshtml`.
- `docs/02-requisitos-trazabilidad.md` actualiza RF-INV-011.
- Verificación: 1 prueba SQL real nueva (`SearchAsync_FiltersByPaymentTerm`). 329 pruebas unitarias y
  374 de integración pasan.
- Verificación manual: la pantalla de Facturas muestra los tres filtros nuevos, el filtro de plazo y
  de fecha de emisión acotan correctamente el listado, y los valores se conservan al paginar y al
  recargar con los mismos parámetros en la URL.
- Pendiente (no aprobado en este bloque, queda para una decisión futura): agregar filtro por
  Tipo/Naturaleza en los 7 catálogos compartidos (Clientes, Proveedores, Productos y servicios,
  Categorías financieras, Cuentas contables, Tipos de IVA, Tipos de retención) y una columna de
  filtro para "Referencia del comprobante" en Cuentas por pagar; y agregar filtros propios a la
  Bitácora de operaciones y a la Bitácora de seguridad, que hoy no tienen ninguno.
