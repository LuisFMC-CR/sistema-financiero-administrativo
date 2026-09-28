# Requisitos y trazabilidad

## Convenciones

- `RF`: requisito funcional.
- `RN`: regla de negocio.
- `RNF`: requisito no funcional.
- `UC`: caso de uso.
- `PR`: prueba.
- `ADR`: decisión arquitectónica.
- `EV`: evidencia.

Estados: **Propuesto**, **Aprobado**, **En desarrollo**, **Implementado**, **Verificado** o **Descartado**.

## Matriz del incremento fundacional

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-SEG-001 | Un usuario interno puede iniciar sesión con correo y contraseña. | Anteproyecto | UC-SEG-001 | `AccountController.Login` | Pendiente con usuario real | Implementado |
| RF-SEG-002 | Un usuario autenticado puede cerrar su sesión mediante POST. | Seguridad | UC-SEG-002 | `AccountController.Logout` | Pendiente con usuario real | Implementado |
| RF-SEG-003 | Un usuario autenticado puede cambiar su contraseña. | Seguridad | UC-SEG-003 | `AccountController.ChangePassword` | Pendiente con usuario real | Implementado |
| RF-SEG-004 | Los recursos nuevos exigen autenticación salvo excepción explícita. | Seguridad | UC-SEG-001 | Política global | PR-SEG-001 | Verificado |
| RF-SEG-005 | No existe registro público de usuarios. | Alcance aprobado | UC-SEG-004 | Identity sin UI predeterminada | PR-SEG-005 | Verificado |
| RF-SEG-006 | El primer administrador y los roles se crean mediante un comando explícito e idempotente. | Seguridad | UC-SEG-004 | `IdentityBootstrapper` | Pendiente de bootstrap local | Implementado |
| RN-SEG-001 | Solo existen Administrador, Gerencia, Finanzas y Asistente. | Aprobación del usuario | UC-SEG-004 | `SystemRoles` | PR-ARQ-003 | Verificado |
| RN-SEG-002 | Un usuario inactivo no puede iniciar sesión. | Control interno | UC-SEG-001 | `AccountController.Login` | Pendiente | Implementado |
| RN-SEG-003 | Los permisos se validan en el servidor mediante políticas. | Control interno | Todos | `SystemPolicies` y configuración web | Pendiente por rol | Implementado |
| RNF-SEG-001 | Las contraseñas se administran con Identity y nunca se almacenan en texto. | Seguridad | Todos | ASP.NET Core Identity | PR-SEG-003 | Verificado |
| RNF-SEG-002 | Cinco fallos bloquean la cuenta durante 15 minutos. | Seguridad | UC-SEG-001 | Opciones de Identity | PR-SEG-003 | Verificado |
| RNF-DAT-001 | La persistencia utiliza SQL Server Express mediante EF Core. | Anteproyecto | No aplica | `FinancialDbContext` | EV-DAT-001 | Verificado |
| RNF-ARQ-001 | Domain no depende de infraestructura o presentación. | ADR-0002 | No aplica | Referencias de proyectos | PR-ARQ-001 | Verificado |
| RNF-MON-001 | La conversión histórica utiliza CRC por USD y redondeo uniforme. | ADR-0005 | No aplica | `ExchangeRate` | PR-MON-001 a PR-MON-004 | Verificado |
| RNF-DOC-001 | Cada incremento actualiza código, pruebas y documentación relacionada. | Instructivo | No aplica | Documentos `docs/` | Revisión por incremento | En desarrollo |

## Matriz del incremento de catálogos

