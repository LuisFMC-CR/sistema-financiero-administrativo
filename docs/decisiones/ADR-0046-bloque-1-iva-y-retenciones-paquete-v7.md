# ADR-0046: Bloque 1 del paquete v7 — IVA y retenciones pendientes

- **Fecha:** 2026-11-03
- **Estado:** Aceptado

## Contexto

`prompt_claude_code_mejoras_finales_v7.md` reemplaza al paquete v2 para todo lo que faltaba. El bloque 1
cubre seis puntos que el documento deja parcialmente abiertos o "a decidir en el ADR": tarifas de IVA
2 % y No sujeto, condición de IVA del cliente (RF-93), retención del 2 % del sector público (RF-95),
IVA no acreditable en gastos (RF-94), y la pantalla única "Impuestos y retenciones" que reemplaza a
`TaxTypesController`/`WithholdingTypesController`.

## Decisiones

### 1. Código de Hacienda de "No sujeto" es opcional

No existe un código de tarifa de IVA de Hacienda para "no sujeto" en el catálogo disponible.
`TaxType.HaciendaRateCode` pasa a `string?`; solo la tarifa de sistema "No sujeto" lo deja en blanco.
Cualquier tarifa agregada a mano (incluida otra de sistema futura) sigue exigiendo 2 dígitos. **Pendiente
de validar contra el Anexo 4.4 oficial de Hacienda** cuando el usuario tenga acceso al documento.

### 2. Identificación "por rol", nunca por código ni nombre

Mismo criterio que `LedgerAccountSystemRole` (regla 1 de `CLAUDE.md`): se agrega `TaxTypeSystemRole`
(`General13`, `Exempt`, `Reduced2`, `NotSubject`) en `TaxType` y `WithholdingType.IsSystemType` (único
tipo de sistema: la retención del 2 % del sector público). Ninguno de los dos se recibe por ningún
comando público; solo la migración de siembra los asigna directamente por SQL. Las reglas de
contabilización y de resolución de tarifa buscan siempre por rol.

### 3. Backfill de tarifas existentes por `Rate` + `HaciendaRateCode`, no por código ni nombre

La migración `AddTaxTypeSystemRolesAndVatRates` identifica "IVA 13 %" por `Rate = 13 AND
HaciendaRateCode = '08'` y "Exento" por `Rate = 0`, después de corregir su código de Hacienda mal
asignado (`'01'` → `'10'`, en `TiposImpuesto` y en la fotografía de `FacturaLineas` ya confirmadas).
Todas las actualizaciones de backfill filtran `AND [IsActive] = 1` para no colisionar con filas de
prueba inactivas que coincidan por casualidad en `Rate`/código.

### 4. Alcance editable reducido en la pantalla única

El documento dice "Finanzas y Gerencia editan el porcentaje, la base mínima, la descripción y el
estado" — ya no menciona código, nombre ni código de Hacienda. Se interpretó como una restricción
nueva: en `TaxesAndWithholdingsController`, `EditTaxType`/`EditWithholdingType` muestran código, nombre
y (en tarifas) código de Hacienda de solo lectura; el servidor **siempre** reconstruye el comando con
los valores actuales de esos campos (nunca con lo que el formulario envía), para que una manipulación
del DOM no pueda cambiarlos. Esos tres campos solo se piden al **crear** una tarifa o un tipo de
retención nuevos, acción reservada a Gerencia (`User.IsInRole(SystemRoles.Management)`, además de la
política `ManageBusinessCatalogs` que ya comparten Finanzas y Gerencia).

### 5. Rutas anteriores: solo el listado redirige; crear/editar/estado se retiran

`TaxTypesController`/`WithholdingTypesController` quedan con una sola acción `Index` que hace
`RedirectToAction("Index", "TaxesAndWithholdings")`; sus acciones `Create`, `Edit` y `SetActive` se
eliminaron (la lógica vive ahora en `TaxesAndWithholdingsController`, con sus propias rutas bajo
`/impuestos-y-retenciones`). Las vistas `Views/TaxTypes/*.cshtml` y `Views/WithholdingTypes/*.cshtml`
se eliminaron por quedar huérfanas; igual los `TaxTypeEditViewModel`/`WithholdingTypeEditViewModel`
(reemplazados por `TaxTypeRateEditViewModel`/`WithholdingTypeRateEditViewModel`, de alcance reducido).

### 6. Se elimina por completo la carga manual de tarifas de referencia

