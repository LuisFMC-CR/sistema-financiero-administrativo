using System.Text;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.IntegrationTests.IncomeTax;

/// <summary>
/// RF-99: el reporte "Estimación del impuesto sobre la renta" se genera en PDF y Excel con datos
/// sintéticos (sin base de datos ni HTTP), con los montos de Excel como números (regla 8).
/// </summary>
public sealed class IncomeTaxEstimateExportTests
{
    static IncomeTaxEstimateExportTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    [Fact]
    public void PdfDocument_GeneratesAValidPdf()
    {
        byte[] pdf = new IncomeTaxEstimateReportPdfDocument(SampleResult()).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void ExcelWorkbook_WritesTheAmountsAsNumbers()
    {
        byte[] excel = ReportExcelExporter.BuildIncomeTaxEstimateWorkbook(SampleResult());

        using MemoryStream stream = new(excel);
        using XLWorkbook workbook = new(stream);
        IXLWorksheet sheet = workbook.Worksheet(1);

        IXLCell estimatedTaxCell = sheet.CellsUsed()
            .First(cell => cell.GetString() == "Impuesto estimado del período")
            .WorksheetRow()
            .Cell(4);
        Assert.Equal(XLDataType.Number, estimatedTaxCell.DataType);
        Assert.Equal(797_300m, estimatedTaxCell.GetValue<decimal>());
        Assert.Contains(sheet.CellsUsed(), cell => cell.GetString() == "Saldo estimado por pagar");
    }

    [Fact]
    public void DescribeBracket_UsesTheExcessWording()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(IncomeTaxScale.Default, 1m, 20_000_000m, 0m, 0m);

        Assert.StartsWith("Hasta ", IncomeTaxEstimateReportPdfDocument.DescribeBracket(estimate.Brackets[0]));
        Assert.StartsWith("Sobre el exceso de ", IncomeTaxEstimateReportPdfDocument.DescribeBracket(estimate.Brackets[1]));
        Assert.DoesNotContain(" hasta ", IncomeTaxEstimateReportPdfDocument.DescribeBracket(estimate.Brackets[3]));
    }

    /// <summary>Utilidad de ₡10.000.000, retenciones de ₡200.000 y un pago de ₡300.000.</summary>
    private static IncomeTaxEstimateReportResult SampleResult()
    {
        IncomeTaxScale scale = IncomeTaxScale.Default;
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(scale, 50_000_000m, 10_000_000m, 200_000m, 300_000m);

        return new IncomeTaxEstimateReportResult(
            new ReportHeader(
                "Estimación del impuesto sobre la renta",
                "Período fiscal del 01/01/2010 al 31/12/2010",
                DateTimeOffset.UtcNow,
                "Usuario de prueba"),
            new DateOnly(2010, 1, 1),
            new DateOnly(2010, 12, 31),
            new IncomeTaxScaleModel(
                scale.Bracket1UpperLimit,
                scale.Bracket2UpperLimit,
                scale.Bracket3UpperLimit,
                scale.Bracket1Rate,
                scale.Bracket2Rate,
                scale.Bracket3Rate,
                scale.Bracket4Rate,
                scale.GrossIncomeThreshold,
                scale.GeneralRate,
                "AAAAAAAAB9E="),
            estimate.GrossIncome,
            40_000_000m,
            estimate.NetIncome,
            estimate.TaxableIncome,
            estimate.Regime,
            estimate.Brackets,
            estimate.EstimatedTax,
            estimate.Withholdings,
            [new IncomeTaxEstimatePaymentLine("PIR-000001", new DateOnly(2010, 6, 30), "REC-HAC-1", 300_000m)],
            estimate.Payments,
            estimate.Balance,
            estimate.AmountDue,
            estimate.AmountInFavor,
            HasData: true);
    }
}