El incremento fue verificado mediante pruebas unitarias, integración HTTP, inspección del modelo EF y
tres escenarios transaccionales contra SQL Server. La evidencia física se registra como `EV-DAT-002`.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CAT-001 | Gerencia, Finanzas y Asistente pueden consultar, buscar y filtrar los catálogos autorizados. | Alcance aprobado | UC-CAT-001 | Servicios `SearchAsync` y vistas `Index` | PR-CAT-001, PR-CAT-008 | Verificado |
| RF-CAT-002 | Finanzas y Asistente pueden crear, modificar, desactivar y reactivar clientes. | Alcance aprobado | UC-CAT-002 | `CustomersController` / `CustomerCatalogService` | PR-CAT-002, PR-CAT-007, PR-CAT-008 | Verificado |
| RF-CAT-003 | Finanzas y Asistente pueden crear, modificar, desactivar y reactivar proveedores. | Alcance aprobado | UC-CAT-003 | `SuppliersController` / `SupplierCatalogService` | PR-CAT-003, PR-CAT-007, PR-CAT-008 | Verificado |
| RF-CAT-004 | Finanzas puede administrar el catálogo único de productos y servicios y sus precios de referencia. | Alcance aprobado | UC-CAT-004 | `CatalogItemsController` / `CatalogItemService` | PR-CAT-004, PR-CAT-008 | Verificado |
| RF-CAT-005 | Finanzas puede administrar categorías financieras jerárquicas de ingreso y gasto. | Alcance aprobado | UC-CAT-005 | `FinancialCategoriesController` / `FinancialCategoryService` | PR-CAT-005, PR-CAT-008 | Verificado |
| RF-CAT-006 | Finanzas puede administrar cuentas financieras de caja o banco. Reemplazado por RF-CTA-001: las cajas y los bancos son ahora cuentas contables de efectivo (ADR-0012). | Alcance aprobado | UC-CAT-006 | Retirado: `FinancialAccount` fue sustituido por `LedgerAccount` | Retiradas con su código | Descartado |
| RF-MON-001 | Gerencia, Finanzas y Asistente pueden consultar los tipos de cambio por fecha. | ADR-0005 | UC-MON-001 | `ExchangeRatesController` / `DailyExchangeRateService` | PR-CAT-001 | Verificado |
| RF-MON-002 | Finanzas puede registrar y corregir una tasa manual por fecha, indicando su fuente. | Alcance aprobado | UC-MON-001 | `ExchangeRatesController` / `DailyExchangeRateService` | PR-MON-005, PR-MON-006, PR-MON-007 | Verificado |
| RN-CAT-001 | Cliente y proveedor son entidades separadas aun cuando representen a la misma persona física o jurídica. | ADR-0006 | UC-CAT-002, UC-CAT-003 | `Customer` y `Supplier` | PR-CAT-002, PR-CAT-003 | Verificado |
| RN-CAT-002 | Los registros maestros se desactivan o reactivan; no se eliminan físicamente. | ADR-0007 | UC-CAT-002 a UC-CAT-006 | Entidades y acciones `SetActive` | PR-CAT-007 | Verificado |
| RN-CAT-003 | Cada artículo del catálogo es exactamente Producto o Servicio y no administra existencias. | Alcance aprobado | UC-CAT-004 | `CatalogItem` | PR-CAT-004 | Verificado |
| RN-CAT-004 | Los precios CRC y USD son opcionales e independientes; cualquier precio informado debe ser positivo y admite cuatro decimales. | Alcance aprobado | UC-CAT-004 | `CatalogItem` y checks SQL | PR-CAT-004 | Verificado |
| RN-CAT-005 | Un producto o servicio puede indicar opcionalmente una categoría activa de ingreso predeterminada. | Alcance aprobado | UC-CAT-004 | `CatalogItemService` | PR-CAT-004 | Verificado |
| RN-CAT-006 | El tipo Ingreso/Gasto de una categoría es fijo; su padre debe tener el mismo tipo y la jerarquía no admite autorreferencias ni ciclos. | Alcance aprobado | UC-CAT-005 | `FinancialCategoryService` | PR-CAT-005 | Verificado |
| RN-CAT-007 | Una cuenta financiera posee código, tipo Caja/Banco y moneda CRC/USD; tipo y moneda no cambian después de crearla y no almacena saldo. Reemplazado por RN-CTA-001 y RN-CTA-002 (ADR-0012). | Alcance aprobado | UC-CAT-006 | Retirado: `FinancialAccount` | Retiradas con su código | Descartado |
| RN-CAT-008 | El código es obligatorio, se normaliza en mayúsculas y es único dentro de cada catálogo. | Control de integridad | UC-CAT-002 a UC-CAT-006 | Entidades e índices SQL | PR-CAT-009 | Verificado |
| RN-CAT-009 | Una categoría no se desactiva si tiene hijas activas o artículos activos que la usan; reactivar una hija exige que su padre esté activo. | Integridad jerárquica | UC-CAT-005 | `FinancialCategoryService` | PR-CAT-005 | Verificado |
| RN-MON-001 | La tasa expresa CRC por USD, es positiva y conserva seis decimales. | ADR-0005 | UC-MON-001 | `ExchangeRate` / `DailyExchangeRate` | PR-MON-005, PR-MON-007 | Verificado |
| RN-MON-002 | Solo puede existir una tasa operativa por fecha de negocio y su fuente es obligatoria. | Alcance aprobado | UC-MON-001 | Entidad e índice `UX_TiposCambioDiarios_EffectiveDate` | PR-MON-006 | Verificado |
| RN-MON-003 | Corregir una tasa maestra no recalculará documentos confirmados, los cuales conservarán la tasa utilizada. | ADR-0005 | UC-MON-001 | Uso futuro por movimientos | Prueba en incremento transaccional | Aprobado |
| RN-SEG-004 | Finanzas y Asistente mantienen clientes/proveedores; solo Finanzas modifica artículos, precios, categorías, cuentas contables, tasas, tipos de impuesto y
tipos de retención. | Alcance aprobado | UC-CAT-002 a UC-MON-001 | `AuthorizationExtensions` | PR-CAT-008 | Verificado |
| RNF-CAT-001 | Los listados utilizan orden estable, búsqueda, filtro de estado y paginación del lado del servidor. | Calidad de uso | UC-CAT-001 | `CatalogQuery`, `PageQuery` y servicios SQL | PR-CAT-001 | Verificado |
| RNF-DAT-002 | SQL Server refuerza valores permitidos, positividad, unicidad y relaciones mediante restricciones e índices. | Control de integridad | Todos | Configuraciones EF Core | PR-CAT-009, PR-MON-006, EV-DAT-002 | Verificado |
| RNF-DAT-003 | Los catálogos mutables detectan actualizaciones concurrentes mediante `rowversion`. | ADR-0007 | Casos de mantenimiento | Configuraciones EF Core y servicios | PR-CAT-010 | Verificado |
| RNF-AUD-001 | Creación, modificación y, cuando corresponde, cambio de estado conservan usuario responsable e instante UTC. | ADR-0007 | Casos de mantenimiento | Entidades y FKs de auditoría | PR-CAT-007 | Verificado |

