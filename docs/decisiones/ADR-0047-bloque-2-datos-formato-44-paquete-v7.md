# ADR-0047: Bloque 2 del paquete v7 — Datos del formato 4.4 que faltaron

- **Fecha:** 2026-11-04
- **Estado:** Aceptado

## Contexto

El bloque 2 de `prompt_claude_code_mejoras_finales_v7.md` completa los datos que el formato 4.4 de
Hacienda necesita para la exportación de la factura interna (RF-88), sin emitir, firmar ni transmitir
nada: actividad económica del receptor, datos codificados del emisor, totales separados de "no
sujeto" frente a "exento" en el resumen, código de descuento por línea, clave y consecutivo oficiales
en la exportación, y la preparación del recibo electrónico de pago en el detalle de cuenta por cobrar.

## Decisiones

### 1. Provincia, cantón y distrito: solo la provincia tiene catálogo real

No existe en el repositorio ni tengo disponible un catálogo completo de cantones y distritos de Costa
Rica. Las 7 provincias sí son una lista fija y bien conocida, así que `SystemParameters.CompanyProvinceCode`
valida formato (1 dígito) **y** rango real (1 a 7); el selector de la pantalla de parámetros muestra sus
nombres desde una lista fija escrita en la propia vista (`CostaRicaProvinces`), sin tabla de catálogo
nueva. `CompanyCantonCode`/`CompanyDistrictCode` solo validan formato (2 dígitos exactos cada uno) y se
capturan como texto libre con ese patrón, sin selector dependiente. **Pendiente: completar el catálogo
real de cantones y distritos cuando esté disponible**, para entonces agregar el selector dependiente.

### 2. `CompanyAddress` se reetiqueta "Otras señas", sin cambiar de columna

El documento dice que la dirección existente "pasa a ser otras señas" ahora que la ubicación principal
la dan los tres códigos territoriales. Se interpretó como un cambio de **etiqueta**, no de columna ni
de propiedad: `SystemParameters.CompanyAddress`/`CompanyDataChange.CompanyAddress` siguen llamándose
igual en el código y en la base de datos; solo cambia el texto mostrado en el formulario
(`Views/SystemParameters/Index.cshtml`) y la clave del JSON exportado (`otrasSenas` en vez de
`direccion`). Evita una migración de renombrado de columna para un cambio que es puramente de
presentación.

### 3. Código de descuento de Hacienda: "99 Otros" sin condición

No se encontró en el repositorio ningún catálogo de códigos de descuento de Hacienda, y no tengo
acceso ahora para verificar contra el Anexo 4.4 oficial si existe un código específico de "descuento
comercial" (nota 20 del anexo, según el documento). Se usa `"99"` ("Otros") para toda línea con
`DiscountAmount > 0`, sin código cuando no hay descuento (`InvoiceElectronicDataExport.OtherDiscountCode`).
**Pendiente de validar contra el Anexo 4.4 oficial** cuando el usuario tenga acceso al documento; si
existe un código específico, el cambio es de una sola constante.

### 4. "No sujeto" frente a "Exento" en el resumen: por `TaxHaciendaRateCode`, no por tarifa

Antes de este bloque, `InvoiceElectronicDataExport.BuildSummary` contaba toda línea de tarifa 0 % como
"exenta", sin distinguir. El bloque 1 del paquete v7 ya había introducido la distinción de dominio: la
tarifa de sistema "No sujeto" no tiene código de Hacienda (`InvoiceLineTax.HaciendaRateCode` nulo),
mientras que "Exento" sí lo tiene (código `10`, corregido en el bloque 1). Este bloque usa esa misma
distinción ya existente (`line.TaxHaciendaRateCode is null` → no sujeto; `is not null` → exento) para
separar `totalServiciosNoSujetos`/`totalMercanciasNoSujetas` de los `...Exentos`/`...Exentas` ya
existentes, sin inventar un campo nuevo.

### 5. Clave y consecutivo oficiales ya existían en el dominio; solo faltaba usarlos

`Invoice.ElectronicInvoiceKey`/`ElectronicInvoiceNumber` (bloque 3 del paquete de mejoras finales) ya
estaban expuestos en `InvoiceModel`, sin que `InvoiceElectronicDataExport` los usara. Este bloque los
agrega a la sección `factura` del JSON y a las columnas del libro de Excel, sin ningún cambio de
modelo.

### 6. Saldo resultante por abono: cálculo de vista, sin campo nuevo

El detalle de cuenta por cobrar no guarda un saldo histórico por abono. En vez de agregar un campo
persistido, `Views/Receivables/Details.cshtml` calcula el saldo resultante de cada abono vigente en el
propio `.cshtml`: resta acumulativamente, en orden cronológico ascendente, el monto más la retención de
cada abono vigente desde `receivable.OriginalAmount`. Un abono anulado no participa del cálculo (ya no
afecta el saldo real, coherente con `Receivable.RevertPayment`) y no muestra saldo resultante.

## Consecuencias

- Nuevo: `Customer.ActivityCode`, `SystemParameters.CompanyTradeName`/`CompanyProvinceCode`/
  `CompanyCantonCode`/`CompanyDistrictCode` (y su espejo en `CompanyDataChange`), migraciones
  `AddCustomerActivityCode` y `AddCompanyTerritorialData`, `DomainRules.OptionalDigits`.
- Modificado: `Customer`, `SystemParameters`, `CompanyDataChange` (Domain); sus configuraciones EF
  Core; `CustomerCatalogContracts`, `SystemParametersContracts`, `InvoiceContracts` (`InvoiceModel.
  CustomerActivityCode`), `ReceivableContracts` (`ReceivableModel.InvoiceElectronicKey`) (Application);
  `CustomerCatalogService`, `SystemParametersService`, `InvoiceService` (`CustomerContact`/
  `GetCustomerContactAsync`/`MapInvoice`), `ReceivableService` (`InvoiceInfo`) (Infrastructure);
  `CustomersController`, `SystemParametersController`, `InvoiceElectronicDataExport` (Web); las vistas
  `Customers/_Form.cshtml`, `SystemParameters/Index.cshtml`, `Receivables/Details.cshtml`.
- `docs/02-requisitos-trazabilidad.md` ajusta RF-88 (RF-IMP y el bloque fundacional de facturación) y
  agrega las referencias de este bloque.
- Verificación: 712 pruebas en verde (336 unitarias + 376 de integración con
  `SISTEMA_FINANCIERO_RUN_SQL_TESTS=1`), incluidas pruebas nuevas de dominio (`SystemParameters`
  rechaza una provincia fuera de 1-7 y un cantón/distrito que no sean 2 dígitos, acepta nulos), de
  exportación (separa "no sujeto" de "exento", asigna el código 99 a una línea con descuento, incluye
  clave y consecutivo oficiales, nombre comercial y ubicación del emisor, actividad económica del
  receptor) y de `ReceivableService` (expone la clave oficial de la factura una vez registrada).
- Verificación manual en navegador: **pendiente**, misma limitación que el bloque 1 (sin contraseñas
  de los usuarios de prueba de Finanzas/Gerencia en este contexto).
