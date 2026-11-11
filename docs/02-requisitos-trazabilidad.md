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
| RN-SEG-005 | Asistente consulta y registra ingresos manuales, consulta gastos propios y cuentas por cobrar y pagar; no confirma gastos, registra abonos o pagos, anula movimientos ni consulta asientos, reportes o presupuesto. Su sidebar oculta Catálogo contable e Impuestos y retenciones sin cambiar la autorización del servidor. | Cambios aprobados por el usuario (2026-11-06 y 2026-11-07) | UC-INV-005, UC-GAS-001, UC-GAS-004 | `RegisterManualIncome`, `ViewReceivablesAndPayables`, `ExpensesController`, `IncomesController`, `ReceivablesController`, `PayablesController`, `Views/Shared/_Layout.cshtml` | `AssistantFinancialAccessTests`, `ExpensesAuthorizationTests`, `IncomesAuthorizationTests`, `ReceivablesAuthorizationTests`, `PayablesAuthorizationTests` | Verificado |
| RNF-SEG-001 | Las contraseñas se administran con Identity y nunca se almacenan en texto. | Seguridad | Todos | ASP.NET Core Identity | PR-SEG-003 | Verificado |
| RNF-SEG-002 | Cinco fallos bloquean la cuenta durante 15 minutos. | Seguridad | UC-SEG-001 | Opciones de Identity | PR-SEG-003 | Verificado |
| RNF-DAT-001 | La persistencia utiliza SQL Server Express mediante EF Core. | Anteproyecto | No aplica | `FinancialDbContext` | EV-DAT-001 | Verificado |
| RNF-ARQ-001 | Domain no depende de infraestructura o presentación. | ADR-0002 | No aplica | Referencias de proyectos | PR-ARQ-001 | Verificado |
| RNF-MON-001 | La conversión histórica utiliza CRC por USD y redondeo uniforme. Retirado: el sistema opera solo en colones, sin tipo de cambio (regla 5). | ADR-0005 | No aplica | Retirado: `ExchangeRate` y `DailyExchangeRate` | Retiradas con su código | Descartado |
| RNF-DOC-001 | Cada incremento actualiza código, pruebas y documentación relacionada. | Instructivo | No aplica | Documentos `docs/` | Revisión por incremento | En desarrollo |

## Matriz del incremento de catálogos