Las reglas aún pendientes sobre impuestos, saldos iniciales y documentos financieros se agregarán en
sus respectivos incrementos; no forman parte de estos catálogos.

## Matriz del incremento de facturación y cuentas por cobrar

Este incremento incorpora facturas administrativas internas; no emite comprobantes electrónicos ni
realiza comunicación con Hacienda. Las tarifas de IVA se administran como datos de negocio y su uso
debe ser revisado por la empresa y su asesoría contable antes de confirmar documentos reales.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-INV-001 | Finanzas y Asistente pueden preparar facturas administrativas en CRC o USD para clientes activos. | Alcance aprobado | UC-INV-001 | Módulo `Invoices` | PR-INV-001 | En desarrollo |
| RF-INV-002 | Una factura conserva líneas de productos o servicios y permite descripciones de venta no catalogadas. | Alcance aprobado | UC-INV-001 | `Invoice` / `InvoiceLine` | PR-INV-002 | En desarrollo |
| RF-INV-003 | Finanzas confirma facturas y el sistema conserva cliente, precios, impuestos y tasa de cambio como datos históricos. | ADR-0005 | UC-INV-002 | Servicio de confirmación | PR-INV-003 | Propuesto |
| RF-INV-004 | Gerencia consulta facturas, Finanzas las confirma y Gerencia anula documentos confirmados sin borrado físico. | Políticas vigentes | UC-INV-002, UC-INV-003 | Políticas financieras | PR-INV-004 | Propuesto |
| RF-INV-005 | La factura confirmada puede visualizarse y exportarse a PDF administrativo. | Alcance aprobado | UC-INV-004 | Vista de detalle y exportador | PR-INV-005 | Propuesto |
| RF-INV-006 | Finanzas registra cobros hasta el límite de autorización y Gerencia los que lo superan, en la misma moneda de la factura; el sistema informa el saldo pendiente. Asistente no registra cobros. | ADR-0005, límite de autorización | UC-INV-005 | `InvoicePayment` | PR-INV-006 | Propuesto |
| RF-INV-007 | Finanzas y Asistente pueden modificar el encabezado y agregar, reemplazar o quitar líneas mientras la factura es borrador. | ADR-0010 | UC-INV-001 | `Invoice.UpdateHeader`, `AddLine`, `ReplaceLine` y `RemoveLine` | `InvoiceDraftEditingTests` | En desarrollo |
| RN-INV-001 | Las facturas siguen el ciclo Borrador, Confirmada y Anulada; una confirmada o anulada no admite edición. | Control interno | UC-INV-001 a UC-INV-003 | `InvoiceStatus` | PR-INV-001, PR-INV-003, `InvoiceDraftEditingTests` | En desarrollo |
| RN-INV-002 | Cada línea conserva descripción, unidad, precio, descuentos e impuestos como una fotografía; los cambios posteriores de catálogos no modifican la factura. | Trazabilidad | UC-INV-001, UC-INV-002 | Líneas y detalles de impuesto | PR-INV-002 | Propuesto |
| RN-INV-003 | Los impuestos se calculan por línea y se conservan con código, nombre, tipo de cálculo, tarifa, base gravable, monto utilizado y el tipo de impuesto del catálogo del que provienen. | Ministerio de Hacienda / alcance interno | UC-INV-001 | Catálogo de tipos de impuesto y detalles históricos | PR-INV-002, `InvoiceLineTaxTests` | En desarrollo |
| RN-INV-004 | Los importes y pagos de una factura usan una única moneda; una factura USD confirmada conserva su tasa CRC por USD. | ADR-0005 | UC-INV-002, UC-INV-005 | `CurrencyCode` y tasa histórica | PR-INV-003, PR-INV-006 | Propuesto |
| RN-INV-005 | No se anula una factura con cobros vigentes; estos deben revertirse primero. | Control interno | UC-INV-003, UC-INV-005 | Validación de estado de cobros | PR-INV-004, PR-INV-006 | Propuesto |
| RN-INV-006 | El saldo por cobrar se calcula a partir del total confirmado menos los cobros vigentes; no se almacena como valor editable. | Control de integridad | UC-INV-005 | Consulta de cuentas por cobrar | PR-INV-006 | Propuesto |
| RN-INV-007 | Una factura es de contado o a crédito. La de contado no tiene fecha de vencimiento; la de crédito la exige y no puede ser anterior a la fecha de emisión. | ADR-0010 | UC-INV-001 | `PaymentTerm`, `Invoice` y restricciones `CK_Facturas_PaymentTerm` y `CK_Facturas_DueDate` | `InvoicePaymentTermTests` | Verificado en dominio |
| RN-INV-008 | Importes, impuestos y totales se redondean a 2 decimales con `MidpointRounding.AwayFromZero`; cantidades, precios unitarios y tarifas usan 4 decimales, y la tasa CRC por USD, 6. | ADR-0010 | UC-INV-001, UC-INV-002 | `DomainRules`, `Invoice`, `InvoiceLine`, `InvoiceLineTax` y columnas `decimal(18,2)` | `InvoiceRoundingTests` | Verificado en dominio |
| RN-INV-009 | La base gravable de cada impuesto es el neto de su línea (bruto menos descuento) y la calcula la propia línea; no se recibe de fuera. | ADR-0010 | UC-INV-001 | `InvoiceLine`, `InvoiceTaxSpecification` e `InvoiceLineTax` | `InvoiceLineTaxTests` | Verificado en dominio |
| RN-INV-010 | En una factura con líneas no se puede cambiar la moneda. La fecha de emisión y el resto del encabezado solo se modifican mientras es borrador. | ADR-0010 | UC-INV-001 | `Invoice.UpdateHeader` | `InvoiceDraftEditingTests` | Verificado en dominio |
| RN-INV-011 | Las líneas de una factura conservan el orden de captura mediante una posición consecutiva desde 1, única por factura; reemplazar una línea conserva su posición y quitarla renumera las siguientes. | ADR-0010 | UC-INV-001, UC-INV-004 | `InvoiceLine.Position`, `Invoice` y el índice `UX_FacturaLineas_InvoiceId_Position` | `InvoiceLinePositionTests`, `SqlServerInvoiceScenarioTests` | Verificado en dominio y en SQL Server |

