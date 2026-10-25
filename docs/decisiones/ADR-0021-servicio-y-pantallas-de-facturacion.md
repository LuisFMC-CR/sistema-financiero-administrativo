# ADR-0021: Servicio y pantallas de facturación (sin cuentas por cobrar ni ingresos)

- **Fecha:** 2026-10-16
- **Estado:** Aceptado
- **Complementa:** [ADR-0009](ADR-0009-facturacion-administrativa.md),
  [ADR-0010](ADR-0010-condicion-de-pago-redondeo-y-edicion-de-borradores.md),
  [ADR-0019](ADR-0019-numeracion-factura-al-confirmar.md),
  [ADR-0020](ADR-0020-plazos-de-pago-fijos.md)

## Contexto

El dominio y la persistencia de la factura administrativa estaban completos (ADR-0009, ADR-0010,
ADR-0019, ADR-0020), pero no existía `IInvoiceService` ni `InvoicesController`: nadie podía crear una
factura desde el navegador. Construir el flujo completo factura → cuenta por cobrar → abono → ingreso
de una sola vez exige entidades que todavía no existen (`CuentaPorCobrar`, `Ingreso`, `Abono`). Se
decidió primero el ciclo de la factura por sí sola —crear el borrador, editar sus líneas, confirmarla y
anularla— y dejar la generación automática del ingreso o la cuenta por cobrar (regla 2) para cuando esas
entidades se construyan.

## Decisión

### Capa de Application

- `IInvoiceService` (en `Application/Invoices/InvoiceContracts.cs`) expone `SearchAsync`, `GetAsync`,
  `CreateAsync`, `UpdateHeaderAsync`, `AddLineAsync`, `ReplaceLineAsync`, `RemoveLineAsync`,
  `ConfirmAsync` y `CancelAsync`.
- `InvoiceQuery`/`InvoiceStatusFilter` son el equivalente de `CatalogQuery`/`CatalogStatusFilter` para
  un documento que no tiene baja lógica sino el ciclo Borrador/Confirmada/Anulada.
- `InvoiceLineInputCommand` sirve tanto para agregar como para reemplazar una línea: los datos que se
  capturan son idénticos: solo cambia a cuál línea se aplican. Evita duplicar un segundo tipo idéntico.
- `ICustomerCatalogService` y `ICatalogItemService` ganan `GetActiveOptionsAsync` (clientes y
  artículos activos), con el mismo patrón que ya tenían `ILedgerAccountService` y `ITaxTypeService`.
- Nueva política `Movimientos.Ver` (Gerencia, Finanzas, Asistente): el listado y el detalle de
  facturas son de solo consulta y necesitan un permiso distinto de `Catalogos.Ver`, que es para los
  catálogos de mantenimiento.

### Regla «Asistente ve solo las suyas»

- Todas las operaciones de lectura y de edición de borrador que podría usar un Asistente reciben un
  `restrictToOwnerUserId` opcional. Cuando no es nulo, cualquier factura de otro usuario se trata como
  inexistente: no hay una respuesta distinta para «no existe» y para «no es tuya», así que una URL
  manipulada no revela si el identificador es válido.
- `InvoicesController.OwnerRestriction()` decide ese valor por rol: lo fija al actor cuando el usuario
  es Asistente y lo deja nulo para Finanzas y Gerencia, que no tienen esa restricción.
- Confirmar y anular no reciben esta restricción porque ya son exclusivos de Finanzas y Gerencia por su
  propia política (`Movimientos.Confirmar`, `Movimientos.Anular`), que ningún Asistente cumple.

### Infraestructura

- `InvoiceService` (Infrastructure) sigue el mismo patrón que los demás servicios de catálogo:
  concurrencia optimista con `CatalogPersistence.TrySetOriginalVersion`/`SaveChangesAsync`, validación
  de referencias (cliente activo, artículo activo, tarifa de IVA activa) antes de tocar el dominio.