Primero se retiró solo el botón "Cargar tarifas de referencia" de la interfaz, dejando
`ITaxTypeService.LoadReferenceTaxTypesAsync`/`ReferenceTaxTypesLoadResult`/`ReferenceTaxTypes` en el
código sin ninguna acción que los invocara. El usuario confirmó eliminar también ese código: se
retiraron el método de la interfaz y su implementación, el record `ReferenceTaxTypesLoadResult`, la
clase `ReferenceTaxTypes` (Application) y sus pruebas (`ReferenceTaxTypesTests`,
`SqlServerTaxTypeScenarioTests.LoadReference_CreatesOnlyTheMissingRatesAndNeverTouchesExistingOnes`).
Las migraciones `SeedReferenceTaxTypes` y `AddTaxTypeHaciendaRateCode` no dependían de esa clase (su
SQL va embebido directamente), así que no se tocaron: las cuatro tarifas de referencia (IVA 13 %,
IVA 2 %, Exento, No sujeto) siguen naciendo siempre por migración de siembra.

### 7. Recalculación de IVA al cambiar el cliente de una factura en borrador

`InvoiceService.UpdateHeaderAsync` ahora carga la factura con sus líneas y, si el cliente cambia,
reevalúa la tarifa de cada línea con la misma lógica de resolución de la condición de IVA
(`ResolveLineTaxTypeIdAsync`), reemplazando solo las líneas cuya tarifa resultó distinta. El resultado
informa cuántas líneas cambiaron; `InvoicesController` lo agrega al mensaje de éxito.

### 8. Fallback a IVA 13 % para cliente Normal sin tarifa indicada

Si el cliente tiene condición Normal y ni el artículo del catálogo ni el formulario indican una
tarifa, el sistema usa la tarifa de sistema `General13` en vez de rechazar la línea. Esto cambió el
comportamiento de una prueba existente (ahora espera éxito con 13 %, no error), documentado como
decisión nueva, no como corrección de un defecto.

## Consecuencias

- Nuevo: `TaxTypeSystemRole.cs`, `VatCondition.cs`, migraciones `AddTaxTypeSystemRolesAndVatRates`,
  `AddCustomerVatConditionAndStateWithholding`, `AddWithholdingTypeMinimumBaseAndStateWithholding`,
  `AddExpenseVatCreditable`; `TaxesAndWithholdingsController` y sus vistas; `TaxesAndWithholdingsViewModels.cs`.
- Modificado: `TaxType`, `WithholdingType`, `Customer`, `Expense` (Domain); sus configuraciones EF Core;
  `TaxTypeContracts`, `WithholdingTypeContracts`, `InvoiceContracts`, `ExpenseContracts` (Application);
  `TaxTypeService`, `WithholdingTypeService`, `InvoiceService`, `CatalogItemService`,
  `ReceivablePaymentService`, `JournalPostingService` (Infrastructure); `CustomersController`,
  `InvoicesController`, `ExpensesController`, `ReceivablePaymentsController`, `_Layout.cshtml` y varias
  vistas (Web).
- Retirado de la interfaz: `TaxTypesController`/`WithholdingTypesController` (solo `Index` con
  redirección), sus vistas `Create`/`Edit`, y el botón "Cargar tarifas de referencia" (ver punto 6).
- `docs/02-requisitos-trazabilidad.md` agrega RF-93, RF-94 y RF-95, y ajusta RF-49 y RF-52.
- Verificación: 708 pruebas en verde (334 unitarias + 374 de integración, estas con
  `SISTEMA_FINANCIERO_RUN_SQL_TESTS=1`), incluidas pruebas nuevas de dominio
  (`TaxType`/`WithholdingType` rechazan inactivar una fila de sistema; `Customer` valida
  `VatCondition`), de `InvoiceService` (fuerza o rechaza la tarifa según la condición del cliente;
  recalcula líneas al cambiar el cliente), de `ReceivablePaymentService` (rechaza una retención que no
  corresponde; respeta la base mínima), de `JournalPostingService`/`Expense` (asiento sin línea de IVA
  acreditable cuando `VatCreditable = false`), y del controlador unificado (edición ignora los campos
  de solo lectura enviados por el cliente; autorización por rol en cada acción).
- Verificación manual en navegador: **pendiente**. El entorno de desarrollo ya tiene usuarios de
  prueba para Finanzas y Gerencia (creados en una sesión anterior), pero sus contraseñas no están
  disponibles en este contexto; queda para que el usuario la haga con sus propias credenciales, o
  indique cómo generar una contraseña de prueba.