El incremento fue verificado mediante pruebas unitarias, integración HTTP, inspección del modelo EF y
tres escenarios transaccionales contra SQL Server. La evidencia física se registra como `EV-DAT-002`.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CAT-001 | Gerencia, Finanzas y Asistente pueden consultar y filtrar por columna los catálogos autorizados; los filtros se aplican con Enter o el botón de búsqueda, se limpian para volver a la lista completa y se conservan al paginar. | Alcance aprobado | UC-CAT-001 | `CustomersController.Index`, `SuppliersController.Index`, `CatalogItemsController.Index`, consultas `CatalogQuery` y vistas `Index` | `SqlServerCatalogScenarioTests`, `CatalogAuthorizationTests` | Verificado |
| RF-CAT-002 | Gerencia, Finanzas y Asistente pueden crear, modificar, desactivar y reactivar clientes. | Alcance aprobado; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-002 | `CustomersController` / `CustomerCatalogService` | PR-CAT-002, PR-CAT-007, PR-CAT-008 | Verificado |
| RF-CAT-003 | Gerencia, Finanzas y Asistente pueden crear, modificar, desactivar y reactivar proveedores. | Alcance aprobado; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-003 | `SuppliersController` / `SupplierCatalogService` | PR-CAT-003, PR-CAT-007, PR-CAT-008 | Verificado |
| RF-CAT-004 | Gerencia y Finanzas pueden administrar el catálogo único de productos y servicios y sus precios de referencia. | Alcance aprobado; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-004 | `CatalogItemsController` / `CatalogItemService` | PR-CAT-004, PR-CAT-008 | Verificado |
| RF-CAT-005 | Gerencia y Finanzas pueden administrar categorías financieras jerárquicas de ingreso y gasto. | Alcance aprobado; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-005 | `FinancialCategoriesController` / `FinancialCategoryService` | PR-CAT-005, PR-CAT-008 | Verificado |
| RF-CAT-006 | Finanzas puede administrar cuentas financieras de caja o banco. Reemplazado por RF-CTA-001: las cajas y los bancos son ahora cuentas contables de efectivo (ADR-0012). | Alcance aprobado | UC-CAT-006 | Retirado: `FinancialAccount` fue sustituido por `LedgerAccount` | Retiradas con su código | Descartado |
| RF-MON-001 | Gerencia, Finanzas y Asistente pueden consultar los tipos de cambio por fecha. Retirado: el sistema opera solo en colones (regla 5). | ADR-0005 | UC-MON-001 | Retirado: `ExchangeRatesController` / `DailyExchangeRateService` | Retiradas con su código | Descartado |
| RF-MON-002 | Finanzas puede registrar y corregir una tasa manual por fecha, indicando su fuente. Retirado: el sistema opera solo en colones (regla 5). | Alcance aprobado | UC-MON-001 | Retirado: `ExchangeRatesController` / `DailyExchangeRateService` | Retiradas con su código | Descartado |
| RF-50 | En clientes y proveedores, la identificación es obligatoria y única dentro de cada catálogo. | Regla de negocio 12, documento académico | UC-CAT-002, UC-CAT-003 | `Customer`, `Supplier` y los índices `UX_Clientes_Identification`/`UX_Proveedores_Identification` | `BusinessPartyCatalogTests`, `SqlServerCatalogScenarioTests` | Verificado |
| RF-51 | El código de clientes, proveedores, productos/servicios y categorías financieras lo asigna el sistema al crear, a partir de una secuencia de SQL Server por catálogo; el usuario no lo escribe y no puede cambiarlo después. Ampliado a ingresos (`ING-001`) y gastos (`GAS-001`), ver RF-GAS-001. | Regla de negocio 12 | UC-CAT-002 a UC-CAT-005 | `CatalogPersistence.GenerateSequentialCodeAsync` y los servicios de cada catálogo | `SqlServerCatalogScenarioTests` | Verificado |
| RF-52 | Un producto o servicio puede indicar un precio de referencia y una tarifa de IVA predeterminada activa; al agregarlo a una línea de factura, ambos se aplican directamente y solo se piden en la línea si el artículo no los tiene (ADR-0043). | Regla de negocio 3 (RF-52); ADR-0043 | UC-CAT-004 | `CatalogItem.ReferencePriceCrc`/`DefaultTaxTypeId`, `CatalogItemService`, `CatalogItemOption`, `InvoiceService.ResolveLineInputAsync`, `InvoicesController._LineForm.cshtml` (atributos `data-reference-price`/`data-default-tax-type`) y `site.js` | `CatalogItemTests`, `CatalogViewModelValidationTests`, `SqlServerInvoiceServiceScenarioTests.AddLineAsync_WithACatalogItemWithItsOwnPriceAndTax_IgnoresManualPriceAndTaxAndCopiesTheItemsData` | Verificado |
| RN-CAT-010 | El proveedor puede indicar opcionalmente su actividad económica. | RF-51, documento académico | UC-CAT-003 | `Supplier.EconomicActivity` | `BusinessPartyCatalogTests` | Verificado |
| RN-CAT-011 | El tipo (Producto/Servicio) de un artículo del catálogo no cambia después de crearlo, porque el código ya lo codifica (`PRD-`/`SRV-`). | Regla de negocio 12 | UC-CAT-004 | `CatalogItem.UpdateDetails`, `CreateCatalogItemCommand`/`UpdateCatalogItemCommand` | `CatalogItemTests` | Verificado |
| RN-CAT-001 | Cliente y proveedor son entidades separadas aun cuando representen a la misma persona física o jurídica. | ADR-0006 | UC-CAT-002, UC-CAT-003 | `Customer` y `Supplier` | PR-CAT-002, PR-CAT-003 | Verificado |
| RN-CAT-002 | Los registros maestros se desactivan o reactivan; no se eliminan físicamente. | ADR-0007 | UC-CAT-002 a UC-CAT-006 | Entidades y acciones `SetActive` | PR-CAT-007 | Verificado |
| RN-CAT-003 | Cada artículo del catálogo es exactamente Producto o Servicio y no administra existencias. | Alcance aprobado | UC-CAT-004 | `CatalogItem` | PR-CAT-004 | Verificado |
| RN-CAT-004 | El precio de referencia en colones es opcional; cuando se informa debe ser positivo y admite cuatro decimales. Ya no existe un precio de referencia en USD (regla 5). | Alcance aprobado | UC-CAT-004 | `CatalogItem` y checks SQL | PR-CAT-004 | Verificado |
| RN-CAT-005 | Un producto o servicio puede indicar opcionalmente una categoría activa de ingreso predeterminada. | Alcance aprobado | UC-CAT-004 | `CatalogItemService` | PR-CAT-004 | Verificado |
| RN-CAT-006 | El tipo Ingreso/Gasto de una categoría es fijo; su padre debe tener el mismo tipo y la jerarquía no admite autorreferencias ni ciclos. | Alcance aprobado | UC-CAT-005 | `FinancialCategoryService` | PR-CAT-005 | Verificado |
| RN-CAT-007 | Una cuenta financiera posee código, tipo Caja/Banco y moneda CRC/USD; tipo y moneda no cambian después de crearla y no almacena saldo. Reemplazado por RN-CTA-001 y RN-CTA-002 (ADR-0012). | Alcance aprobado | UC-CAT-006 | Retirado: `FinancialAccount` | Retiradas con su código | Descartado |
| RN-CAT-008 | El código es obligatorio, se normaliza en mayúsculas y es único dentro de cada catálogo. | Control de integridad | UC-CAT-002 a UC-CAT-006 | Entidades e índices SQL | PR-CAT-009 | Verificado |
| RN-CAT-009 | Una categoría no se desactiva si tiene hijas activas o artículos activos que la usan; reactivar una hija exige que su padre esté activo. | Integridad jerárquica | UC-CAT-005 | `FinancialCategoryService` | PR-CAT-005 | Verificado |
| RN-MON-001 | La tasa expresa CRC por USD, es positiva y conserva seis decimales. Retirado: el sistema opera solo en colones (regla 5). | ADR-0005 | UC-MON-001 | Retirado: `ExchangeRate` / `DailyExchangeRate` | Retiradas con su código | Descartado |
| RN-MON-002 | Solo puede existir una tasa operativa por fecha de negocio y su fuente es obligatoria. Retirado (regla 5). | Alcance aprobado | UC-MON-001 | Retirado: entidad e índice `UX_TiposCambioDiarios_EffectiveDate` | Retiradas con su código | Descartado |
| RN-MON-003 | Corregir una tasa maestra no recalculará documentos confirmados, los cuales conservarán la tasa utilizada. Retirado (regla 5). | ADR-0005 | UC-MON-001 | Retirado | Retiradas con su código | Descartado |
| RN-SEG-004 | Gerencia, Finanzas y Asistente mantienen clientes/proveedores; Gerencia y Finanzas modifican artículos, precios, categorías, cuentas contables, tipos de impuesto y
tipos de retención. | Alcance aprobado; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-002 a UC-CAT-007 | `AuthorizationExtensions` | PR-CAT-008 | Verificado |
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
| RF-INV-001 | Gerencia, Finanzas y Asistente pueden preparar facturas administrativas en colones (CRC) para clientes activos. | Alcance aprobado; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-INV-001 | `IInvoiceService.CreateAsync`, `InvoicesController` (ruta `/facturacion/facturas`) | `SqlServerInvoiceServiceScenarioTests`, `InvoiceAuthorizationTests` | Verificado |
| RF-INV-002 | Una línea de factura exige un artículo del catálogo; la descripción y la unidad de medida siempre vienen de él, y el precio y la tarifa de IVA también, salvo que el artículo no los tenga definidos. | Decisión del usuario (2026-10-27), ADR-0043 | UC-INV-001 | `Invoice` / `InvoiceLine`, `IInvoiceService.AddLineAsync`/`ReplaceLineAsync` (`CatalogItemId` obligatorio, `ResolveLineInputAsync`) | `SqlServerInvoiceScenarioTests`, `SqlServerInvoiceServiceScenarioTests.AddLineAsync_WithACatalogItemWithItsOwnPriceAndTax_IgnoresManualPriceAndTaxAndCopiesTheItemsData`, `AddLineAsync_WithACatalogItemWithoutAReferencePrice_UsesTheManualUnitPrice`, `AddLineAsync_WithACatalogItemWithoutAReferencePriceAndNoManualPrice_IsRejected`, `AddLineAsync_WithACatalogItemWithoutADefaultTaxTypeAndNoManualTaxType_IsRejected`, `AddLineAsync_WithAnInactiveCatalogItem_IsRejected` | Verificado |
| RF-INV-003 | Gerencia y Finanzas confirman facturas y el sistema conserva cliente, precios e impuestos como datos históricos; además genera la cuenta por cobrar (crédito) o el ingreso (contado) de la factura. | ADR-0005; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-INV-002 | `IInvoiceService.ConfirmAsync`, `Receivable`, `Income.FromInvoice`, política `Movimientos.Confirmar` | `SqlServerInvoiceServiceScenarioTests`, `InvoiceAuthorizationTests` | Verificado |
| RF-INV-004 | Gerencia consulta facturas, Gerencia y Finanzas las confirman y Gerencia anula documentos confirmados sin borrado físico; anular arrastra la cuenta por cobrar o el ingreso que la factura generó. | Políticas vigentes; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-INV-002, UC-INV-003 | Políticas `Movimientos.Ver`, `Movimientos.Confirmar` y `Movimientos.Anular`; `IInvoiceService.CancelAsync` | `InvoiceAuthorizationTests`, `SqlServerInvoiceServiceScenarioTests` | Verificado |
| RF-INV-005 | La factura confirmada o anulada puede visualizarse y exportarse a PDF administrativo, con los datos vigentes de la empresa, el detalle por línea, el resumen de IVA por tarifa, el medio de pago si es de contado, la leyenda de documento interno y, si está anulada, "ANULADA" con el motivo; un borrador no se exporta. | Regla de negocio 15 | UC-INV-004 | Visualización: `InvoicesController.Details`. Exportación a PDF: `InvoicesController.Pdf`, `InvoicePdfDocument` (QuestPDF) | `InvoicePdfDocumentTests.InvoicePdfDocument_ForAConfirmedCashInvoice_GeneratesAValidPdfWithCompanyDataAndPaymentMethod`, `InvoicePdfDocument_ForACreditInvoice_GeneratesAValidPdfWithoutAPaymentMethod`, `InvoicePdfDocument_ForACancelledInvoice_GeneratesAValidPdfWithTheReason`, `InvoicePdfDocument_WithoutCompanyData_GeneratesAValidPdf`, `SqlServerInvoiceServiceScenarioTests.GetAsync_ForAConfirmedCashInvoice_IncludesThePaymentMethodFromItsIncome`, `GetAsync_ForACreditInvoice_HasNoPaymentMethod` | Verificado |
| RF-55 | Gerencia define los datos de la empresa (nombre, cédula jurídica, dirección, teléfono y correo); se muestran en Parámetros y alimentan el encabezado de la factura en PDF; cada cambio queda en su propio historial. | RF-55 | CU-06 | `SystemParameters.UpdateCompany`, `ISystemParametersService.UpdateCompanyAsync`, `CompanyDataChange`, `SystemParametersController.UpdateCompany` (ruta `/parametros`, formulario independiente), política `Parametros.Administrar` | `SystemParametersTests.UpdateCompany_SetsAllFieldsAndTheAudit`, `UpdateCompany_AllowsOptionalFieldsToBeNull`, `UpdateCompany_RejectsAMissingName`, `UpdateCompany_RejectsAMissingLegalId`, `SqlServerSystemParametersScenarioTests.UpdateCompanyAsync_SavesTheDataAndKeepsASnapshotHistory`, `UpdateCompanyAsync_RejectsAMissingLegalId`, `SystemParametersControllerTests.UpdateCompany_CallsServiceAndRedirectsOnSuccess`, `UpdateCompany_WithInvalidData_DoesNotCallTheService`, `UpdateCompany_WithoutAnAuthenticatedUser_IsForbidden` | Verificado |
| RF-INV-006 | Asistente, Finanzas y Gerencia registran abonos (política `RegisterReceivablePayments`); sobre el límite de autorización solo Gerencia; el sistema informa el saldo pendiente. El abono individual se inicia desde el detalle de una cuenta por cobrar. Actualizado: antes el Asistente no registraba abonos (cambio del 09/11/2026). | ADR-0005, límite de autorización, ADR-0056, ADR-0059 | UC-INV-005 | `IReceivablePaymentService.RegisterAsync` (parámetro `actorIsManagement`), `SystemParameters.AuthorizationLimitCrc`, `ReceivablePaymentsController.RegisterSingle` (ruta `/ingresos/abonos/cuenta/{id}`) | `SqlServerReceivablePaymentServiceScenarioTests.Register_ToASingleInvoice_AppliesThePaymentAndCreatesItsIncome`, `SqlServerReceivablePaymentServiceScenarioTests.Register_AboveTheAuthorizationLimitWithoutManagement_IsRejected`, `ReceivablesAuthorizationTests` | Verificado |
| RF-INV-008 | Al confirmar una factura de contado se indican cuenta de efectivo, medio de pago, referencia y categoría, y el sistema genera su ingreso vigente por el total de la factura; a crédito no se pide nada adicional. | Regla de negocio 2 y 10 | UC-INV-002 | `ConfirmInvoiceCommand`, `IInvoiceService.ConfirmAsync`, `Income.FromInvoice`, `InvoicesController.Confirm` (GET/POST) | `IncomeTests`, `SqlServerInvoiceServiceScenarioTests.Lifecycle_CreatesEditsConfirmsAndCancelsThroughTheService` | Verificado |
| RF-INV-009 | Desde el detalle de una cuenta por cobrar, el abono se aplica únicamente a esa cuenta y el cliente se obtiene de ella. El reparto de un cobro entre varias cuentas del mismo cliente se inicia como opción separada en Ingresos: propone de la más antigua a la más nueva, editable, con retención por línea; crea una fila por cuenta con referencia y grupo comunes, y cada abono se anula individualmente. | Regla de negocio 2 (RF-15), ADR-0056 | UC-INV-005 | `ReceivablePayment.GroupId`, `IReceivablePaymentService.RegisterAsync`/`CancelAsync`, `ReceivablePaymentsController.RegisterSingle` y `Start`/`Register` (flujo múltiple desde Ingresos) | `ReceivablePaymentTests`, `SqlServerReceivablePaymentServiceScenarioTests.Register_ToASingleInvoice_AppliesThePaymentAndCreatesItsIncome`, `SqlServerReceivablePaymentServiceScenarioTests.Register_ToSeveralInvoicesOfTheSameCustomer_SharesTheGroupIdAndCancelsIndividually`, `ReceivablesAuthorizationTests` | Verificado |
| RF-INV-007 | Gerencia, Finanzas y Asistente pueden modificar el encabezado y agregar, reemplazar o quitar líneas mientras la factura es borrador. | ADR-0010; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-INV-001 | `Invoice.UpdateHeader`, `AddLine`, `ReplaceLine`, `RemoveLine`; `IInvoiceService`, `InvoicesController.EditHeader`/`AddLine`/`EditLine`/`RemoveLine` | `InvoiceDraftEditingTests`, `SqlServerInvoiceServiceScenarioTests` | Verificado |
| RF-34 | El número consecutivo de la factura se asigna solo al confirmarla, no al guardar el borrador; un borrador no tiene número. | Regla de negocio 12, documento académico (RF-34) | UC-INV-001, UC-INV-002 | `Invoice.Confirm`, columna `Number` nula hasta confirmar y la secuencia `finanzas.Seq_FacturaNumero` | `InvoiceConfirmationTests`, `SqlServerInvoiceScenarioTests` | Verificado |
| RN-INV-001 | Las facturas siguen el ciclo Borrador, Confirmada y Anulada; una confirmada o anulada no admite edición. | Control interno | UC-INV-001 a UC-INV-003 | `InvoiceStatus`, `IInvoiceService`, `InvoicesController` | `InvoiceDraftEditingTests`, `SqlServerInvoiceServiceScenarioTests` | Verificado |
| RN-INV-002 | Cada línea conserva descripción, unidad, precio, descuentos e impuestos como una fotografía; los cambios posteriores de catálogos no modifican la factura. | Trazabilidad | UC-INV-001, UC-INV-002 | Líneas y detalles de impuesto | PR-INV-002 | Propuesto |
| RN-INV-003 | El impuesto de cada línea se calcula sobre su neto y se conserva con código, nombre, tarifa, base gravable, monto y el tipo de impuesto del catálogo del que proviene. | Ministerio de Hacienda / alcance interno | UC-INV-001 | `InvoiceLineTax` | PR-INV-002, `InvoiceLineTaxTests` | En desarrollo |
| RN-INV-004 | La factura opera solo en colones (CRC); no existe selección de moneda ni tasa de cambio (regla 5). | ADR-0005 | UC-INV-002, UC-INV-005 | Retirado: `CurrencyCode` y tasa histórica | Retiradas con su código | Descartado |
| RN-INV-005 | No se anula una factura con abonos vigentes; estos deben anularse primero. | Control interno | UC-INV-003, UC-INV-005 | `Receivable.Cancel` (exige `Balance == OriginalAmount`), `IInvoiceService.CancelAsync` | `ReceivableTests`, `SqlServerInvoiceServiceScenarioTests.Cancel_OnACreditInvoiceWithAnActivePayment_IsBlockedAndKeepsEverythingUnchanged` | Verificado |
| RN-INV-006 | El saldo por cobrar se reduce con cada abono vigente (monto más retención, si la hay) y se restituye al anular uno; la invariante 0 ≤ saldo ≤ monto original se exige en el dominio y se repite como `CHECK` en la base de datos. | Control de integridad | UC-INV-005 | `Receivable.ApplyPayment`, `Receivable.RevertPayment`, `CK_CuentasPorCobrar_Balance` | `ReceivableTests`, `SqlServerReceivablePaymentServiceScenarioTests` | Verificado |
| RN-INV-007 | El plazo de pago es una lista fija de 4 valores (regla 14): contado o crédito a 30, 60 o 90 días. El vencimiento no se digita: el dominio lo calcula como la fecha de emisión más los días del plazo; de contado no tiene vencimiento. | ADR-0010, ADR-0020 | UC-INV-001 | `PaymentTerm`, `PaymentTermDays`, `Invoice.CalculateDueDate` y restricciones `CK_Facturas_PaymentTerm` y `CK_Facturas_DueDate` | `InvoicePaymentTermTests` | Verificado en dominio |
| RN-INV-008 | Importes, impuestos y totales se redondean a 2 decimales con `MidpointRounding.AwayFromZero`; cantidades, precios unitarios y tarifas usan 4 decimales. | ADR-0010 | UC-INV-001, UC-INV-002 | `DomainRules`, `Invoice`, `InvoiceLine`, `InvoiceLineTax` y columnas `decimal(18,2)` | `InvoiceRoundingTests` | Verificado en dominio |
| RN-INV-009 | La base gravable de cada impuesto es el neto de su línea (bruto menos descuento) y la calcula la propia línea; no se recibe de fuera. | ADR-0010 | UC-INV-001 | `InvoiceLine`, `InvoiceTaxSpecification` e `InvoiceLineTax` | `InvoiceLineTaxTests` | Verificado en dominio |
| RN-INV-010 | El cliente, la fecha de emisión y el plazo de pago del encabezado solo se modifican mientras la factura es borrador; cada cambio recalcula el vencimiento desde la nueva fecha de emisión y el plazo elegido (regla 14). | ADR-0010, ADR-0020 | UC-INV-001 | `Invoice.UpdateHeader`, `PaymentTerm` | `InvoiceDraftEditingTests`, `InvoicePaymentTermTests` | Verificado en dominio |
| RN-INV-011 | Las líneas de una factura conservan el orden de captura mediante una posición consecutiva desde 1, única por factura; reemplazar una línea conserva su posición y quitarla renumera las siguientes. | ADR-0010 | UC-INV-001, UC-INV-004 | `InvoiceLine.Position`, `Invoice` y el índice `UX_FacturaLineas_InvoiceId_Position` | `InvoiceLinePositionTests`, `SqlServerInvoiceScenarioTests` | Verificado en dominio y en SQL Server |
| RN-INV-012 | Asistente consulta facturas de cualquier usuario, en cualquier estado, y solo edita sus propios borradores; Finanzas y Gerencia ven y resuelven cualquiera según su política. | Perfiles (Tabla de permisos); ADR-0052 | UC-INV-001 a UC-INV-003 | `InvoicesController` (consulta sin límite por propietario; `OwnerRestriction` en edición), `IInvoiceService` | `SqlServerInvoiceServiceScenarioTests.AssistantCanReadAnotherUsersDraft_ButCannotEditIt`, `.Search_FiltersByStatusAndByOwner` | Verificado |
| RN-INV-013 | Confirmar y anular una factura quedan registrados en la bitácora de operaciones, con el documento, la acción, quién la hizo y cuándo (y el motivo, si fue una anulación). | Regla de negocio 4 y 16 | UC-INV-002, UC-INV-003 | `OperationLogEntry`, `IInvoiceService.ConfirmAsync`/`CancelAsync` (misma transacción que la mutación del dominio) | `OperationLogEntryTests`, `SqlServerInvoiceServiceScenarioTests.Lifecycle_CreatesEditsConfirmsAndCancelsThroughTheService` | Verificado |
| RN-INV-014 | Un abono puede incluir una retención por línea: tipo, porcentaje y nombre quedan como fotografía del catálogo al momento del abono (los cambios posteriores no la alteran); la base es editable (por defecto el neto de la factura) y el monto se calcula sobre ella. | Regla de negocio 3 | UC-INV-005 | `Withholding`, `IReceivablePaymentService.RegisterAsync` | `WithholdingTests`, `SqlServerReceivablePaymentServiceScenarioTests.Register_WithAWithholding_ReducesTheBalanceByTheAmountPlusTheWithholding` | Verificado |
| RN-INV-015 | El sistema advierte, sin bloquear, si un ingreso, un abono, un pago o una factura de contado repite la referencia en la misma cuenta de efectivo. | Regla de negocio 10 | UC-INV-005 | `DuplicateReferenceGuard.ExistsAsync` (Infrastructure/Common), usado por `IReceivablePaymentService.RegisterAsync` y `ISupplierPaymentService.RegisterAsync` (resultado `DuplicateReferenceWarning`), `IIncomeService.CreateManualAsync` (resultado `DuplicateReferenceWarning`) e `IInvoiceService.HasDuplicateCashReferenceAsync` (consultado por `InvoicesController.Confirm` antes de confirmar una factura de contado) | `SqlServerReceivablePaymentServiceScenarioTests.Register_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds`, `SqlServerIncomeServiceScenarioTests.CreateManualAsync_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds`, `SqlServerInvoiceServiceScenarioTests.HasDuplicateCashReferenceAsync_DetectsAReferenceAlreadyUsedByAnIncome` | Verificado |
| RF-INV-010 | Asistente, Finanzas y Gerencia registran ingresos manuales (reintegros, intereses que acredite el banco), nunca préstamos ni aportes, con categoría, cuenta de efectivo, medio de pago y una descripción obligatoria que explica su origen; solo Gerencia anula ingresos manuales, nunca los que vienen de una factura o de un abono. | Regla de negocio 2; cambio aprobado por el usuario (2026-11-06) | UC-INV-005 | `Income.CreateManual`, `IIncomeService.CreateManualAsync`/`CancelAsync`, políticas `Ingresos.RegistrarManual` y `Movimientos.Anular`, `IncomesController` (ruta `/ingresos`) | `IncomeTests`, `SqlServerIncomeServiceScenarioTests`, `IncomesAuthorizationTests`, `AssistantFinancialAccessTests` | Verificado |
| RF-INV-011 | Las pantallas de facturas, cuentas por cobrar e ingresos tienen filtros propios alineados con sus columnas; cada pantalla filtra solo su tabla. Se aplican con Enter o el botón de búsqueda, se limpian para mostrar todo y se conservan al paginar. El detalle de factura ofrece un botón para volver al listado. Asistente consulta facturas de todos los usuarios, e ingresos y cuentas por cobrar; en CxC es de solo consulta y no registra abonos. | Regla de negocio 11; ADR-0045, ADR-0052; cambio aprobado por el usuario (2026-11-06 y 2026-11-07) | UC-INV-005 | `InvoicesController.Index`, `InvoiceQuery`, `Views/Invoices/Details.cshtml`, `ReceivablesController.Index`, `IncomesController.Index`, políticas `Movimientos.Ver` y `Cuentas.Ver` | `SqlServerInvoiceServiceScenarioTests.Search_FiltersByStatusAndByOwner`, `ReceivablesAuthorizationTests`, `IncomesAuthorizationTests`, `AssistantFinancialAccessTests`; compilación de la vista Razor | Verificado |
| RF-99 | Cada cliente puede tener un límite de crédito propio en colones, opcional; si al confirmar una factura a crédito el saldo pendiente del cliente queda por encima de su límite, el sistema solo muestra una alerta informativa y nunca bloquea la confirmación. | Decisión del usuario (2026-11-03), ADR-0044 | UC-CAT-002, UC-INV-002 | `Customer.CreditLimit`, `IInvoiceService.CheckCustomerCreditLimitAsync`, `InvoicesController.Confirm`, `Views/Invoices/Confirm.cshtml` | `BusinessPartyCatalogTests.Customer_Constructor_RejectsANegativeCreditLimit`, `Customer_UpdateDetails_AllowsAZeroCreditLimitAndClearingItBackToNull`, `SqlServerInvoiceServiceScenarioTests.CheckCustomerCreditLimitAsync_WithoutALimitConfigured_NeverExceeds`, `CheckCustomerCreditLimitAsync_SumsOutstandingReceivablesAndNeverBlocksConfirmation` | Verificado |
| RF-88 | Una factura confirmada se exporta en JSON (una factura) o Excel (varias, de un período) con los datos preparados para el traslado manual al formato 4.4 de Hacienda: emisor (nombre, nombre comercial, cédula jurídica, ubicación codificada, otras señas, teléfono, correo, actividad económica), receptor (identificación, nombre, correo, actividad económica), condición de venta, medio de pago, líneas (con código de descuento) y resumen (gravado, exento y no sujeto por separado, descuentos, IVA por tarifa), además de la clave y el consecutivo oficiales de la factura. Nunca emite, firma ni transmite nada ante Hacienda. | Regla de negocio 15; paquete de mejoras finales (bloque 3); completado por el bloque 2 del paquete v7 (ADR-0047) | UC-INV-001, UC-INV-002 | `InvoiceElectronicDataExport.BuildJson`/`BuildWorkbook`, `Invoice.ElectronicInvoiceKey`/`ElectronicInvoiceNumber`, `SystemParameters.CompanyTradeName`/`CompanyProvinceCode`/`CompanyCantonCode`/`CompanyDistrictCode`, `Customer.ActivityCode` | `InvoiceElectronicDataExportTests.BuildJson_IncludesEveryRequiredSection`, `BuildJson_IncludesThePaymentMethodForACashInvoice`, `BuildWorkbook_GeneratesAValidWorkbookWithALineRowPerInvoiceLine` | Verificado |
| RF-100 | El detalle de una cuenta por cobrar muestra, por cada abono, la factura y su clave oficial (si existe) y el saldo resultante tras ese abono, como preparación del recibo electrónico de pago (sin emitirlo). | Paquete v7, bloque 2 (ADR-0047) | UC-INV-005 | `ReceivableModel.InvoiceElectronicKey`, `Views/Receivables/Details.cshtml` | `SqlServerReceivableServiceScenarioTests.GetAsync_ExposesTheInvoiceElectronicKeyOnceRegistered` | Verificado |