## Matriz del incremento de tipos de impuesto

El catálogo de tipos de impuesto alimenta el cálculo por línea de las facturas administrativas. Las
tarifas de referencia son datos configurables, no reglas de facturación, y su uso debe ser validado por
la empresa y su asesoría contable (ADR-0009 y ADR-0011).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-IMP-001 | Gerencia, Finanzas y Asistente consultan los tipos de impuesto; Finanzas los crea, modifica, desactiva y reactiva sin borrado físico. | ADR-0011 | UC-CAT-007 | `TaxTypesController` / `TaxTypeService` | `TaxTypesControllerTests`, `CatalogAuthorizationTests`, `SqlServerTaxTypeScenarioTests` | Verificado |
| RF-IMP-002 | Finanzas puede cargar las tarifas de IVA de referencia; solo se crean las que faltan y las existentes no se modifican ni se reactivan. | ADR-0009, ADR-0011 | UC-CAT-007 | `ReferenceTaxTypes` y `TaxTypeService.LoadReferenceTaxTypesAsync` | `ReferenceTaxTypesTests`, `SqlServerTaxTypeScenarioTests` | Verificado |
| RN-IMP-001 | El código es único y en mayúsculas; la tarifa no es negativa, conserva cuatro decimales y un impuesto porcentual no supera 100 %. | ADR-0011 | UC-CAT-007 | `TaxType`, `CK_TiposImpuesto_Rate` y `UX_TiposImpuesto_Code` | `TaxTypeTests`, `SqlServerTaxTypeScenarioTests` | Verificado |
| RN-IMP-002 | El método de cálculo (porcentaje o monto fijo por unidad) no cambia después de crear el tipo y nunca se toma del formulario de edición. | ADR-0011 | UC-CAT-007 | `TaxType.UpdateDetails` y `TaxTypesController.Edit` | `TaxTypeTests`, `TaxTypesControllerTests` | Verificado |
| RN-IMP-003 | Modificar o desactivar un tipo no altera las facturas existentes, cuyas líneas conservan su propia fotografía; la base impide eliminar un tipo que una línea ya utilizó. | ADR-0011 | UC-CAT-007, UC-INV-001 | `InvoiceLineTax.TaxTypeId` y `FK_FacturaLineaImpuestos_TiposImpuesto` | `InvoiceLineTaxTests`, `SqlServerInvoiceScenarioTests` | Verificado |
| RN-IMP-004 | Las tarifas de referencia se identifican como datos sujetos a validación contable y solo se cargan cuando Finanzas lo solicita mediante una acción POST. | ADR-0011 | UC-CAT-007 | `ReferenceTaxTypes` y acción `LoadReference` | `ReferenceTaxTypesTests`, `CatalogAuthorizationTests` | Verificado |

