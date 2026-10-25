# ADR-0033: Pantalla de Consultas

- **Fecha:** 2026-10-25
- **Estado:** Aceptado

## Contexto

Segundo bloque del paso 5 de `CLAUDE.md`: la pantalla de Consultas con filtros combinados (regla de
negocio 11). Confirmado con el usuario: es un selector de tipo de documento más los filtros
combinados que apliquen a ese tipo, y cada resultado enlaza al `Details` que ya existe de su propio
módulo, sin construir una vista de detalle unificada nueva.

## Decisión

- **Cero métodos nuevos en Application o Infrastructure**, salvo una brecha real encontrada al
  armar el filtro de fecha: `InvoiceQuery` no tenía rango de fechas (los demás `Query` sí). Se agregó
  `IssueDateFrom`/`IssueDateTo` a `InvoiceQuery` e `InvoiceService.SearchAsync`; el resto es
  composición en `QueriesController`, que llama al `SearchAsync` que ya existe de cada servicio
  (`IInvoiceService`, `IExpenseService`, `IReceivableService`, `IPayableService`, `IIncomeService`).
- **`QueryResultRow`** (Web) es la forma común a la que se traduce cada resultado, para que la
  pantalla tenga una sola tabla sin importar el tipo elegido; cada fila enlaza al controlador de
  detalle real de su módulo.
- **`IncomesController` no tenía pantalla de `Details`** (a diferencia de los demás módulos); se
  agregó, porque Consultas necesita un destino para el detalle de un ingreso (regla 11: "detalle con
  registros relacionados e historial"). Es de solo lectura, con la misma política de clase que
  `Index`, y el enlace "Anular" que ya tenía la fila del listado.
- **Los filtros combinados que no aplican al tipo elegido no se muestran**, en vez de mostrarse
  deshabilitados: cliente y proveedor nunca coinciden en el mismo documento, así que mostrar los seis
  filtros siempre sería confuso. El estado se filtra con un solo campo de texto convertido al enum
  correcto de cada módulo (`Enum.TryParse` con reserva a "Todos" si no es válido).
- **Incluye el mismo indicador de cuentas vencidas que la página de inicio** (ADR-0032), reutilizando
  `_OverdueIndicator.cshtml`.
- **Política `Reportes.Ver`**, igual criterio que Cuentas por Cobrar/Pagar e Ingresos: Asistente no
  entra, porque Consultas expone saldos y documentos de todos los usuarios, no solo los propios.

## Consecuencias

- Nuevo: `QueriesController` (`/consultas`), `Views/Queries/{Index,_Pagination}.cshtml`,
  `Models/Queries/QueryViewModels.cs`, `IncomesController.Details`, `Views/Incomes/Details.cshtml`.
- Modificado: `InvoiceQuery`/`InvoiceService.SearchAsync` (rango de fechas), `Views/Incomes/Index.cshtml`
  (la fecha ahora enlaza al detalle y muestra el código), menú lateral (`_Layout.cshtml`).
- `docs/02-requisitos-trazabilidad.md` agrega RF-CON-002.
- Verificación: `QueriesAuthorizationTests` (sin SQL Server). 269 pruebas unitarias y 242 de
  integración pasan contra SQL Server real.
- Verificación manual: la app arranca y `/consultas`, `/consultas?type=Expense` e
  `/ingresos/{id}` exigen inicio de sesión.
- Pendiente: los seis reportes en PDF y Excel (bloques 3 a 6 del paso 5).
