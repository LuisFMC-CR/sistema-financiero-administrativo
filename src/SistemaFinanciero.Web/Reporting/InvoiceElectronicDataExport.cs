using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>
/// Prepara los datos de una factura confirmada para el formato 4.4 de Hacienda (bloque 3 del paquete
/// de mejoras finales, RF-88, completado en el bloque 2 del paquete v7): no emite, firma ni transmite
/// nada ante Hacienda, solo guarda un JSON (una factura) o un libro de Excel (varias, de un período)
/// con los datos ya clasificados para el traslado manual a la plataforma externa.
/// </summary>
public static class InvoiceElectronicDataExport
{
    /// <summary>
    /// Código de Hacienda "99 Otros" para el descuento de cada línea (bloque 2 del paquete v7): no se
    /// encontró un código específico de "descuento comercial" en el catálogo disponible; pendiente de
    /// validar contra el Anexo 4.4 oficial.
    /// </summary>
    private const string OtherDiscountCode = "99";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static string BuildJson(InvoiceModel invoice, SystemParametersModel? companyData)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var data = new
        {
            factura = new
            {
                numero = invoice.Number,
                fecha = invoice.IssueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                estado = invoice.Status.ToString(),
                claveOficial = invoice.ElectronicInvoiceKey,
                consecutivoOficial = invoice.ElectronicInvoiceNumber,
            },
            emisor = new
            {
                nombre = companyData?.CompanyName,
                nombreComercial = companyData?.CompanyTradeName,
                cedulaJuridica = companyData?.CompanyLegalId,
                provincia = companyData?.CompanyProvinceCode,
                canton = companyData?.CompanyCantonCode,
                distrito = companyData?.CompanyDistrictCode,
                otrasSenas = companyData?.CompanyAddress,
                telefono = companyData?.CompanyPhone,
                correo = companyData?.CompanyEmail,
                codigoActividad = companyData?.CompanyActivityCode,
            },
            receptor = new
            {
                tipoIdentificacion = invoice.CustomerIdentificationType.ToHaciendaCode(),
                identificacion = invoice.CustomerIdentification,
                nombre = invoice.CustomerName,
                correo = invoice.CustomerEmail,
                actividadEconomica = invoice.CustomerActivityCode,
            },
            condicionVenta = new
            {
                codigo = invoice.PaymentTerm.ToSaleConditionCode(),
                plazoDias = invoice.PaymentTerm.ToCreditDays(),
            },
            medioPago = invoice.PaymentMethod is PaymentMethod paymentMethod
                ? new { codigo = paymentMethod.ToHaciendaCode() }
                : null,
            lineas = invoice.Lines
                .OrderBy(line => line.Position)
                .Select(line => new
                {
                    numero = line.Position,
                    cabys = line.CabysCode,
                    codigoComercial = line.CatalogItemCode,
                    descripcion = line.Description,
                    cantidad = line.Quantity,
                    unidadMedida = line.UnitOfMeasure,
                    precioUnitario = line.UnitPrice,
                    descuento = line.DiscountAmount,
                    codigoDescuento = line.DiscountAmount > 0 ? OtherDiscountCode : null,
                    @base = line.NetAmount,
                    codigoTarifa = line.TaxHaciendaRateCode,
                    tarifa = line.TaxRate,
                    iva = line.TaxAmount,
                    total = line.TotalAmount,
                })
                .ToArray(),
            resumen = BuildSummary(invoice),
        };

        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public static byte[] BuildWorkbook(IReadOnlyList<InvoiceModel> invoices, string generatedByUserName)
    {
        ArgumentNullException.ThrowIfNull(invoices);

        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Datos de facturas");

        sheet.Cell(1, 1).Value = "Exportación de datos de facturas (RF-88)";
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = $"Generado: {DateTimeOffset.UtcNow.ToLocalTime():dd/MM/yyyy HH:mm}";
        sheet.Cell(3, 1).Value = $"Usuario: {generatedByUserName}";

        string[] columns =
        [
            "Factura", "Clave oficial", "Consecutivo oficial", "Fecha", "Receptor", "Tipo ident.",
            "Identificación", "Actividad receptor", "Línea", "CABYS", "Código comercial", "Descripción",
            "Cantidad", "Unidad", "Precio unitario", "Descuento", "Código descuento", "Base",
            "Código tarifa", "Tarifa", "IVA", "Total línea",
        ];
        int row = 5;
        for (int index = 0; index < columns.Length; index++)
        {
            sheet.Cell(row, index + 1).Value = columns[index];
        }

        sheet.Row(row).Style.Font.SetBold();
        row++;

        foreach (InvoiceModel invoice in invoices.OrderBy(invoice => invoice.IssueDate).ThenBy(invoice => invoice.Number))
        {
            foreach (InvoiceLineModel line in invoice.Lines.OrderBy(line => line.Position))
            {
                sheet.Cell(row, 1).Value = invoice.Number;
                sheet.Cell(row, 2).Value = invoice.ElectronicInvoiceKey ?? string.Empty;
                sheet.Cell(row, 3).Value = invoice.ElectronicInvoiceNumber ?? string.Empty;
                sheet.Cell(row, 4).Value = invoice.IssueDate.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 4).Style.DateFormat.Format = "dd/mm/yyyy";
                sheet.Cell(row, 5).Value = invoice.CustomerName;
                sheet.Cell(row, 6).Value = invoice.CustomerIdentificationType.ToHaciendaCode();
                sheet.Cell(row, 7).Value = invoice.CustomerIdentification;
                sheet.Cell(row, 8).Value = invoice.CustomerActivityCode ?? string.Empty;
                sheet.Cell(row, 9).Value = line.Position;
                sheet.Cell(row, 10).Value = line.CabysCode ?? string.Empty;
                sheet.Cell(row, 11).Value = line.CatalogItemCode ?? string.Empty;
                sheet.Cell(row, 12).Value = line.Description;
                sheet.Cell(row, 13).Value = line.Quantity;
                sheet.Cell(row, 14).Value = line.UnitOfMeasure;
                sheet.Cell(row, 15).Value = line.UnitPrice;
                sheet.Cell(row, 15).Style.NumberFormat.Format = "#,##0.0000";
                sheet.Cell(row, 16).Value = line.DiscountAmount;
                sheet.Cell(row, 16).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 17).Value = line.DiscountAmount > 0 ? OtherDiscountCode : string.Empty;
                sheet.Cell(row, 18).Value = line.NetAmount;
                sheet.Cell(row, 18).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 19).Value = line.TaxHaciendaRateCode ?? string.Empty;
                sheet.Cell(row, 20).Value = line.TaxRate;
                sheet.Cell(row, 21).Value = line.TaxAmount;
                sheet.Cell(row, 21).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 22).Value = line.TotalAmount;
                sheet.Cell(row, 22).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }
        }

        sheet.Columns().AdjustToContents();

        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static object BuildSummary(InvoiceModel invoice)
    {
        // Una línea de tarifa 0 % es "no sujeto" cuando no tiene código de Hacienda (la tarifa de
        // sistema No sujeto, bloque 1 del paquete v7, no tiene uno); con código es "exento" (p. ej.
        // la tarifa Exento, código 10). Antes de este bloque, todo lo de tarifa 0 % se contaba como
        // exento sin distinguir.
        static bool IsNotSubject(InvoiceLineModel line) => line.TaxRate == 0 && line.TaxHaciendaRateCode is null;
        static bool IsExempt(InvoiceLineModel line) => line.TaxRate == 0 && line.TaxHaciendaRateCode is not null;

        decimal ServiceTotal(Func<InvoiceLineModel, bool> predicate) => invoice.Lines
            .Where(line => line.CatalogItemType == CatalogItemType.Service && predicate(line))
            .Sum(line => line.NetAmount);

        decimal GoodsTotal(Func<InvoiceLineModel, bool> predicate) => invoice.Lines
            .Where(line => line.CatalogItemType == CatalogItemType.Product && predicate(line))
            .Sum(line => line.NetAmount);

        return new
        {
            totalServiciosGravados = ServiceTotal(line => line.TaxRate > 0),
            totalServiciosExentos = ServiceTotal(IsExempt),
            totalServiciosNoSujetos = ServiceTotal(IsNotSubject),
            totalMercanciasGravadas = GoodsTotal(line => line.TaxRate > 0),
            totalMercanciasExentas = GoodsTotal(IsExempt),
            totalMercanciasNoSujetas = GoodsTotal(IsNotSubject),
            totalDescuentos = invoice.Lines.Sum(line => line.DiscountAmount),
            ventaNeta = invoice.Lines.Sum(line => line.NetAmount),
            ivaPorTarifa = invoice.Lines
                .GroupBy(line => line.TaxRate)
                .OrderBy(group => group.Key)
                .Select(group => new { tarifa = group.Key, monto = group.Sum(line => line.TaxAmount) })
                .ToArray(),
            totalComprobante = invoice.TotalAmount,
        };
    }
}
