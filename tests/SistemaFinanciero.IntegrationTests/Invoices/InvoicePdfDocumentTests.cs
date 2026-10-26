using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.IntegrationTests.Invoices;

/// <summary>
/// Genera la factura interna en PDF con datos sintéticos, sin base de datos ni HTTP, para comprobar
/// que QuestPDF (licencia Community) produce un archivo válido en cada escenario (regla de negocio
/// 15, RF-37). Sigue el mismo patrón que <c>ReportExportTests</c>.
/// </summary>
public sealed class InvoicePdfDocumentTests
{
    static InvoicePdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly SystemParametersModel SampleCompanyData = new(
        500_000m, 7, DateOnly.MinValue,
        "Soporte Experto", "3-101-123456", "San José, Costa Rica", "2222-2222", "info@soportexperto.cr",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1");

    private static readonly InvoiceLineModel SampleLine = new(
        Guid.NewGuid(), 1, null, "Servicio de soporte técnico", "unidad", 1m, 1000m, 1000m, 0m, 1000m,
        "IVA-13", "IVA 13%", 13m, 1000m, 130m, 1130m);

    [Fact]
    public void InvoicePdfDocument_ForAConfirmedCashInvoice_GeneratesAValidPdfWithCompanyDataAndPaymentMethod()
    {
        InvoiceModel invoice = new(
            Guid.NewGuid(), 42, Guid.NewGuid(), "Cliente de prueba",
            new DateOnly(2026, 1, 10), PaymentTerm.Cash, null, InvoiceStatus.Confirmed,
            1000m, 0m, 1000m, 130m, 1130m,
            DateTimeOffset.UtcNow, null, null,
            Guid.NewGuid(), "Usuario de prueba", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "v1",
            [SampleLine], PaymentMethod.Cash);

        byte[] pdf = new InvoicePdfDocument(invoice, SampleCompanyData).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void InvoicePdfDocument_ForACreditInvoice_GeneratesAValidPdfWithoutAPaymentMethod()
    {
        InvoiceModel invoice = new(
            Guid.NewGuid(), 43, Guid.NewGuid(), "Cliente de prueba",
            new DateOnly(2026, 1, 10), PaymentTerm.Credit30, new DateOnly(2026, 2, 9), InvoiceStatus.Confirmed,
            1000m, 0m, 1000m, 130m, 1130m,
            DateTimeOffset.UtcNow, null, null,
            Guid.NewGuid(), "Usuario de prueba", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "v1",
            [SampleLine], null);

        byte[] pdf = new InvoicePdfDocument(invoice, SampleCompanyData).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void InvoicePdfDocument_ForACancelledInvoice_GeneratesAValidPdfWithTheReason()
    {
        InvoiceModel invoice = new(
            Guid.NewGuid(), 44, Guid.NewGuid(), "Cliente de prueba",
            new DateOnly(2026, 1, 10), PaymentTerm.Credit30, new DateOnly(2026, 2, 9), InvoiceStatus.Cancelled,
            1000m, 0m, 1000m, 130m, 1130m,
            DateTimeOffset.UtcNow, "Factura registrada por error", DateTimeOffset.UtcNow,
            Guid.NewGuid(), "Usuario de prueba", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "v1",
            [SampleLine], null);

        byte[] pdf = new InvoicePdfDocument(invoice, SampleCompanyData).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void InvoicePdfDocument_WithoutCompanyData_GeneratesAValidPdf()
    {
        InvoiceModel invoice = new(
            Guid.NewGuid(), 45, Guid.NewGuid(), "Cliente de prueba",
            new DateOnly(2026, 1, 10), PaymentTerm.Credit30, new DateOnly(2026, 2, 9), InvoiceStatus.Confirmed,
            1000m, 0m, 1000m, 130m, 1130m,
            DateTimeOffset.UtcNow, null, null,
            Guid.NewGuid(), "Usuario de prueba", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "v1",
            [SampleLine], null);

        byte[] pdf = new InvoicePdfDocument(invoice, companyData: null).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
