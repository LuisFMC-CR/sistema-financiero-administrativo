using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.IntegrationTests.CreditNotes;

public sealed class CreditNotePdfDocumentTests
{
    static CreditNotePdfDocumentTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void CreditNotePdfDocument_WithRefund_GeneratesAValidPdf()
    {
        CreditNoteModel note = new(
            Guid.NewGuid(),
            "NCR-000001",
            Guid.NewGuid(),
            42,
            "Cliente de prueba",
            new DateOnly(2026, 11, 6),
            "Devolución total",
            100m,
            13m,
            113m,
            0m,
            113m,
            0m,
            "Caja de pruebas",
            "DEV-0001",
            null,
            null,
            CreditNoteStatus.Active,
            DateTimeOffset.UtcNow,
            "Usuario de prueba",
            [
                new CreditNoteLineModel(
                    Guid.NewGuid(),
                    1,
                    "Servicio de soporte",
                    "unidad",
                    1m,
                    100m,
                    13m,
                    13m,
                    113m),
            ],
            "v1");
        SystemParametersModel companyData = new(
            500_000m, 7, DateOnly.MinValue,
            "Soporte Experto", "3-101-123456", "San José, Costa Rica", "2222-2222", "info@soportexperto.cr", "620301",
            "Soporte Experto CR", "1", "01", "02", "001", "00001",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1");

        byte[] pdf = new CreditNotePdfDocument(note, companyData).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }
}