## Matriz del incremento de cuentas contables

El catálogo de cuentas contables unifica la clasificación contable con las cajas y los bancos. Es solo
clasificación: el sistema no lleva partida doble, asientos ni saldos contables (ADR-0012).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CTA-001 | Gerencia, Finanzas y Asistente consultan las cuentas contables; Finanzas las crea, modifica, activa y desactiva, sin borrado físico. | ADR-0012 | UC-CAT-006 | `LedgerAccountsController` / `LedgerAccountService` | `LedgerAccountsControllerTests`, `CatalogAuthorizationTests`, `SqlServerLedgerAccountScenarioTests`, `SqlServerCatalogPagesTests` | Verificado |
| RF-CTA-002 | Finanzas asigna una cuenta contable a cada categoría financiera de ingreso o de gasto. | ADR-0012 | UC-CAT-005 | `FinancialCategoriesController` / `FinancialCategoryService` | `FinancialCategoriesControllerTests`, `SqlServerLedgerAccountScenarioTests`, `SqlServerCatalogPagesTests` | Verificado |
| RF-CTA-003 | El servicio ofrece las cajas y bancos activos, opcionalmente de una moneda, para elegir la cuenta de efectivo de ingresos y pagos futuros. | ADR-0012 | UC-CAT-006 | `ILedgerAccountService.GetActiveCashAccountsAsync` | `SqlServerLedgerAccountScenarioTests` | Implementado |
| RN-CTA-001 | Toda cuenta tiene un tipo (Activo, Pasivo, Patrimonio, Ingreso o Gasto); tipo, subtipo de efectivo y moneda no cambian después de crearla, y nunca se toman del formulario de edición. | ADR-0012 | UC-CAT-006 | `LedgerAccount` y `LedgerAccountsController.Edit` | `LedgerAccountTests`, `LedgerAccountsControllerTests` | Verificado |
| RN-CTA-002 | Una cuenta de efectivo es de tipo Activo, tiene subtipo Caja o Banco y una moneda CRC/USD; una que no es de efectivo no tiene moneda. Una cuenta de efectivo no puede tener cuentas hijas. | ADR-0012 | UC-CAT-006 | `LedgerAccount`, `LedgerAccountService` y `CK_CuentasContables_Cash` | `LedgerAccountTests`, `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-CTA-003 | La cuenta superior existe, está activa, es del mismo tipo y no produce ciclos; no se desactiva una cuenta con hijas activas y reactivar una hija exige la superior activa. | ADR-0012 | UC-CAT-006 | `LedgerAccountService` y `CK_CuentasContables_Parent` | `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-CTA-004 | Toda categoría apunta a una cuenta contable activa de su mismo tipo (Ingreso o Gasto); una cuenta usada por categorías activas no se desactiva y la base impide eliminarla. | ADR-0012 | UC-CAT-005, UC-CAT-006 | `FinancialCategory.LedgerAccountId`, `FinancialCategoryService` y `FK_CategoriasFinancieras_CuentasContables` | `FinancialCategoryTests`, `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-CTA-005 | La cuenta contable no almacena saldo ni asientos: los saldos se calcularán desde movimientos confirmados. | ADR-0012, alcance aprobado | UC-CAT-006 | `LedgerAccount` | `LedgerAccountTests` | Verificado |
| RN-CTA-006 | Las cajas y bancos existentes se migraron a cuentas contables de efectivo con el mismo identificador, y las categorías existentes se asignaron a las cuentas raíz «Ingresos» o «Gastos». La tabla anterior se eliminó solo tras verificar que cada fila tenía su equivalente. | ADR-0012 | No aplica | Migraciones `AddLedgerAccounts`, `AddCategoryLedgerAccount` y `DropFinancialAccounts` | Verificada en bases de prueba con datos de ejemplo, incluido el caso en que falta un equivalente | Verificado |

## Matriz del incremento de tipos de retención

El catálogo de tipos de retención es el complemento del de tipos de impuesto: alimentará el registro de
abonos y pagos. A diferencia de un impuesto, una retención es siempre un porcentaje, sin monto fijo por
unidad. La base y el monto de cada retención concreta no viven en este catálogo, sino en el abono o el
pago al que se aplique (regla de negocio 3).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-RET-001 | Gerencia, Finanzas y Asistente consultan los tipos de retención; Finanzas los crea, modifica, desactiva y reactiva sin borrado físico. | Regla de negocio 3 | UC-CAT-008 | `WithholdingTypesController` / `WithholdingTypeService` | `WithholdingTypesControllerTests`, `CatalogAuthorizationTests`, `SqlServerWithholdingTypeScenarioTests` | Verificado |
| RN-RET-001 | El código es único y en mayúsculas; la tarifa es siempre porcentual, no es negativa, conserva cuatro decimales y no supera 100 %. | Regla de negocio 3 | UC-CAT-008 | `WithholdingType`, `CK_TiposRetencion_Rate` y `UX_TiposRetencion_Code` | `WithholdingTypeTests`, `SqlServerWithholdingTypeScenarioTests` | Verificado |
| RN-RET-002 | Modificar o desactivar un tipo de retención no altera los abonos o pagos que ya lo usaron. | Regla de negocio 3 | UC-CAT-008 | Pendiente: se verificará junto con la fotografía de la retención al construir abonos y pagos | Pendiente | Propuesto |

## Matriz del incremento de parámetros del sistema

Cubre las reglas de negocio 7 (límite de autorización) y 9 (días de alerta de vencimiento). No es un
catálogo: existe como máximo una fila, y su bitácora es un historial propio de cambios, distinto de la
bitácora de seguridad de cuentas de usuario.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CFG-001 | Gerencia y Finanzas consultan los parámetros vigentes; si no se han configurado, el sistema lo indica. | Regla de negocio 7 y 9 | UC-CFG-001 | `SystemParametersController.Index` / `SystemParametersService.GetAsync` | `SystemParametersControllerTests` | Verificado |
| RF-CFG-002 | Gerencia configura o corrige el límite de autorización en CRC y los días de alerta de vencimiento, con control de concurrencia. | Regla de negocio 7 y 9 | UC-CFG-001 | `SystemParametersController.Save` / `SystemParametersService` | `SystemParametersControllerTests`, `SqlServerSystemParametersScenarioTests` | Verificado |
| RN-CFG-001 | El límite de autorización no es negativo y se redondea a dos decimales; los días de alerta son un entero mayor que cero. | Regla de negocio 7 y 9 | UC-CFG-001 | `SystemParameters`, `CK_ParametrosSistema_AuthorizationLimit` y `CK_ParametrosSistema_OverdueAlertDays` | `SystemParametersTests`, `SqlServerSystemParametersScenarioTests` | Verificado |
| RN-CFG-002 | Solo Gerencia puede crear o modificar los parámetros; Finanzas, Asistente y Administrador solo consultan o no acceden. | Tabla de perfiles | UC-CFG-001 | `SystemPolicies.ManageSystemParameters` | `CatalogAuthorizationTests` | Verificado |
| RN-CFG-003 | Cada cambio queda en un historial de solo inserción, con el valor anterior y el nuevo de cada campo; el primer registro no tiene valor anterior. | Convenciones de datos («cambios de parámetros quedan en la bitácora») | UC-CFG-001 | `SystemParameterChange` y `FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated` | `SystemParameterChangeTests`, `SqlServerSystemParametersScenarioTests` | Verificado |

## Matriz del incremento de administración de usuarios

El incremento extiende Identity sin agregar registro ni recuperación pública. Los roles continúan
siendo valores fijos y la bitácora de seguridad no conserva contraseñas, tokens ni hashes.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-USR-001 | Administrador puede listar, buscar y filtrar usuarios por estado y rol mediante paginación del servidor. | ADR-0008 | UC-USR-001 | `UsersController.Index` / `UserAdministrationService.SearchAsync` | PR-USR-001, PR-USR-010 | Verificado |
| RF-USR-002 | Administrador puede crear una cuenta activa con nombre, correo único, contraseña temporal y un rol aprobado. | ADR-0008 | UC-USR-001 | `UsersController.Create` / `UserAdministrationService.CreateAsync` | PR-USR-002 | Verificado |
| RF-USR-003 | Administrador puede modificar nombre, correo y rol de una cuenta existente. | ADR-0008 | UC-USR-001 | `UsersController.Edit` / `UserAdministrationService.UpdateAsync` | PR-USR-003 | Verificado |
| RF-USR-004 | Administrador puede desactivar y reactivar cuentas sin borrarlas físicamente. | ADR-0008 | UC-USR-001 | `UsersController.SetActive` | PR-USR-004, PR-USR-010 | Verificado |
| RF-USR-005 | Administrador puede asignar una contraseña temporal sin conocer ni exponer el hash almacenado. | ADR-0008 | UC-USR-002 | `UsersController.ResetPassword` | PR-USR-005 | Verificado |
| RF-USR-006 | Administrador puede eliminar un bloqueo temporal sin cambiar la contraseña. | Control interno | UC-USR-002 | `UsersController.Unlock` | PR-USR-006 | Verificado |
| RF-USR-007 | Una cuenta con contraseña temporal debe cambiarla antes de utilizar cualquier otro módulo. | ADR-0008 | UC-SEG-003 | `MustChangePassword` y middleware web | PR-USR-007 | Verificado |
| RN-USR-001 | Cada usuario posee exactamente uno de los cuatro roles aprobados; los roles no tienen CRUD. | ADR-0008 | UC-USR-001 | `SystemRoles` / `UserAdministrationService` | PR-USR-002, PR-USR-003 | Verificado |
| RN-USR-002 | Correo y nombre de usuario coinciden y son únicos después de la normalización de Identity. | Seguridad | UC-USR-001 | `UserManager` e índices Identity | PR-USR-002 | Verificado |
| RN-USR-003 | No puede desactivarse ni degradarse al último Administrador activo. | Disponibilidad | UC-USR-001 | `UserAdministrationService` | PR-USR-008 | Verificado |
| RN-USR-004 | Un Administrador no puede desactivarse, cambiar su rol ni restablecerse desde el flujo administrativo. | Menor privilegio | UC-USR-001/002 | `UserAdministrationService` | PR-USR-008 | Verificado |
| RN-USR-005 | Inactividad y bloqueo temporal son estados distintos; reactivar no implica desbloquear. | Control interno | UC-USR-001/002 | Identity y acciones separadas | PR-USR-004, PR-USR-006 | Verificado |
| RN-USR-006 | Cambiar correo, rol, estado o contraseña invalida las sesiones anteriores de la cuenta afectada. | Seguridad | UC-USR-001/002 | `SecurityStamp` | PR-USR-003 a PR-USR-006 | Verificado |
| RNF-USR-001 | Operaciones compuestas son transaccionales y los formularios obsoletos se rechazan mediante `ConcurrencyStamp`. | Integridad | UC-USR-001/002 | Servicio Identity y EF Core | PR-USR-009 | Verificado |
| RNF-USR-002 | Cambios administrativos registran acción, actor, objetivo e instante UTC sin secretos. | Auditoría | UC-USR-001/002 | `EventosSeguridad` | PR-USR-002 a PR-USR-006 | Verificado |
| RNF-USR-003 | No existen endpoints públicos de registro, recuperación de contraseña o borrado de usuarios. | Superficie mínima | Todos | Rutas MVC explícitas | PR-USR-010 | Verificado |