## Matriz del incremento de tipos de impuesto

El catálogo de tipos de impuesto alimenta el cálculo por línea de las facturas administrativas. Las
tarifas de referencia son datos configurables, no reglas de facturación, y su uso debe ser validado por
la empresa y su asesoría contable (ADR-0009 y ADR-0011).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-IMP-001 | Gerencia, Finanzas y Asistente consultan las tarifas de IVA en la pantalla única "Impuestos y retenciones"; Gerencia y Finanzas las crean, modifican (solo tarifa, descripción y estado: código, nombre y código de Hacienda quedan fijos tras crearla), desactivan y reactivan sin borrado físico. Las rutas anteriores (`/catalogos/tipos-impuesto`) solo redirigen. | ADR-0011, ADR-0046; ampliado a Gerencia por decisión del usuario (2026-10-21); pantalla unificada por el bloque 1 del paquete v7 (ADR-0046) | UC-CAT-007 | `TaxesAndWithholdingsController` / `TaxTypeService`; `TaxTypesController.Index` solo redirige | `TaxesAndWithholdingsControllerTests`, `TaxTypesControllerTests`, `CatalogAuthorizationTests`, `SqlServerTaxTypeScenarioTests`, `SqlServerCatalogPagesTests` | Verificado |
| RF-IMP-002 | Retirado: la carga manual de tarifas de IVA de referencia (botón "Cargar tarifas de referencia", `ReferenceTaxTypes`, `TaxTypeService.LoadReferenceTaxTypesAsync`) se eliminó del código. Las cuatro tarifas de referencia (IVA 13 %, IVA 2 %, Exento, No sujeto) ya nacen siempre por migración de siembra. | ADR-0009, ADR-0011; eliminado por decisión del usuario (2026-11-03), bloque 1 del paquete v7 (ADR-0046, punto 6) | UC-CAT-007 | Migraciones `SeedReferenceTaxTypes` y `AddTaxTypeSystemRolesAndVatRates` | No aplica (código retirado) | Descartado |
| RN-IMP-001 | El código es único y en mayúsculas; la tarifa no es negativa, conserva cuatro decimales y un impuesto porcentual no supera 100 %. | ADR-0011 | UC-CAT-007 | `TaxType`, `CK_TiposImpuesto_Rate` y `UX_TiposImpuesto_Code` | `TaxTypeTests`, `SqlServerTaxTypeScenarioTests` | Verificado |
| RN-IMP-002 | Solo existe el método porcentual: se retiró "monto fijo por unidad" (`TaxCalculationType.FixedAmountPerUnit`); un impuesto es siempre un porcentaje sobre el neto de la línea (regla 3). | ADR-0011, regla de negocio 3 | UC-CAT-007 | `TaxType` (sin `CalculationType`) | `TaxTypeTests` | Verificado |
| RN-IMP-003 | Modificar o desactivar un tipo no altera las facturas existentes, cuyas líneas conservan su propia fotografía; la base impide eliminar un tipo que una línea ya utilizó. | ADR-0011 | UC-CAT-007, UC-INV-001 | `InvoiceLineTax.TaxTypeId` y `FK_FacturaLineas_TiposImpuesto` | `InvoiceLineTaxTests`, `SqlServerInvoiceScenarioTests` | Verificado |
| RN-IMP-005 | Cada línea de factura lleva una sola tarifa de IVA, incluso la tarifa Exento (0 %); ya no existe una colección de impuestos por línea. | Regla de negocio 3 | UC-INV-001 | `InvoiceLine.Tax` (antes `InvoiceLine.Taxes`) | `InvoiceLineTaxTests` | Verificado |
| RN-IMP-006 | Las cuatro tarifas de sistema (IVA 13 %, IVA 2 %, Exento, No sujeto) se identifican por `TaxType.SystemRole`, nunca por código ni nombre (editables); una tarifa de sistema no se puede inactivar. El código de Hacienda es obligatorio salvo en "No sujeto" (sin equivalente en el catálogo oficial, pendiente de validar contra el Anexo 4.4). | Regla de negocio 1; ADR-0046 | UC-CAT-007 | `TaxTypeSystemRole`, `TaxType.SystemRole`/`HaciendaRateCode`, `TaxTypeService.SetActiveAsync` | `TaxTypeTests`, `SqlServerTaxTypeScenarioTests.SetActiveAsync_RejectsDeactivatingASystemRoleTaxType` | Verificado |

## Matriz del incremento de cuentas contables

