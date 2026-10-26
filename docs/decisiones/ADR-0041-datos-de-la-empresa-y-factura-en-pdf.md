# ADR-0041: Datos de la empresa y factura interna en PDF

- **Fecha:** 2026-10-26
- **Estado:** Aceptado

## Contexto

Dos funcionalidades que el documento académico exige y que ninguna parte del roadmap numerado había
cubierto todavía: los datos de la empresa como parámetro (CU-06, RF-55) y la factura interna en PDF
(regla de negocio 15, RF-37), que es una salida propia, distinta de los seis reportes del paso 5.

## Decisión

- **Datos de la empresa**: nombre y cédula jurídica obligatorios; dirección, teléfono y correo
  opcionales. Es una operación propia de `SystemParameters` (`UpdateCompany`), separada de
  `UpdateValues` y de `ChangeClosingDate`, con su propio formulario en `/parametros` y su propia
  bitácora, `CompanyDataChange`. A diferencia de `SystemParameterChange` (que guarda un par
  anterior/nuevo por campo), `CompanyDataChange` guarda una fotografía completa de los cinco campos
  en cada cambio: con diez columnas de par anterior/nuevo no se ganaba nada que la fila anterior del
  historial no diera ya, al ser una tabla de solo anexo.
- **Factura en PDF**: `InvoicesController.Pdf` (ruta `/facturacion/facturas/{id}/pdf`) solo permite
  confirmadas o anuladas (un borrador no tiene número ni totales definitivos); un intento sobre un
  borrador redirige al detalle con un mensaje, igual criterio que "sin datos" en los reportes.
  `InvoicePdfDocument` (QuestPDF, público para las pruebas) compone el encabezado con los datos
  vigentes de la empresa (o "—" si Gerencia todavía no los configuró), el detalle por línea, el
  resumen de IVA agrupado por tarifa, el medio de pago solo si es de contado, la leyenda de documento
  interno y, si está anulada, un bloque "ANULADA" con el motivo.
- **Medio de pago en `InvoiceModel`**: no existía ningún campo para mostrarlo (ni en la pantalla de
  detalle, que tampoco lo mostraba). Se agrega `PaymentMethod?`, resuelto en
  `InvoiceService.GetAsync` uniendo con el `Income` que generó la factura de contado al confirmarse;
  en una factura a crédito, o en un borrador, queda en `null` porque no existe ese ingreso.
- **"Código" de línea**: se usa el número de posición de la línea, igual que ya muestra la pantalla
  de detalle; una línea de factura no siempre tiene un artículo de catálogo asociado.
- **"Datos del cliente"**: solo el nombre, igual que la pantalla de detalle ya muestra; no se agregan
  campos nuevos a `InvoiceModel` para esto.

## Consecuencias

- Nuevo: `Domain/Parameters/CompanyDataChange.cs`,
  `Infrastructure/Persistence/Configurations/CompanyDataChangeConfiguration.cs`,
  `Web/Models/Parameters/CompanyDataViewModel`, `Web/Reporting/InvoicePdfDocument.cs`. Migraciones
  `AddCompanyData` (columnas en `ParametrosSistema`, tabla `ParametrosEmpresaHistorial`), aplicadas a
  la base de desarrollo.
- Modificado: `SystemParameters.cs` (datos de la empresa y `UpdateCompany`),
  `SystemParametersContracts.cs`, `SystemParametersService.cs`, `SystemParametersConfiguration.cs`,
  `FinancialDbContext.cs`, `SystemParametersController.cs` y su vista (tercer formulario);
  `InvoiceContracts.cs` (`PaymentMethod` en `InvoiceModel`), `InvoiceService.cs` (lo resuelve en
  `GetAsync`), `InvoicesController.cs` (acción `Pdf`) y `Views/Invoices/Details.cshtml` (enlace
  "Exportar PDF").
- Verificación: 7 pruebas unitarias de dominio (`UpdateCompany`), 2 pruebas SQL reales de parámetros,
  3 de controlador, 4 de `InvoicePdfDocument` con datos sintéticos (contado, crédito, anulada, sin
  datos de empresa) y 2 pruebas SQL reales que confirman el medio de pago resuelto correctamente
  (contado) y ausente (crédito). 292 pruebas unitarias y 304 de integración pasan.
- Verificación manual: la app arranca y `/facturacion/facturas/{id}/pdf` y `/parametros` exigen
  inicio de sesión.
