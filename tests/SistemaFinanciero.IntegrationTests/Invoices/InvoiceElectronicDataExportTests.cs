using System.Text.Json;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.IntegrationTests.Invoices;

/// <summary>
/// Genera los datos de la factura preparados para el formato 4.4 de Hacienda (bloque 3 del paquete de
/// mejoras finales, RF-88) con datos sintéticos, sin base de datos ni HTTP.
/// </summary>
public sealed class InvoiceElectronicDataExportTests
{
    private static readonly SystemParametersModel SampleCompanyData = new(
        500_000m, 7, DateOnly.MinValue,
        "Soporte Experto", "3-101-123456", "San José, Costa Rica", "2222-2222", "info@soportexperto.cr", "620301",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1");

    private static readonly InvoiceLineModel ServiceLine = new(
        Guid.NewGuid(), 1, Guid.NewGuid(), "SRV-0001", CatalogItemType.Service, "8311500000000",
        "Servicio de soporte técnico", "Unidad", 1m, 1000m, 1000m, 0m, 1000m,
        "IVA-13", "IVA 13%", 13m, "08", 1000m, 130m, 1130m);

    private static readonly InvoiceLineModel GoodsLine = new(
        Guid.NewGuid(), 2, Guid.NewGuid(), "PRD-0001", CatalogItemType.Product, "8311500000001",
        "Router inalámbrico", "Unidad", 2m, 500m, 1000m, 100m, 900m,
        "IVA-EXENTO", "IVA exento", 0m, "01", 900m, 0m, 900m);

    private static InvoiceModel CreateInvoice() => new(
        Guid.NewGuid(), 42, Guid.NewGuid(), "Cliente de prueba",
        IdentificationType.LegalEntity, "3101000001", "cliente@example.test", "8888-8888",
        VatCondition.Normal,
        Guid.NewGuid(), "Categoría de prueba",
        new DateOnly(2026, 1, 10), PaymentTerm.Credit30, new DateOnly(2026, 2, 9), InvoiceStatus.Confirmed,
        2000m, 100m, 1900m, 130m, 2030m,
        DateTimeOffset.UtcNow, null, null, null, null,
        Guid.NewGuid(), "Usuario de prueba", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "v1",
        [ServiceLine, GoodsLine], null);

    [Fact]
    public void BuildJson_IncludesEveryRequiredSection()
    {
        InvoiceModel invoice = CreateInvoice();

        string json = InvoiceElectronicDataExport.BuildJson(invoice, SampleCompanyData, "Usuario de prueba");
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;

        Assert.Equal("620301", root.GetProperty("emisor").GetProperty("codigoActividad").GetString());
        Assert.Equal("02", root.GetProperty("receptor").GetProperty("tipoIdentificacion").GetString());
        Assert.Equal("3101000001", root.GetProperty("receptor").GetProperty("identificacion").GetString());
        Assert.Equal("02", root.GetProperty("condicionVenta").GetProperty("codigo").GetString());
        Assert.Equal(30, root.GetProperty("condicionVenta").GetProperty("plazoDias").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("medioPago").ValueKind);
        Assert.Equal(2, root.GetProperty("lineas").GetArrayLength());

        JsonElement summary = root.GetProperty("resumen");
        Assert.Equal(1000m, summary.GetProperty("totalServiciosGravados").GetDecimal());
        Assert.Equal(900m, summary.GetProperty("totalMercanciasExentas").GetDecimal());
        Assert.Equal(100m, summary.GetProperty("totalDescuentos").GetDecimal());
        Assert.Equal(1900m, summary.GetProperty("ventaNeta").GetDecimal());
        Assert.Equal(2030m, summary.GetProperty("totalComprobante").GetDecimal());
    }

    [Fact]
    public void BuildJson_IncludesThePaymentMethodForACashInvoice()
    {
        InvoiceModel invoice = CreateInvoice() with { PaymentTerm = PaymentTerm.Cash, PaymentMethod = PaymentMethod.SinpeMobile };

        string json = InvoiceElectronicDataExport.BuildJson(invoice, SampleCompanyData, "Usuario de prueba");
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal("01", document.RootElement.GetProperty("condicionVenta").GetProperty("codigo").GetString());
        Assert.Equal(0, document.RootElement.GetProperty("condicionVenta").GetProperty("plazoDias").GetInt32());
        Assert.Equal("06", document.RootElement.GetProperty("medioPago").GetProperty("codigo").GetString());
    }

    [Fact]
    public void BuildWorkbook_GeneratesAValidWorkbookWithALineRowPerInvoiceLine()
    {
        InvoiceModel invoice = CreateInvoice();

        byte[] workbook = InvoiceElectronicDataExport.BuildWorkbook([invoice], "Usuario de prueba");

        Assert.True(workbook.Length > 0);
        Assert.Equal(0x50, workbook[0]); // Firma ZIP ("PK") de un archivo .xlsx válido.
        Assert.Equal(0x4B, workbook[1]);
    }
}