El catálogo de cuentas contables unifica la clasificación contable con las cajas y los bancos. Es solo
clasificación: el sistema no lleva partida doble, asientos ni saldos contables (ADR-0012).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CTA-001 | Gerencia, Finanzas y Asistente consultan las cuentas contables; Gerencia y Finanzas las crean, modifican, activan y desactivan, sin borrado físico. | ADR-0012; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-006 | `LedgerAccountsController` / `LedgerAccountService` | `LedgerAccountsControllerTests`, `CatalogAuthorizationTests`, `SqlServerLedgerAccountScenarioTests`, `SqlServerCatalogPagesTests.FinanceUser_DeactivationFormPostsAnExplicitFalseValue` | Verificado |
| RF-CTA-002 | Gerencia y Finanzas asignan una cuenta contable a cada categoría financiera de ingreso o de gasto. | ADR-0012; ampliado a Gerencia por decisión del usuario (2026-10-21) | UC-CAT-005 | `FinancialCategoriesController` / `FinancialCategoryService` | `FinancialCategoriesControllerTests`, `SqlServerLedgerAccountScenarioTests`, `SqlServerCatalogPagesTests` | Verificado |
| RF-CTA-003 | El servicio ofrece las cajas y bancos activos para elegir la cuenta de efectivo de ingresos y pagos futuros. Ya no admite filtrar por moneda (regla 5). | ADR-0012 | UC-CAT-006 | `ILedgerAccountService.GetActiveCashAccountsAsync` | `SqlServerLedgerAccountScenarioTests` | Implementado |
| RN-CTA-001 | Toda cuenta tiene un tipo limitado a lo que el sistema clasifica (Activo, Ingreso o Gasto; sin Pasivo ni Patrimonio, regla 1); el tipo, el código, la cuenta superior y el subtipo de efectivo no cambian después de crearla, y nunca se toman del formulario de edición. | ADR-0012, regla de negocio 1 | UC-CAT-006 | `LedgerAccount` y `LedgerAccountsController.Edit` | `LedgerAccountTests`, `LedgerAccountsControllerTests` | Verificado |
| RN-CTA-002 | Una cuenta de efectivo es de tipo Activo y tiene subtipo Caja o Banco; ya no tiene moneda (regla 5). Una cuenta de efectivo no puede tener cuentas hijas. | ADR-0012 | UC-CAT-006 | `LedgerAccount`, `LedgerAccountService` y `CK_CuentasContables_Cash` | `LedgerAccountTests`, `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-CTA-007 | El código de una cuenta contable es jerárquico (p. ej. `3.1.07`) y lo calcula el sistema como el siguiente hijo libre de la cuenta superior; después de creada solo cambian el nombre y la referencia bancaria (regla 12). | Regla de negocio 1 y 12 | UC-CAT-006 | `LedgerAccountService.CreateAsync` (`ComputeNextChildCodeAsync`) | `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-CTA-008 | El sistema se instala con un catálogo base de cuentas y una categoría inicial por cada cuenta de detalle; las cuentas de grupo (primer y segundo nivel) quedan protegidas y no se modifican ni se inactivan, pero el usuario agrega cuentas de detalle dentro de los grupos. | Regla de negocio 13 (RF-54) | UC-CAT-005, UC-CAT-006 | `LedgerAccount.IsProtected` y la migración `SeedBaseChartOfAccounts` | `SqlServerLedgerAccountScenarioTests`, `SqlServerCatalogScenarioTests` | Verificado |
| RN-CTA-003 | La cuenta superior existe, está activa, es del mismo tipo y no produce ciclos; no se desactiva una cuenta con hijas activas y reactivar una hija exige la superior activa. | ADR-0012 | UC-CAT-006 | `LedgerAccountService` y `CK_CuentasContables_Parent` | `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-CTA-004 | El botón para desactivar una cuenta de movimiento siempre está disponible. Si tiene líneas en el libro mayor, la baja lógica se rechaza y se informa que existen movimientos asociados. Si no tiene movimientos, puede desactivarse; las operaciones automáticas que requieran su rol quedan bloqueadas hasta reactivarla. Las cuentas inactivas no se ofrecen para nuevas operaciones manuales. | Regla de negocio 1 y 13 | UC-CAT-005, UC-CAT-006 | `LedgerAccountModel.CanDeactivate`, `LedgerAccountService.SetActiveAsync`, `JournalPostingService.GetSystemAccountIdAsync` y opciones de cuenta activa | `LedgerAccountModelTests`, `SqlServerLedgerAccountScenarioTests`, `SqlServerInvoiceServiceScenarioTests` | Verificado |
| RN-CTA-005 | La cuenta contable no almacena saldo ni asientos: los saldos se calcularán desde movimientos confirmados. | ADR-0012, alcance aprobado | UC-CAT-006 | `LedgerAccount` | `LedgerAccountTests` | Verificado |
| RN-CTA-006 | Las cajas y bancos existentes se migraron a cuentas contables de efectivo con el mismo identificador, y las categorías existentes se asignaron a las cuentas raíz «Ingresos» o «Gastos». La tabla anterior se eliminó solo tras verificar que cada fila tenía su equivalente. | ADR-0012 | No aplica | Migraciones `AddLedgerAccounts`, `AddCategoryLedgerAccount` y `DropFinancialAccounts` | Verificada en bases de prueba con datos de ejemplo, incluido el caso en que falta un equivalente | Verificado |

## Matriz del incremento de tipos de retención

El catálogo de tipos de retención es el complemento del de tipos de impuesto: alimentará el registro de
abonos y pagos. A diferencia de un impuesto, una retención es siempre un porcentaje, sin monto fijo por
unidad. La base y el monto de cada retención concreta no viven en este catálogo, sino en el abono o el
pago al que se aplique (regla de negocio 3).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-RET-001 | Gerencia, Finanzas y Asistente consultan los tipos de retención en la pantalla única "Impuestos y retenciones"; Gerencia y Finanzas los crean, modifican (tarifa, base mínima, descripción y estado; código y nombre quedan fijos tras crearlo), desactivan y reactivan sin borrado físico. Las rutas anteriores (`/catalogos/tipos-retencion`) solo redirigen. | Regla de negocio 3; ADR-0046; ampliado a Gerencia por decisión del usuario (2026-10-21); pantalla unificada por el bloque 1 del paquete v7 (ADR-0046) | UC-CAT-008 | `TaxesAndWithholdingsController` / `WithholdingTypeService`; `WithholdingTypesController.Index` solo redirige | `TaxesAndWithholdingsControllerTests`, `WithholdingTypesControllerTests`, `CatalogAuthorizationTests`, `SqlServerWithholdingTypeScenarioTests` | Verificado |
| RN-RET-001 | El código es único y en mayúsculas; la tarifa es siempre porcentual, no es negativa, conserva cuatro decimales y no supera 100 %. | Regla de negocio 3 | UC-CAT-008 | `WithholdingType`, `CK_TiposRetencion_Rate` y `UX_TiposRetencion_Code` | `WithholdingTypeTests`, `SqlServerWithholdingTypeScenarioTests` | Verificado |
| RN-RET-002 | Modificar o desactivar un tipo de retención no altera los abonos o pagos que ya lo usaron. | Regla de negocio 3 | UC-CAT-008 | Pendiente: se verificará junto con la fotografía de la retención al construir abonos y pagos | Pendiente | Propuesto |
| RN-RET-003 | La retención del 2 % del sector público es el único tipo de sistema (`IsSystemType`); tiene una base mínima (`MinimumBase`) y no se puede inactivar. Solo se ofrece a clientes marcados como sector público (`Customer.AppliesStateWithholding`) y solo si la base enviada supera el mínimo configurado. | Regla de negocio 3; ADR-0046 | UC-CAT-008, UC-INV-005 | `WithholdingType.IsSystemType`/`MinimumBase`, `Customer.AppliesStateWithholding`, `ReceivablePaymentService.ResolveWithholdingTypesAsync` | `WithholdingTypeTests`, `SqlServerWithholdingTypeScenarioTests.SetActiveAsync_RejectsDeactivatingASystemTypeWithholding`, `SqlServerReceivablePaymentServiceScenarioTests.Register_WithAWithholdingForACustomerWithoutTheStateWithholdingMark_IsRejected`, `Register_WithAWithholdingBelowTheMinimumBase_IsRejected` | Verificado |

## Matriz del bloque 1 del paquete v7 (IVA y retenciones pendientes)

Retoma los puntos de IVA y retenciones que el paquete de mejoras finales dejó pendientes: condición de
IVA del cliente con tarifa automática por línea, retención del 2 % del sector público e IVA no
acreditable en gastos (ADR-0046).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-93 | Cada cliente tiene una condición de IVA (Normal, institución estatal de educación superior o no sujeto) que determina automáticamente la tarifa de IVA de cada línea de sus facturas; la factura nunca ofrece una tarifa que no corresponda a esa condición. Si el cliente es Normal y no se indica tarifa (ni el artículo ni la línea), se usa IVA 13 % por defecto. Cambiar el cliente de una factura en borrador recalcula la tarifa de cada línea existente. | Regla de negocio 3 (RF-93); ADR-0046 | UC-INV-001, UC-INV-002, UC-CAT-002 | `Customer.VatCondition`, `InvoiceService.ResolveLineTaxTypeIdAsync`/`RecalculateLineTaxesForNewCustomerAsync`, `InvoicesController.PopulateLineOptionsAsync` | `SqlServerInvoiceServiceScenarioTests` (resolución forzada por condición, rechazo de tarifa que no corresponde, fallback a 13 %, recalculación de líneas al cambiar el cliente) | Verificado |
| RF-94 | En un gasto, el IVA puede marcarse como no acreditable; en ese caso forma parte del costo del gasto en vez de acreditarse, y el asiento automático debita la cuenta de la categoría por el total (neto + IVA) sin generar la línea de IVA acreditable. | Regla de negocio 3 (RF-94); ADR-0046 | UC-GTO-001, UC-GTO-002 | `Expense.VatCreditable`, `JournalPostingService.PostExpenseConfirmationAsync` | `ExpenseTests`, `SqlServerJournalEntryScenarioTests.Expense_ConfirmedCashWithNonCreditableVat_DebitsCategoryByTheTotalAndSkipsVatCreditable` | Verificado |
| RF-95 | La única retención que aplica es la del 2 % del sector público (Ley del Impuesto sobre la Renta, art. 23, inciso g), sobre el monto sin IVA; solo se ofrece a clientes marcados como sector público y solo cuando el monto de la operación supera la base mínima configurada (un salario base). El servidor rechaza una retención enviada igualmente cuando no corresponde. | Regla de negocio 3 (RF-95); ADR-0046 | UC-INV-005 | `Customer.AppliesStateWithholding`, `WithholdingType.MinimumBase`/`IsSystemType`, `ReceivablePaymentService.ResolveWithholdingTypesAsync`, `ReceivablePaymentsController.ProposeWithholdingBase` | `SqlServerReceivablePaymentServiceScenarioTests.Register_WithAWithholdingForACustomerWithoutTheStateWithholdingMark_IsRejected`, `Register_WithAWithholdingBelowTheMinimumBase_IsRejected` | Verificado |
| RN-93-01 | Las tarifas y el tipo de retención que la lógica del sistema usa se identifican por rol (`TaxTypeSystemRole`/`WithholdingType.IsSystemType`), nunca por código ni nombre, porque esos campos son editables; la pantalla única "Impuestos y retenciones" no permite inactivarlos y fija código, nombre y código de Hacienda tras crearlos. | Regla de negocio 1; ADR-0046 | UC-CAT-007, UC-CAT-008 | `TaxesAndWithholdingsController`, `TaxTypeService.SetActiveAsync`, `WithholdingTypeService.SetActiveAsync` | `TaxesAndWithholdingsControllerTests`, `SqlServerTaxTypeScenarioTests`, `SqlServerWithholdingTypeScenarioTests.SetActiveAsync_RejectsDeactivatingASystemTypeWithholding` | Verificado |

## Matriz del incremento de parámetros del sistema

Cubre las reglas de negocio 7 (límite de autorización) y 9 (días de alerta de vencimiento). No es un
catálogo: existe como máximo una fila, y su bitácora es un historial propio de cambios, distinto de la
bitácora de seguridad de cuentas de usuario.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CFG-001 | Los campos del límite, los días de alerta, el período fiscal y los datos de empresa muestran la configuración vigente; los perfiles autorizados editan sus propios campos y el cambio solo se aplica al guardar. | Reglas de negocio 7 y 9; cambio solicitado (2026-11-07) | UC-CFG-001 | `SystemParametersController.Index` / `SystemParametersController.Save` / `SystemParametersService.GetAsync`, `Views/SystemParameters/Index.cshtml` | `SystemParametersControllerTests.Index_WhenConfigured_ShowsTheCurrentValues`, `CatalogAuthorizationTests.FinanceUser_SeesCurrentAuthorizationLimitInAnEditableField`, `CatalogAuthorizationTests.FinanceUser_CanSaveNestedSystemParametersForm` | Verificado |
| RF-CFG-002 | Gerencia configura o corrige el límite de autorización en CRC; Gerencia y Finanzas cambian los días de alerta de vencimiento, con control de concurrencia. Si Finanzas envía un límite distinto del vigente, el servicio lo rechaza ("Solo Gerencia puede cambiar el límite de autorización.") y no guarda nada; la creación inicial de los parámetros es solo de Gerencia. | Regla de negocio 7 y 9; paquete v9, bloque 6 (ADR-0059) | UC-CFG-001 | `SystemParametersController.Save` / `SystemParametersService.CreateAsync`/`UpdateAsync` (`actorIsManagement`) | `SystemParametersControllerTests`, `SqlServerSystemParametersScenarioTests.Finance_OnlyChangesTheAlertDays_WhileManagementDefinesTheLimit`, `SystemParametersAuthorizationTests`, `CatalogAuthorizationTests.FinanceUser_SeesCurrentAuthorizationLimitReadOnly` | Verificado |
| RN-CFG-001 | El límite de autorización no es negativo y se redondea a dos decimales; los días de alerta son un entero mayor que cero. | Regla de negocio 7 y 9 | UC-CFG-001 | `SystemParameters`, `CK_ParametrosSistema_AuthorizationLimit` y `CK_ParametrosSistema_OverdueAlertDays` | `SystemParametersTests`, `SqlServerSystemParametersScenarioTests` | Verificado |
| RN-CFG-002 | Gerencia define el límite de autorización, los datos de la empresa, el período fiscal, la escala de renta y las reaperturas o cierres mensuales; Finanzas entra a Parámetros, cambia solo los días de anticipación y ve lo demás en solo lectura; Asistente y Administrador no acceden. | Tabla de perfiles; CU-06 y Tabla 10 del documento final; ADR-0049, ADR-0054 y ADR-0059 | UC-CFG-001/002 | `SystemPolicies.ManageSystemParameters`, `SystemPolicies.ManageAccountingPeriods` (también en `UpdateCompany`), `SystemParametersService` | `CatalogAuthorizationTests`, `SystemParametersAuthorizationTests` | Verificado |
| RF-CFG-003 | Gerencia define un período fiscal de doce meses calendario, desde el primer día de un mes hasta el último día, con motivo opcional e historial inmutable; el período inicial es 01/01/2026–31/12/2026. | Cambio solicitado; ADR-0054 | UC-CFG-002 | `SystemParameters.UpdateFiscalPeriod`, `FiscalPeriodChange`, `SystemParametersController.UpdateFiscalPeriod` | `SystemParametersTests.UpdateFiscalPeriod_*`, `SystemParametersControllerTests.UpdateFiscalPeriod_*`, `SqlServerSystemParametersScenarioTests.UpdateFiscalPeriodAsync_*` | Parcial: unitarias/controlador verificadas; SQL pendiente |
| RF-CFG-004 | El presupuesto anual a la fecha acumula desde el inicio del período fiscal hasta el mes seleccionado y toma los doce meses fiscales para el presupuesto anual de referencia. | Cambio solicitado; regla de negocio 8; ADR-0054 | UC-REP-007 | `ReportService.GetAnnualBudgetReportAsync`, `ReportsController.AnnualBudget`, `Views/Reports/AnnualBudget.cshtml` | `SqlServerReportServiceScenarioTests.GetAnnualBudgetReportAsync_UsesConfiguredFiscalMonths` | Parcial: compilación verificada; SQL pendiente |
| RN-CFG-003 | Cada cambio queda en un historial de solo inserción, con el valor anterior y el nuevo de cada campo; el primer registro no tiene valor anterior. | Convenciones de datos («cambios de parámetros quedan en la bitácora») | UC-CFG-001 | `SystemParameterChange` y `FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated` | `SystemParameterChangeTests`, `SqlServerSystemParametersScenarioTests` | Verificado |

## Matriz del incremento de administración de usuarios

El incremento extiende Identity sin agregar registro ni recuperación pública. Los roles continúan
siendo valores fijos y la bitácora de seguridad no conserva contraseñas, tokens ni hashes.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-USR-001 | Administrador puede listar y filtrar por nombre, correo, rol, estado de acceso y estado de cuenta mediante paginación del servidor; los filtros se aplican con Enter o el botón de búsqueda y se limpian para volver a la lista completa. | ADR-0008 | UC-USR-001 | `UsersController.Index` / `UserAdministrationService.SearchAsync` | `SqlServerUserAdministrationScenarioTests.CreateSearchAndEdit_KeepOneRoleAndRejectStaleOrDuplicateData`, PR-USR-001, PR-USR-010 | Verificado |
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

## Matriz del incremento de gastos y cuentas por pagar

Paso 3 completo: dominio y servicio de gastos, cuentas por pagar (ADR-0027), pagos a proveedores
(ADR-0028) y sus pantallas (ADR-0029), espejo del incremento de facturación, cuentas por cobrar,
abonos e ingresos.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-GAS-001 | Asistente, Finanzas y Gerencia crean y editan el encabezado de un borrador de gasto en colones para un proveedor activo, con una categoría de gasto, una tarifa de IVA y un código automático (`GTO-000001`); Asistente consulta gastos de cualquier usuario, pero solo edita sus propios borradores. | Regla de negocio 2, 3 y 12; Perfiles (Tabla de permisos); ADR-0052 | UC-GAS-001 | `IExpenseService.CreateAsync`/`UpdateHeaderAsync` (parámetro `restrictToOwnerUserId`), `Expense`, `finanzas.Seq_GastoCodigo`, `ExpensesController` (ruta `/gastos`) | `ExpenseTests`, `SqlServerExpenseServiceScenarioTests.AssistantCanReadAnotherUsersExpense_ButCannotEditIt`, `ExpensesAuthorizationTests` | Verificado |
| RF-GAS-002 | Al confirmar un gasto de contado se indican cuenta de efectivo y referencia (el medio de pago ya quedó fijo desde el borrador); a crédito el sistema genera su cuenta por pagar con el vencimiento del plazo elegido. | Regla de negocio 2, 3 y 7 | UC-GAS-002 | `IExpenseService.ConfirmAsync`, `Expense.Confirm`, `Payable`, `ExpensesController.Confirm` (GET/POST) | `SqlServerExpenseServiceScenarioTests.Confirm_CashWithinTheLimit_SavesTheCashAccountAndReferenceWithoutAPayable`, `.Confirm_Credit_CreatesThePayableWithTheExpenseTotalAndDueDate` | Verificado |
| RF-GAS-003 | Un gasto de contado que supera el límite de autorización no lo confirma Finanzas de contado; Gerencia sí puede confirmarlo de contado sobre el límite. | Regla de negocio 7; decisión del usuario (2026-10-25): mismo criterio que el límite de abonos y pagos | UC-GAS-002 | `IExpenseService.ConfirmAsync` (parámetro `actorIsManagement`), `SystemParameters.AuthorizationLimitCrc` | `SqlServerExpenseServiceScenarioTests.Confirm_CashAboveTheLimitWithoutManagement_IsRejectedButManagementCanConfirmIt` | Verificado |
| RF-GAS-004 | Gerencia anula un gasto confirmado con motivo; si era a crédito, anula en cascada su cuenta por pagar, siempre que no tenga pagos vigentes. | Regla de negocio 4 | UC-GAS-003 | `IExpenseService.CancelAsync`, `Payable.Cancel`, `ExpensesController.Cancel` | `SqlServerExpenseServiceScenarioTests.Cancel_OnACreditExpenseWithoutPayments_CancelsItsPayable`, `.Cancel_OnACreditExpenseWithAnActivePayment_IsBlockedAndKeepsEverythingUnchanged` | Verificado |
| RF-GAS-008 | Las pantallas de gastos y cuentas por pagar tienen cada una sus propios filtros alineados con sus columnas (incluidos rangos de fecha y monto); se aplican con Enter o el botón de búsqueda, se limpian para mostrar todo y se conservan al paginar. Asistente consulta gastos de cualquier usuario y tiene acceso de solo consulta a cuentas por pagar, sin registrar pagos. | Regla de negocio 11; ADR-0052; cambio aprobado por el usuario (2026-11-06) | UC-GAS-001, UC-GAS-002 | `ExpensesController.Index`, `PayablesController.Index`, políticas `Movimientos.Ver` y `Cuentas.Ver` | `ExpensesAuthorizationTests`, `SqlServerExpenseServiceScenarioTests.AssistantCanReadAnotherUsersExpense_ButCannotEditIt`, `PayablesAuthorizationTests`, `AssistantFinancialAccessTests` | Verificado |
| RN-GAS-001 | El gasto se registra sin IVA; el IVA y el total se calculan sobre el monto neto registrado, con una sola tarifa por gasto conservada como fotografía (código, nombre, tarifa, importe). | Regla de negocio 3 | UC-GAS-001 | `Expense` (`TaxCode`, `TaxName`, `TaxRate`, `TaxAmount`) | `ExpenseTests` | Verificado en dominio |
| RN-GAS-002 | El plazo de pago de un gasto es la misma lista fija que la de facturas (contado o crédito a 30/60/90 días) y el vencimiento se calcula igual: fecha de emisión más los días del plazo. | Regla de negocio 14 | UC-GAS-001 | `Expense` reutiliza `PaymentTerm`/`PaymentTermDays` de `Invoices` | `ExpenseTests` | Verificado en dominio |
| RN-GAS-003 | El saldo de una cuenta por pagar se reduce con cada pago vigente y se restituye al anular uno; la invariante 0 ≤ saldo ≤ monto original se exige en el dominio y se repite como `CHECK` en la base de datos. | Control de integridad | UC-GAS-002 | `Payable.ApplyPayment`, `Payable.RevertPayment`, `CK_CuentasPorPagar_Balance` | `PayableTests` | Verificado |
| RN-GAS-004 | No se anula un gasto a crédito con pagos vigentes en su cuenta por pagar; estos deben anularse primero. | Control interno (regla de negocio 4) | UC-GAS-003 | `Payable.Cancel` (exige `Balance == OriginalAmount`), `IExpenseService.CancelAsync` | `SqlServerExpenseServiceScenarioTests.Cancel_OnACreditExpenseWithAnActivePayment_IsBlockedAndKeepsEverythingUnchanged` | Verificado |
| RN-GAS-005 | Confirmar y anular un gasto quedan registrados en la bitácora de operaciones, con el documento, la acción, quién la hizo y cuándo (y el motivo, si fue una anulación); la cuenta por pagar que genera no tiene entrada propia, igual criterio que `Receivable`. | Regla de negocio 4 y 16 | UC-GAS-002, UC-GAS-003 | `OperationLogEntry`, `OperationLogEntityType.Expense`/`Payable`, `IExpenseService.ConfirmAsync`/`CancelAsync` | `SqlServerExpenseServiceScenarioTests` | Verificado |
| RN-INC-001 | Todo ingreso tiene un código automático (`ING-001`, regla 12), asignado al crear y no editable; los ingresos existentes antes de esta corrección se respaldaron por orden de creación. | Regla de negocio 12; brecha detectada al construir RF-GAS-001 | UC-INV-005 | `Income.Code`, `finanzas.Seq_IngresoCodigo`, migración `AddExpensesPayablesAndIncomeCode` | `IncomeTests` | Verificado |
| RF-GAS-005 | Finanzas registra pagos a proveedores hasta el límite de autorización y Gerencia los que lo superan; el sistema informa el saldo pendiente. | Regla de negocio 7, ADR-0028 | UC-GAS-004 | `ISupplierPaymentService.RegisterAsync` (parámetro `actorIsManagement`), `SystemParameters.AuthorizationLimitCrc`, `SupplierPaymentsController` (ruta `/cuentas-por-pagar/pagos`) | `SqlServerSupplierPaymentServiceScenarioTests.Register_AboveTheAuthorizationLimitWithoutManagement_IsRejected`, `PayablesAuthorizationTests` | Verificado |
| RF-GAS-006 | Un pago puede aplicarse a varias cuentas por pagar del mismo proveedor en una sola operación: crea una fila por cuenta, todas con la misma referencia y un identificador de grupo común; cada una se anula de forma individual. La pantalla propone el reparto de la más antigua a la más nueva, editable. | Regla de negocio 2 (RF-15) | UC-GAS-004 | `SupplierPayment.GroupId`, `ISupplierPaymentService.RegisterAsync`/`CancelAsync`, `SupplierPaymentsController.Start`/`Register` (asistente de dos pasos) | `SqlServerSupplierPaymentServiceScenarioTests.Register_ToSeveralPayablesOfTheSameSupplier_SharesTheGroupIdAndCancelsIndividually` | Verificado |
| RF-GAS-007 | Los pagos a proveedores no llevan retención. | Regla de negocio 3 | UC-GAS-004 | `RegisterSupplierPaymentCommand` (sin retención, a diferencia de `RegisterReceivablePaymentCommand`) | `SupplierPaymentTests` | Verificado en dominio |
| RN-GAS-006 | El saldo de una cuenta por pagar se reduce con cada pago vigente y se restituye al anular uno, igual criterio que una cuenta por cobrar. | Control de integridad | UC-GAS-004 | `Payable.ApplyPayment`, `Payable.RevertPayment` | `SqlServerSupplierPaymentServiceScenarioTests.Register_ToASinglePayable_AppliesThePayment` | Verificado |
| RN-GAS-007 | El sistema advierte, sin bloquear, si un ingreso, un abono, un pago o una factura de contado repite la referencia en la misma cuenta de efectivo. | Regla de negocio 10 | UC-GAS-004, UC-INV-005 | `DuplicateReferenceGuard.ExistsAsync` (Infrastructure/Common), comparando contra `Ingresos`, `Abonos` y `Pagos`; usado por `ISupplierPaymentService.RegisterAsync`, `IReceivablePaymentService.RegisterAsync`, `IIncomeService.CreateManualAsync` e `IInvoiceService.HasDuplicateCashReferenceAsync` | `SqlServerSupplierPaymentServiceScenarioTests.Register_WithAReferenceAlreadyUsedInTheSameCashAccount_WarnsButSucceeds` | Verificado |
| RN-GAS-008 | Confirmar y anular un pago quedan registrados en la bitácora de operaciones; a diferencia de la cuenta por pagar que actualizan, el pago sí tiene entrada propia. | Regla de negocio 4 y 16 | UC-GAS-004 | `OperationLogEntry`, `OperationLogEntityType.SupplierPayment` | `SqlServerSupplierPaymentServiceScenarioTests` | Verificado |
| RN-GAS-009 | Asistente consulta gastos de cualquier usuario, en cualquier estado, y solo edita sus propios borradores; Finanzas y Gerencia ven y resuelven cualquiera según su política. | Perfiles (Tabla de permisos); ADR-0052 | UC-GAS-001 a UC-GAS-003 | `ExpensesController` (consulta sin límite por propietario; `OwnerRestriction` en edición), `IExpenseService` | `SqlServerExpenseServiceScenarioTests.AssistantCanReadAnotherUsersExpense_ButCannotEditIt`, `ExpensesAuthorizationTests` | Verificado |

## Matriz del incremento de presupuesto

Paso 4 completo: presupuesto mensual por categoría de gasto de último nivel y la alerta no bloqueante
al confirmar un gasto (ADR-0030, ADR-0031).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-PRE-001 | Gerencia y Finanzas configuran o corrigen el monto presupuestado de una categoría de gasto de último nivel para un mes, con monto 0 válido; cada cambio guarda el monto anterior, el nuevo, quién y cuándo. | Regla de negocio 6 | UC-PRE-001 | `IBudgetService.SetAmountAsync`, `Budget`, `BudgetChange`, `BudgetsController.Edit` (ruta `/presupuesto`) | `BudgetTests`, `BudgetChangeTests`, `SqlServerBudgetServiceScenarioTests.SetAmount_FirstConfiguration_HasNoPreviousAmount`, `.SetAmount_SubsequentCorrection_KeepsThePreviousAmount` | Verificado |
| RF-PRE-002 | Solo se presupuestan categorías de gasto de último nivel; una categoría con hijas se rechaza. Las categorías superiores no tienen fila propia: solo suman. | Regla de negocio 6 | UC-PRE-001 | `BudgetService` (validación de categoría hoja, misma consulta que `FinancialCategoryService`) | `SqlServerBudgetServiceScenarioTests.SetAmount_RejectsACategoryThatHasChildren` | Verificado |
| RF-PRE-003 | Lo ejecutado es el monto sin IVA (`NetAmount`) de los gastos confirmados de la cuenta de gasto y el mes (sin borradores ni anulados; los pagos no lo cambian); disponible, variación y variación % se calculan a partir de ahí y nunca se almacenan. Corregido: decía "total con IVA" (RF-40 del documento final; el código ya usaba el neto). | Regla de negocio 6; RF-40 del documento final | UC-PRE-001 | `IBudgetService.SearchByPeriodAsync`/`GetAsync`, `BudgetModel`, `ExpenseService.CheckBudgetAsync` | `SqlServerBudgetServiceScenarioTests.SearchByPeriod_OnlyConfirmedExpensesOfThatMonthCountAsExecuted` | Verificado |
| RF-PRE-004 | Al confirmar un gasto que supera lo disponible del presupuesto se muestra una alerta antes de guardar, sin bloquear; si no hay presupuesto configurado para esa categoría y ese mes, se indica y se permite continuar. | Regla de negocio 6 | UC-GAS-002 | `IExpenseService.CheckBudgetAsync`, `ExpensesController.Confirm` (banner informativo, GET y POST) | `SqlServerBudgetServiceScenarioTests.CheckBudgetAsync_WithoutABudget_ReportsNoBudgetAndNeverExceeds`, `.CheckBudgetAsync_WithABudget_ReportsWhetherItWouldExceedTheAvailableAmount` | Verificado |
| RN-PRE-002 | Un presupuesto solo se puede crear o modificar para el mes actual o uno futuro. Los períodos anteriores se pueden consultar, pero no tienen acciones de alta o edición; la validación del servicio usa el reloj común `TimeProvider`. | Decisión aprobada por el usuario; ADR-0053 | UC-PRE-001 | `BudgetService.SetAmountAsync` (validación con `TimeProvider.GetLocalNow`), `BudgetsController` | `SqlServerBudgetServiceScenarioTests.SetAmount_RejectsPreviousMonth` | Verificado |
| RN-PRE-001 | Solo Gerencia y Finanzas ven y editan el presupuesto; Asistente no lo ve. | Perfiles (Tabla de permisos) | UC-PRE-001 | Política `Reportes.Ver` en `BudgetsController` | `BudgetsAuthorizationTests` | Verificado |

## Matriz del incremento de consultas, vencidas y reportes

Paso 5, primer bloque: el indicador de cuentas vencidas y próximas a vencer (ADR-0032). Los bloques de
Consultas y reportes se agregan a esta misma matriz a medida que se completan.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-CON-001 | Gerencia, Finanzas y Asistente ven en el inicio la cantidad y el monto de cuentas por cobrar y por pagar vencidas (vencimiento anterior a hoy y saldo mayor que cero) y próximas a vencer (dentro de los días de alerta, 7 por defecto); Administrador no lo ve. Solo Gerencia y Finanzas ven el indicador de presupuestos excedidos. | Regla de negocio 9; cambio aprobado por el usuario (2026-11-07) | UC-CON-001 | `IReceivableService`/`IPayableService.GetOverdueSummaryAsync`, `HomeController.Index` (`Cuentas.Ver` para vencimientos y `Reportes.Ver` para presupuesto), `_OverdueIndicator.cshtml`, `_BudgetExceededIndicator.cshtml` | `AssistantFinancialAccessTests`, `SqlServerReceivableServiceScenarioTests.GetOverdueSummaryAsync_ClassifiesOverdueAndUpcomingSeparately`, `SqlServerPayableServiceScenarioTests.GetOverdueSummaryAsync_ClassifiesOverdueAndUpcomingSeparately` | Verificado |
| RF-CON-002 | (Descartado) La pantalla única de Consultas se retiró con su módulo (controlador, vistas, modelos y pruebas) porque cada listado tiene sus propios filtros. | Regla de negocio 11 (modificada); ADR-0049 | — | Retirado: `QueriesController`, `Views/Queries/*`, `Models/Queries/*` | — | Descartado |
| RF-CON-003 | Gerencia y Finanzas consultan, en pantalla, PDF y Excel, los ingresos vigentes emitidos entre dos fechas, con el total; cada reporte muestra su nombre, los parámetros usados, la fecha y hora de generación y el usuario; sin datos no se genera ningún archivo. | Regla de negocio 8 | UC-REP-001 | `IReportService.GetIncomeReportAsync`, `ReportsController.Income`/`IncomePdf`/`IncomeExcel`, `IncomeReportPdfDocument`, `ReportExcelExporter.BuildIncomeWorkbook` | `SqlServerReportServiceScenarioTests.GetIncomeReportAsync_OnlyIncludesActiveIncomesWithinThePeriodAndTotalsThem`, `GetIncomeReportAsync_WithNoIncomesInThePeriod_ReturnsAnEmptyResult`, `ReportExportTests.IncomeReportPdfDocument_WithLines_GeneratesAValidPdf`, `BuildIncomeWorkbook_WithLines_GeneratesAValidXlsx` | Verificado |
| RF-CON-004 | Gerencia y Finanzas consultan, en pantalla, PDF y Excel, los gastos confirmados entre dos fechas agrupados por categoría (o de una sola, si se indica), con el total; excluye siempre borradores y anulados; cada reporte muestra su nombre, los parámetros usados, la fecha y hora de generación y el usuario; sin datos no se genera ningún archivo. | Regla de negocio 8 | UC-REP-002 | `IReportService.GetExpenseByCategoryReportAsync`, `ReportsController.ExpenseByCategory`/`ExpenseByCategoryPdf`/`ExpenseByCategoryExcel`, `ExpenseByCategoryReportPdfDocument`, `ReportExcelExporter.BuildExpenseByCategoryWorkbook` | `SqlServerReportServiceScenarioTests.GetExpenseByCategoryReportAsync_OnlyIncludesConfirmedExpensesGroupedByCategory`, `GetExpenseByCategoryReportAsync_WithNoExpensesInThePeriod_ReturnsAnEmptyResult`, `ReportExportTests.ExpenseByCategoryReportPdfDocument_WithLines_GeneratesAValidPdf`, `BuildExpenseByCategoryWorkbook_WithLines_GeneratesAValidXlsx` | Verificado |
| RF-CON-005 | Gerencia y Finanzas consultan, en pantalla, PDF y Excel, el presupuesto contra lo ejecutado de un mes por categoría de gasto de último nivel, con los totales; cada reporte muestra su nombre, los parámetros usados, la fecha y hora de generación y el usuario; sin categorías de último nivel activas no se genera ningún archivo. | Regla de negocio 8 | UC-REP-003 | `IReportService.GetBudgetVsExecutedReportAsync` (reutiliza `IBudgetService.SearchByPeriodAsync`), `ReportsController.BudgetVsExecuted`/`BudgetVsExecutedPdf`/`BudgetVsExecutedExcel`, `BudgetVsExecutedReportPdfDocument`, `ReportExcelExporter.BuildBudgetVsExecutedWorkbook` | `SqlServerReportServiceScenarioTests.GetBudgetVsExecutedReportAsync_IncludesExecutedFromConfirmedExpensesForThatCategoryAndMonth`, `ReportExportTests.BudgetVsExecutedReportPdfDocument_WithLines_GeneratesAValidPdf`, `BuildBudgetVsExecutedWorkbook_WithLines_GeneratesAValidXlsx` | Verificado |
| RF-CON-006 | Gerencia y Finanzas consultan, en pantalla, PDF y Excel, el comparativo mensual de ingresos vigentes y gastos confirmados para un rango de meses, con una fila por mes (incluidos los meses sin movimientos) y los totales; sin movimientos en ningún mes del rango no se genera ningún archivo. | Regla de negocio 8 | UC-REP-004 | `IReportService.GetMonthlyComparisonReportAsync`, `ReportsController.MonthlyComparison`/`MonthlyComparisonPdf`/`MonthlyComparisonExcel`, `MonthlyComparisonReportPdfDocument`, `ReportExcelExporter.BuildMonthlyComparisonWorkbook` | `SqlServerReportServiceScenarioTests.GetMonthlyComparisonReportAsync_SumsIncomesAndExpensesPerMonthAndFillsMonthsWithNoActivity`, `GetMonthlyComparisonReportAsync_WithNoActivityInTheRange_ReturnsZeroTotalsButKeepsTheMonths`, `ReportExportTests.MonthlyComparisonReportPdfDocument_WithLines_GeneratesAValidPdf`, `BuildMonthlyComparisonWorkbook_WithLines_GeneratesAValidXlsx` | Verificado |
| RF-CON-007 | Gerencia y Finanzas consultan, en pantalla, PDF y Excel, el estado de cuenta de un cliente elegido: su historial cronológico de facturas a crédito y abonos (incluidos los anulados, visibles pero sin afectar el saldo) y su saldo actual; sin facturas a crédito no se genera ningún archivo. | Regla de negocio 8 | UC-REP-005 | `IReportService.GetCustomerStatementReportAsync`, `ReportsController.CustomerStatement`/`CustomerStatementPdf`/`CustomerStatementExcel`, `CustomerStatementReportPdfDocument`, `ReportExcelExporter.BuildCustomerStatementWorkbook` | `SqlServerReportServiceScenarioTests.GetCustomerStatementReportAsync_BuildsChronologicalHistoryWithCurrentBalance`, `GetCustomerStatementReportAsync_KeepsACancelledCreditInvoiceVisibleButExcludesItFromTheCurrentBalance`, `GetCustomerStatementReportAsync_WithNoCreditInvoices_ReturnsAnEmptyResult`, `ReportExportTests.CustomerStatementReportPdfDocument_WithLines_GeneratesAValidPdf`, `BuildCustomerStatementWorkbook_WithLines_GeneratesAValidXlsx` | Verificado |
| RF-CON-008 | Gerencia y Finanzas consultan, en pantalla, PDF y Excel, las cuentas por pagar vigentes agrupadas por proveedor, con la cantidad, el saldo total y el vencimiento más próximo de cada uno; sin cuentas por pagar vigentes no se genera ningún archivo. | Regla de negocio 8 | UC-REP-006 | `IReportService.GetPayableBySupplierReportAsync`, `ReportsController.PayableBySupplier`/`PayableBySupplierPdf`/`PayableBySupplierExcel`, `PayableBySupplierReportPdfDocument`, `ReportExcelExporter.BuildPayableBySupplierWorkbook` | `SqlServerReportServiceScenarioTests.GetPayableBySupplierReportAsync_GroupsVigentPayablesBySupplierAndExcludesPaidOnes`, `GetPayableBySupplierReportAsync_WithNoPayables_ReturnsAnEmptyResult`, `ReportExportTests.PayableBySupplierReportPdfDocument_WithLines_GeneratesAValidPdf`, `BuildPayableBySupplierWorkbook_WithLines_GeneratesAValidXlsx` | Verificado |

## Matriz del incremento de anulaciones, ajustes y cierres mensuales

Paso 6, primer bloque: el ajuste manual de saldo (ADR-0037). Los bloques de fecha de cierre y su
validación en las demás operaciones se agregan a esta misma matriz a medida que se completan.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-AJU-001 | Finanzas y Gerencia ajustan manualmente el saldo de una cuenta por cobrar o por pagar, con un sentido (aumento o disminución) y un motivo obligatorios, respetando 0 ≤ saldo ≤ monto original; el ajuste no genera ingresos ni pagos ni toca efectivo, documento o presupuesto. Asistente no tiene este permiso. | Regla de negocio 4; ADR-0057 | UC-AJU-001 | `Receivable.Adjust`/`Payable.Adjust`, `IBalanceAdjustmentService.AdjustReceivableAsync`/`AdjustPayableAsync`, `BalanceAdjustmentsController` (rutas `/cuentas-por-cobrar/{id}/ajustar` y `/cuentas-por-pagar/{id}/ajustar`), política `Movimientos.Ajustar` | `ReceivableTests.Adjust_*`, `PayableTests.Adjust_*`, `SqlServerBalanceAdjustmentServiceScenarioTests.AdjustReceivableAsync_ACorrectionInTheOppositeDirectionRestoresTheOriginalBalance`, `AdjustReceivableAsync_RejectsAnAdjustmentThatWouldExceedTheOriginalAmount`, `AdjustPayableAsync_WithDecrease_ReducesTheBalanceAndLogsTheAdjustment`, `AdjustPayableAsync_WithAStaleVersion_ReturnsConcurrencyConflict`, `ReceivablesAuthorizationTests.BalanceAdjustmentEndpoints_RequireAdjustFinancialBalancesNotTheOthers`, `PayablesAuthorizationTests.BalanceAdjustmentEndpoints_RequireAdjustFinancialBalancesNotTheOthers`, `BalanceAdjustmentAuthorizationTests` | Verificado |
| RF-AJU-002 | Un ajuste registrado por error se corrige con otro ajuste en sentido contrario; ningún ajuste se edita ni se anula, y todos quedan visibles en el historial de la cuenta junto con su motivo y usuario. | Regla de negocio 4 | UC-AJU-001 | `BalanceAdjustment` (registro inmutable), `IBalanceAdjustmentService.SearchByReceivableAsync`/`SearchByPayableAsync`, sección "Ajustes de saldo" en `Views/Receivables/Details.cshtml` y `Views/Payables/Details.cshtml` | `SqlServerBalanceAdjustmentServiceScenarioTests.AdjustReceivableAsync_ACorrectionInTheOppositeDirectionRestoresTheOriginalBalance` | Verificado |
| RF-CIE-001 | Los meses anteriores al mes actual, determinado con la fecha local del `TimeProvider`, quedan cerrados automáticamente. Gerencia puede reabrir uno con motivo, mantenerlo abierto hasta otro cambio y cerrarlo nuevamente; cada transición queda en una bitácora inmutable. | Regla de negocio 16; ADR-0054 | UC-CFG-002 | `ClosingDateGuard.GetViolationMessageAsync`, `AccountingPeriodOverride`, `AccountingPeriodChange`, `SystemParametersController.ReopenAccountingPeriod`/`CloseAccountingPeriod`, política `Parametros.PeriodosContables` | `AccountingPeriodOverrideTests`, `SqlServerSystemParametersScenarioTests.ReopenAndCloseAccountingPeriod_*`, escenarios `*IsNowClosed_IsRejected` | Parcial: unitarias/controlador y compilación verificadas; SQL pendiente |
| RF-CIE-002 | No se registra, confirma, anula ni ajusta fuera del período fiscal vigente ni en un mes pasado cerrado, salvo que Gerencia lo haya reabierto. Excepción: se acepta una fecha del período fiscal inmediatamente anterior si su mes sigue abierto (el mes actual o uno reabierto); el rango permitido es [inicio del período − 12 meses, fin del período]. Fuera de él: "No es posible operar con fecha dd/MM/yyyy: está fuera del período fiscal vigente y del período anterior." | Regla de negocio 16; RF-55 y CU-06 del documento final; ADR-0054 y ADR-0059 | UC-CFG-002 | `ClosingDateGuard.GetViolationMessageAsync` valida primero el rango fiscal y luego el estado mensual; se invoca desde facturas, gastos, abonos, pagos, ingresos, ajustes, traspasos, liquidaciones, notas de crédito y pagos de renta | `SqlServerPreviousFiscalPeriodScenarioTests`, escenarios `*IsNowClosed_IsRejected` de los servicios financieros | Verificado |

## Matriz del incremento de perfiles y permisos

Paso 7 de `CLAUDE.md`, el último del roadmap (ADR-0040).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-SEG-007 | Cualquier usuario autenticado consulta, sin administrar nada desde ahí, la matriz de los cuatro perfiles y sus permisos. | Perfiles aprobados | UC-SEG-005 | `PermissionsController` (ruta `/seguridad/perfiles-y-permisos`), `Views/Permissions/Index.cshtml` | `PermissionsAuthorizationTests.PermissionsRoute_WhenAnonymous_RedirectsToLogin`, `PermissionsEndpoint_HasNoAdditionalPolicy_RelyingOnlyOnTheGlobalAuthenticatedUserFallback` | Verificado |

## Matriz del cambio de asientos contables a consulta general

El bloque de asientos (RF-78/RF-79) ya generaba sus registros dentro de la transacción de cada
operación, pero la pantalla `/contabilidad/asientos` solo ofrecía un comando explícito de relleno para
documentos anteriores y era exclusiva de Gerencia. Este cambio la convierte en una consulta general de
movimientos contables y retira el relleno (ADR-0048).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-ASI-001 | Toda factura confirmada, gasto confirmado, abono, pago a proveedor, ingreso manual y ajuste de saldo genera su asiento contable dentro de la misma transacción en que se registra, y toda anulación genera su asiento de reversión; el usuario no ejecuta ningún comando para generarlos. | Reglas 1, 2 y 4; RF-78/RF-79; ADR-0048 | UC-ASI-001 | `JournalPostingService`, invocado por `InvoiceService`, `ExpenseService`, `ReceivablePaymentService`, `IncomeService`, `SupplierPaymentService` y `BalanceAdjustmentService` | `SqlServerJournalEntryScenarioTests` (escenarios de confirmación y anulación de cada operación) | Verificado |
| RF-ASI-002 | Gerencia y Finanzas consultan, en pantalla, los asientos de un mes: el conteo y el total de movimientos del resultado, los filtros de período, estado y número de asiento específico y, por cada asiento, su número `ASI-000000`, fecha, descripción, total y sus líneas (cuenta, débito y crédito); los filtros se aplican con Enter o el botón de búsqueda y se pueden limpiar. Asistente y Administrador no entran. | Cambio solicitado; mockup de la pantalla; ADR-0048 | UC-ASI-002 | `IJournalEntryService.SearchAsync`, `JournalEntriesController.Index` (ruta `/contabilidad/asientos`), `JournalEntriesIndexViewModel`, `Views/JournalEntries/Index.cshtml`, política `Reportes.Ver` | `JournalEntriesAuthorizationTests.JournalEntriesEndpoint_RequiresTheExpectedPolicy`, `SqlServerJournalEntryScenarioTests.SearchAsync_ReturnsTheConfirmedInvoiceEntryWithItsLinesTotalAndPeriodSummary` (incluye búsqueda exacta por número) | Verificado |
| RF-ASI-003 | La consulta de asientos no ofrece ningún comando para generar asientos: se retira el relleno de documentos previos, y el enlace del menú "Asientos contables" queda visible para Gerencia y Finanzas bajo Control Financiero Contable. | ADR-0048 | UC-ASI-002 | `JournalEntriesController` sin `Backfill`, `IJournalEntryService` sin `BackfillAsync`, `_Layout.cshtml` (enlace bajo Control Financiero Contable con `canViewFinancialReports`) | `JournalEntriesAuthorizationTests` (ya no existe la ruta `generar-pendientes`) | Verificado |

## Matriz de notas de crédito

La nota de crédito total es el único flujo de la interfaz web para anular una factura confirmada.
Conserva el historial, liquida el saldo por cobrar, devuelve solo el efectivo recibido y genera un
asiento propio (ADR-0058).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-NCR-001 | Finanzas y Gerencia consultan facturas confirmadas y pueden emitir una nota de crédito total con fecha, motivo y copia de líneas. La operación anula la factura en la misma transacción; en crédito liquida el saldo, devuelve solo los abonos en efectivo y revierte las retenciones en el activo; en contado devuelve el total. La devolución requiere cuenta de efectivo y referencia, respeta el límite de autorización y produce un asiento contable propio. Una factura con ajustes de saldo o importes no conciliados no se acredita. | Regla de negocio 4, nota de crédito interna; ADR-0058 | UC-NCR-001 | `CreditNotesController`, `ICreditNoteService.RegisterAsync`, `CreditNoteService`, `CreditNote`, `CreditNoteLine`, `JournalPostingService.PostCreditNoteAsync`, `Views/CreditNotes/*`, enlace en `_Layout.cshtml` | `CreditNoteTests`, `CreditNotesAuthorizationTests`, `SqlServerInvoiceServiceScenarioTests.CreditNoteRegistration_CancelsCashInvoiceAndPostsRefundInTheSameFlow`, `CreditNotePdfDocumentTests` | Verificado |
| RF-NCR-002 | La factura no ofrece una acción web ni una ruta directa de anulación; la nota de crédito tiene detalle y PDF, no puede anularse y conserva vínculo al asiento y la factura de origen. | Decisión aprobada por el usuario; ADR-0058 | UC-NCR-001 | `InvoicesController` sin endpoints `anular`, `CreditNotesController.Details`/`Pdf`, `InvoiceDetails.cshtml` | `InvoiceAuthorizationTests.InvoiceEndpoints_RequireExactlyThePolicyOfEachAction`, `CreditNotesAuthorizationTests.CreditNoteEndpoints_RequireConfirmationPolicyAndDoNotExposeInvoiceCancellation` | Verificado |

## Matriz de la reorganización del menú y permisos de parámetros

El menú lateral se agrupa por ciclo de trabajo y se retira la pantalla de Consultas; además, Finanzas
pasa a administrar los parámetros del sistema, con la fecha de cierre reservada a Gerencia (ADR-0049).

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-MEN-001 | El menú lateral se organiza en cinco grupos: Dashboard; Ciclo Operativo Diario (facturas, cuentas por cobrar, ingresos, gastos y cuentas por pagar); Control Financiero Contable (presupuesto, asientos contables, reportes y bitácora de operaciones); Catálogos (clientes, proveedores, productos y servicios, categorías financieras con cuentas contables anidadas e impuestos y retenciones); y Configuración (parámetros del sistema). Cada enlace respeta la política de su pantalla y no se muestra a un perfil sin acceso. | Cambio solicitado; ADR-0049 | No aplica | `_Layout.cshtml`, `site.css` (`.sidebar-link--sub`) | `CatalogAuthorizationTests` (políticas por ruta), `JournalEntriesAuthorizationTests` | Verificado |
| RF-MEN-002 | La pantalla de Consultas se retira por completo: no queda enlace, ruta, controlador, vistas, modelos ni pruebas; cada listado conserva sus propios filtros. | Cambio solicitado; ADR-0049 | No aplica | `QueriesController`, `Views/Queries/*`, `Models/Queries/*` y `QueriesAuthorizationTests` retirados | — | Descartado |
| RF-MEN-003 | Gerencia y Finanzas entran a Parámetros; Finanzas solo cambia los días de anticipación. Solo Gerencia define el límite, los datos de la empresa, el período fiscal y la escala de renta, y reabre y cierra meses. | Cambio solicitado; regla 16; ADR-0049, ADR-0054 y ADR-0059 | UC-CFG-001, UC-CFG-002 | `SystemPolicies.ManageSystemParameters`, `SystemPolicies.ManageAccountingPeriods`, `SystemParametersController`, `Views/SystemParameters/Index.cshtml` | `CatalogAuthorizationTests.ManageSystemParametersPolicy_AllowsManagementAndFinance`, `ManageAccountingPeriodsPolicy_AllowsManagementOnly`, `FiscalPeriodEndpoint_RequiresManageAccountingPeriods`, `SystemParametersAuthorizationTests` | Verificado |

## Matriz de la estimación del impuesto sobre la renta (RF-99 de CLAUDE.md)

Trabajo del 09/11/2026 que quedaba sin documentar (regla 17 de `CLAUDE.md`, ADR-0059). En este archivo
RF-99 ya nombraba el límite de crédito del cliente (matriz de facturación); para no renombrar códigos
existentes, la estimación de renta se identifica aquí como RF-REN-001 a RF-REN-003, que corresponden al
"RF-99" de `CLAUDE.md` y del código.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RF-REN-001 | Finanzas y Gerencia registran pagos del impuesto sobre la renta a Hacienda (período fiscal al que se aplican, cuenta de efectivo, medio de pago, monto, referencia y fecha no anterior al inicio de ese período); sobre el límite solo Gerencia; solo Gerencia anula, con reversión del asiento (débito 1.3.03, crédito efectivo). | Regla 17; RF-85 del documento final | CU-26 | `IncomeTaxPayment`, `IIncomeTaxService.RegisterPaymentAsync`/`CancelPaymentAsync`, `IncomeTaxPaymentsController` (ruta `/pagos-renta`), migración `AddIncomeTaxEstimate` | `SqlServerIncomeTaxServiceScenarioTests`, `IncomeTaxAuthorizationTests`, `AssistantRestrictedRoutesTests` | Verificado |
| RF-REN-002 | La estimación del impuesto sobre la renta de un período fiscal toma la renta bruta (ingresos) y la renta neta (utilidad del estado de resultados), aplica la escala de tramos vigente o la tarifa general si la renta bruta supera el umbral, y resta retenciones del 2 % y pagos de renta vigentes; se consulta en pantalla, PDF y Excel. | Regla 17; RF-84 y RF-66 del documento final | CU-22, CU-26 | `IncomeTaxEstimate.Calculate`, `IIncomeTaxService.GetEstimateAsync`, `ReportsController` (ruta `/reportes/estimacion-renta`) | `IncomeTaxEstimateTests`, `SqlServerIncomeTaxServiceScenarioTests` | Verificado |
| RF-REN-003 | Gerencia cambia la escala de renta en Parámetros; cada cambio queda en `EscalaRentaHistorial` y recalcula toda estimación. Finanzas la ve en solo lectura. | Regla 17; RF-84 y CU-06 del documento final | CU-06 | `SystemParameters.UpdateIncomeTaxScale`, `IIncomeTaxService.UpdateScaleAsync`, `SystemParametersController.UpdateIncomeTaxScale` (política `ManageAccountingPeriods`) | `IncomeTaxAuthorizationTests.Finance_CannotChangeTheIncomeTaxScale`, `SqlServerIncomeTaxServiceScenarioTests` | Verificado |

## Matriz del paquete v9 (correcciones de cierre)

Correcciones finales para que el código haga exactamente lo que dice el documento (ADR-0059). Ninguna
agrega migraciones, pantallas, rutas, reportes ni columnas.

| ID | Enunciado verificable | Fuente | Caso de uso | Implementación | Prueba | Estado |
|---|---|---|---|---|---|---|
| RN-V9-001 | Toda fecha de negocio sale de la hora local del `TimeProvider`: el ingreso de una factura de contado toma la fecha de la factura; "hoy" de cuentas vencidas y los valores por defecto de los formularios usan `GetLocalNow()`. | Bloque 1; RF-19 y RF-59 del documento | CU-10, CU-21 | `InvoiceService.ConfirmAsync`, `ReceivableService`/`PayableService.GetOverdueSummaryAsync`, controladores y acciones GET con `TimeProvider` | `SqlServerLocalBusinessDateScenarioTests` (23:30 del último día del mes en Costa Rica) | Verificado |
| RN-V9-002 | El ingreso de un abono es el monto recibido (sin la retención); el saldo baja por monto + retención y el asiento no cambia. | Bloque 2; CU-12 paso 7 y diccionario `Ingresos.Amount` del documento | CU-12 | `ReceivablePaymentService.RegisterAsync` (`Income.FromReceivablePayment` con `payment.Amount`) | `SqlServerReceivablePaymentServiceScenarioTests.Register_WithWithholding_IncomeIsTheReceivedAmountAndEntryBalances`, `Register_WithAWithholding_ReducesTheBalanceByTheAmountPlusTheWithholding` | Verificado |
| RN-V9-003 | El flujo de efectivo mensual sale de las líneas de asiento de las cuentas de efectivo (débitos = entradas, créditos = salidas), sin asientos anulados ni sus reversiones; sin filtro excluye los traspasos y con filtro los separa en recibidos y enviados; "Pagos de IVA" son los créditos a efectivo de las liquidaciones. Su neto cuadra con la variación del saldo de efectivo en la balanza. | Bloque 3; RF-66 del documento | CU-22 | `ReportService.GetCashFlowReportAsync` | `SqlServerCashFlowFromJournalScenarioTests`, `SqlServerReportServiceScenarioTests.GetCashFlowReportAsync_*`, `SqlServerCashAndVatEndToEndScenarioTests` | Verificado |
| RN-V9-004 | La retención de un abono anulado deja de contar como ya retenida. | Bloque 4; RF-09 del documento | CU-12 | `ReceivablePaymentService` (`alreadyWithheld` solo con abonos vigentes) | `SqlServerReceivablePaymentServiceScenarioTests.Register_AfterCancellingAPaymentWithWithholding_AcceptsTheSameWithholdingAgain` | Verificado |
| RN-V9-005 | El reporte "IVA del período" suma en compras solo el IVA acreditable; la base de compras incluye todos los gastos confirmados. | Bloque 5; CU-22 del documento | CU-22 | `ReportService.GetVatPeriodReportAsync` | `SqlServerVatPeriodCreditableScenarioTests` | Verificado |
| RN-V9-006 | El límite de autorización y los datos de la empresa solo los cambia Gerencia; Finanzas solo los días de anticipación (ver RF-CFG-002 y RN-CFG-002). | Bloque 6; CU-06 y Tabla 10 del documento | CU-06 | `SystemParametersController.UpdateCompany` (`ManageAccountingPeriods`), `SystemParametersService` (`actorIsManagement`), vista de Parámetros | `SystemParametersAuthorizationTests`, `SqlServerSystemParametersScenarioTests.Finance_OnlyChangesTheAlertDays_WhileManagementDefinesTheLimit` | Verificado |
| RN-V9-007 | El listado y el detalle de traspasos, liquidaciones de IVA y pagos de renta exigen `ConfirmFinancialTransactions` (Gerencia y Finanzas); el Asistente recibe 403. | Bloque 7; CU-24, CU-25 y CU-26 del documento | CU-24, CU-25, CU-26 | `CashTransfersController`, `VatSettlementsController`, `IncomeTaxPaymentsController` | `AssistantRestrictedRoutesTests` | Verificado |
| RN-V9-008 | Se opera en los meses aún abiertos del período fiscal inmediatamente anterior (ver RF-CIE-002). | Bloque 8; RF-55 y CU-06 del documento | CU-06 | `ClosingDateGuard` | `SqlServerPreviousFiscalPeriodScenarioTests` | Verificado |
| RN-V9-009 | El usuario crea cuentas contables nuevas solo de tercer nivel dentro de 1.1, 4.1, 4.2, 4.3, 5.1, 5.2 y 5.3 (sin Pasivo ni Patrimonio). Decisión del usuario del 10/11/2026 durante el bloque 9: el código permitía 2.1, 2.2 y 3.1 y no 5.3. | Regla 1 de `CLAUDE.md`; RF-01 del documento | CU-07 | `LedgerAccountGroups.DetailParentCodes`, `LedgerAccountService.CreateAsync`/`GetParentOptionsAsync` | `SqlServerLedgerAccountScenarioTests` | Verificado |
| RN-V9-010 | Las pruebas SQL se aíslan de los datos de la base de desarrollo dentro de su transacción revertida (liquidaciones vigentes y cuentas abiertas) y usan un reloj fijo cuando la regla depende de la fecha. | Bloque 9 | No aplica | `SqlTestIsolation`, `FixedLocalTimeProvider` | Suite completa con `SISTEMA_FINANCIERO_RUN_SQL_TESTS=1`: 847 de 847 | Verificado |

## Equivalencias con el documento final

El repositorio conserva sus códigos (no se renombran en comentarios, pruebas ni `docs/`). Estas tablas
hacen la correspondencia por contenido con los requerimientos del Anexo A (RF-01 a RF-85) y los casos
de uso del Anexo B (CU-01 a CU-26) del documento final.

### Requerimientos funcionales

| Documento final | Repositorio |
|---|---|
| RF-01 | RF-CTA-001, RN-CTA-007, RN-CTA-008, RN-V9-009 |
| RF-02 | RN-CTA-003, RN-CTA-002, RN-CTA-004 (cuentas de sistema) |
| RF-03 | RN-CTA-001, RN-CTA-007 |
| RF-04 | RN-CTA-002, RF-CTA-003 |
| RF-05 | RN-CTA-004, RF-CTA-001 |
| RF-06 | RF-CTA-001, RF-CAT-001 |
| RF-07 | RF-INV-003 |
| RF-08 | RF-INV-006, RF-INV-009 |
| RF-09 | RN-INV-014, RF-95, RN-RET-003, RN-V9-004 |
| RF-10 | RN-INV-006 |
| RF-11 | RF-INV-011, RF-CON-001 |
| RF-12 | RF-100 |
| RF-13 | RF-GAS-002 |
| RF-14 | RF-GAS-005 |
| RF-15 | RF-GAS-006, RF-GAS-007 |
| RF-16 | RF-GAS-005 y las reglas RN-GAS de saldo |
| RF-17 | RF-GAS-008, RF-CON-001 |
| RF-18 | RF-GAS-005 |
| RF-19 | RF-INV-003, RF-INV-008, RF-INV-009, RN-V9-001, RN-V9-002 |
| RF-20 | RF-INV-010 |
| RF-21 | RF-INV-010, RF-CTA-003 |
| RF-22 | RN-INV-015 |
| RF-23 | RF-INV-011 |
| RF-24 | RF-CON-003 |
| RF-25 | RF-GAS-001, RF-94 |
| RF-26 | RF-GAS-002, RF-GAS-003 |
| RF-27 | RF-GAS-001, RF-GAS-002 |
| RF-28 | RF-94 |
| RF-29 | RF-PRE-004 |
| RF-30 | RF-GAS-008 |
| RF-31 | RF-INV-001, RN-INV-007 |
| RF-32 | RF-INV-002, RF-INV-007 |
| RF-33 | RN-INV-003, RN-INV-008, RN-INV-009 |
| RF-34 | RF-34, RF-INV-003 |
| RF-35 | RN-INV-001, RF-NCR-002 |
| RF-36 | RF-INV-011 |
| RF-37 | RF-INV-005, RF-88 |
| RF-38 | RF-PRE-001, RF-PRE-002 |
| RF-39 | RF-PRE-001 |
| RF-40 | RF-PRE-003 |
| RF-41 | RF-PRE-003, RF-CON-005 |
| RF-42 | RF-CON-005 (el indicador de presupuesto del inicio no tiene fila propia) |
| RF-43 | RF-IMP-001, RF-IMP-002, RN-IMP-006 |
| RF-44 | RF-RET-001, RN-RET-003 |
| RF-45 | RF-IMP-001, RF-RET-001, RN-IMP-003 |
| RF-46 | RN-IMP-005, RN-INV-009, RF-93 |
| RF-47 | RN-INV-014 |
| RF-48 | RN-INV-002, RN-INV-003, RN-INV-014 |
| RF-49 | RN-V9-005 (reportes de IVA del período y de retenciones por clientes; sin fila propia anterior) |
| RF-50 | RF-CAT-002 a RF-CAT-004, RF-51 |
| RF-51 | RF-CAT-001 a RF-CAT-004 |
| RF-52 | RN-CAT-002, RF-50 |
| RF-53 | RF-93, RF-99 (límite de crédito del cliente de la matriz de facturación) |
| RF-54 | RN-CTA-008, RF-IMP-002 |
| RF-55 | RF-55, RF-CFG-001 a RF-CFG-003, RF-CIE-002, RN-V9-008 |
| RF-56 | RN-INV-001, RF-INV-004, RF-GAS-004, RF-NCR-001 |
| RF-57 | RF-AJU-001, RF-AJU-002 |
| RF-58 | RF-CAT-001, RF-INV-011, RF-GAS-008, RF-ASI-002 |
| RF-59 | RF-CON-001, RN-V9-001 |
| RF-60 | RF-100, RF-ASI-002 |
| RF-61 | RN-INV-013, RNF-AUD-001 |
| RF-62 | RF-CON-003, RF-CON-004 |
| RF-63 | RF-CON-005, RF-CFG-004 |
| RF-64 | RF-CON-007, RF-CON-008 (y la antigüedad de cuentas por cobrar, RF-98 de `CLAUDE.md`) |
| RF-65 | Sin fila propia: libro diario, libro mayor, balanza y estado de resultados (bloque 6 del paquete de mejoras finales) |
| RF-66 | RN-V9-003, RN-V9-005, RF-REN-002 |
| RF-67 | RF-CON-006 |
| RF-68 | RF-CON-003 a RF-CON-008 |
| RF-69 | RF-SEG-001, RNF-SEG-002 |
| RF-70 | RF-SEG-002 |
| RF-71 | RF-USR-001 a RF-USR-004 |
| RF-72 | RF-USR-005, RF-USR-007 |
| RF-73 | RF-SEG-003 |
| RF-74 | RN-SEG-003, RF-SEG-004, RN-V9-007 |
| RF-75 | RF-MEN-001, RN-SEG-005 |
| RF-76 | RF-INV-006, RF-GAS-003, RF-GAS-005, RF-CFG-002, RF-REN-001 |
| RF-77 | RN-INV-013, RNF-AUD-001 |
| RF-78 | RF-ASI-001 |
| RF-79 | RF-ASI-001 |
| RF-80 | RF-NCR-001 |
| RF-81 | RF-NCR-001, RF-NCR-002 |
| RF-82 | Traspasos (RF-96 de `CLAUDE.md`; paquete v8, bloque 1; sin fila propia anterior) |
| RF-83 | Liquidación del IVA (RF-97 de `CLAUDE.md`; paquete v8, bloque 2; sin fila propia anterior) |
| RF-84 | RF-REN-002, RF-REN-003 (RF-99 de `CLAUDE.md`) |
| RF-85 | RF-REN-001 (RF-99 de `CLAUDE.md`) |

Códigos del repositorio sin equivalente en el documento final: RF-SEG-005 (sin registro público),
RF-SEG-006 (comando de arranque de seguridad), RF-SEG-007 (pantalla de la matriz de perfiles, apoyo de
RF-74), RF-CAT-005 y RF-CTA-002 (categorías financieras, retiradas), RF-ASI-003, y los descartados
RF-CAT-006, RF-MON-001, RF-MON-002, RF-IMP-002, RF-CON-002 y RF-MEN-002.

### Casos de uso

| Documento final | Repositorio |
|---|---|
| CU-01 Iniciar y cerrar sesión | UC-SEG-001, UC-SEG-002 |
| CU-02 Cambiar contraseña | UC-SEG-003 |
| CU-03 Administrar usuarios | UC-USR-001, UC-USR-002 |
| CU-04 Gestionar clientes y proveedores | UC-CAT-001, UC-CAT-002, UC-CAT-003 |
| CU-05 Gestionar productos y servicios | UC-CAT-001, UC-CAT-004 |
| CU-06 Definir parámetros de operación | UC-CFG-001, UC-CFG-002 |
| CU-07 Gestionar cuentas contables | UC-CAT-006, UC-ASI-001 |
| CU-08 Gestionar tipos de impuesto y retención | UC-CAT-007, UC-CAT-008 |
| CU-09 Registrar factura en borrador | UC-INV-001 |
| CU-10 Confirmar factura | UC-INV-002 |
| CU-11 Consultar y exportar facturas | UC-INV-004 |
| CU-12 Registrar abono | UC-INV-005 |
| CU-13 Registrar ingreso manual | UC-INV-005 |
| CU-14 Registrar gasto en borrador | UC-GAS-001, UC-GTO-001 |
| CU-15 Confirmar gasto | UC-GAS-002, UC-GTO-002 |
| CU-16 Registrar pago a proveedor | UC-GAS-004 |
| CU-17 Anular operación confirmada | UC-INV-003, UC-GAS-003 |
| CU-18 Ajustar saldo de cuenta por cobrar o por pagar | UC-AJU-001 |
| CU-19 Registrar presupuesto mensual | UC-PRE-001 |
| CU-20 Consultar ejecución presupuestaria | UC-PRE-001, UC-REP-003 |
| CU-21 Consultar operaciones y trazabilidad | UC-CON-001, UC-ASI-002 |
| CU-22 Generar y exportar reportes | UC-REP-001 a UC-REP-007 |
| CU-23 Registrar nota de crédito | UC-NCR-001 |
| CU-24 Registrar traspaso entre cuentas de efectivo | Sin caso de uso propio en el repositorio (paquete v8, bloque 1) |
| CU-25 Registrar liquidación mensual del IVA | Sin caso de uso propio en el repositorio (paquete v8, bloque 2) |
| CU-26 Registrar pago del impuesto sobre la renta | Sin caso de uso propio en el repositorio (RF-REN-001) |

Casos de uso del repositorio sin equivalente en el documento final: UC-SEG-004 (inicializar
seguridad), UC-SEG-005 (consultar perfiles y permisos), UC-CAT-005 (categorías financieras, retiradas)
y UC-MON-001 (tipos de cambio, descartado).