- `CatalogPersistence.GenerateSequentialCodeAsync` se dividió para extraer
  `GetNextSequenceValueAsync`, que devuelve el valor crudo de una secuencia sin prefijo. Lo usa
  `InvoiceService.ConfirmAsync` para el número de factura (RF-34), reemplazando la copia local que
  `SqlServerInvoiceScenarioTests` tenía como marcador de posición (ADR-0019 ya anunciaba este
  reemplazo). Esa prueba sigue con su copia porque sigue ejercitando el dominio directamente contra el
  `DbContext`, no el servicio nuevo.

### Pantallas

- Ruta `/facturacion/facturas`, controlador `InvoicesController`. Cada acción de mutación de borrador
  pide la política `Movimientos.CrearBorrador` (Finanzas y Asistente); confirmar pide
  `Movimientos.Confirmar` (solo Finanzas) y anular pide `Movimientos.Anular` (solo Gerencia). Las tres
  son subconjuntos de `Movimientos.Ver` (la política de la clase), así que combinarlas con
  `[Authorize]` adicional en cada acción restringe correctamente sin contradicciones, igual que ya
  hacían `LedgerAccountsController`/`CatalogItemsController` con `Catalogos.Administrar`.
- El encabezado, agregar línea y editar línea son pantallas separadas (como ya separan Crear/Editar los
  demás catálogos), no un editor dinámico con JavaScript: no hay bibliotecas nuevas que agregar a
  `wwwroot/lib` y cada formulario es una sola responsabilidad.
- El detalle (`Details.cshtml`) es la única pantalla «de lectura»: muestra encabezado, líneas y
  totales, y condiciona los botones de edición, confirmación y anulación a `CanEditDraft`/
  `CanConfirm`/`CanCancel`, calculados en el controlador con `IAuthorizationService` y el estado del
  documento. La factura con formato profesional de la regla 15 (PDF) no se construyó aquí: corresponde
  al módulo Reportes.
- Quitar una línea y confirmar reutilizan el patrón `js-confirm-form` que ya usan los demás catálogos
  para pedir confirmación antes de una acción sensible (RNF). Anular pide motivo, así que usa una
  pantalla propia con un formulario normal en lugar de ese patrón de un clic.

## Alternativas consideradas

- **Construir ya `CuentaPorCobrar` e `Ingreso` para que `Confirm` genere el movimiento real.** Se
  descartó para este cambio: exige diseñar esas entidades, su persistencia y sus propias reglas antes
  de tener nada que probar en pantalla. Queda como el siguiente bloque del roadmap.
- **Reutilizar `Catalogos.Ver` para el listado de facturas.** Se descartó: el nombre describe
  catálogos de mantenimiento, no documentos financieros, y el matiz entre uno y otro ya importará
  cuando existan más documentos (gastos, abonos, pagos) con la misma necesidad de una política de
  «ver movimientos» distinta de «ver catálogos».
- **Un solo formulario dinámico (JavaScript) para encabezado y líneas.** Se descartó: el proyecto no
  usa CDN ni bibliotecas de formularios dinámicos, y una pantalla por responsabilidad es más simple de
  entender y de probar que un editor de una sola página con muchos estados.

## Consecuencias

- No hay migraciones nuevas: las tablas de factura ya existían.
- Pruebas: `SqlServerInvoiceServiceScenarioTests` (ciclo completo, numeración distinta entre dos
  facturas, restricción de propietario, conflicto de concurrencia, búsqueda filtrada) e
  `InvoiceAuthorizationTests` (política de cada ruta, redirección anónima). Como ya sucede con
  `CatalogItemsController` (que tampoco tiene una prueba de controlador con dobles de prueba
  manuscritos, solo de SQL Server y de autorización), no se agregó aquí un archivo de pruebas de
  controlador con tres servicios doblados a mano: las acciones del controlador son delegaciones
  directas a `IInvoiceService`, y la cobertura real de las reglas vive en el servicio.
- Pendiente explícito, no abordado en este ADR: al confirmar una factura de contado o a crédito, nada
  genera todavía su ingreso o su cuenta por cobrar (regla 2); la factura con formato profesional en PDF
  (regla 15) y los reportes de facturación tampoco existen.
