using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.IntegrationTests.Reports;

/// <summary>
/// Genera cada documento de reporte con datos sintéticos, sin base de datos ni HTTP, para comprobar
/// que QuestPDF (licencia Community) y ClosedXML producen un archivo válido (regla de negocio 8).
/// </summary>
/// <remarks>
/// Fija la licencia de QuestPDF en el constructor estático porque esta clase no arranca
/// <c>Program.cs</c> (que la fija para la aplicación real): sin ella, <c>GeneratePdf</c> lanza una
/// excepción de licencia no configurada.
/// </remarks>
public sealed class ReportExportTests
{
    static ReportExportTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly ReportHeader SampleHeader = new(
        "Ingresos por período",
        "Del 01/01/2010 al 31/01/2010",
        DateTimeOffset.UtcNow,
        "Usuario de prueba");

    [Fact]
    public void IncomeReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        IncomeReportResult result = new(
            SampleHeader,
            new DateOnly(2010, 1, 1),
            new DateOnly(2010, 1, 31),
            [new IncomeReportLine("ING-001", new DateOnly(2010, 1, 10), "Otros ingresos", "Caja chica", IncomeOrigin.Manual, "REC-1", 500m)],
            500m);

        byte[] pdf = new IncomeReportPdfDocument(result).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void ExpenseByCategoryReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        ExpenseByCategoryReportResult result = new(
            SampleHeader with { ReportName = "Gastos por período y categoría" },
            new DateOnly(2010, 1, 1),
            new DateOnly(2010, 1, 31),
            CategoryId: null,
            [new ExpenseByCategoryReportLine("3.1.01", "Alquiler", 2, 1000m, 130m, 1130m)],
            1130m);

        byte[] pdf = new ExpenseByCategoryReportPdfDocument(result).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildIncomeWorkbook_WithLines_GeneratesAValidXlsx()
    {
        IncomeReportResult result = new(
            SampleHeader,
            new DateOnly(2010, 1, 1),
            new DateOnly(2010, 1, 31),
            [new IncomeReportLine("ING-001", new DateOnly(2010, 1, 10), "Otros ingresos", "Caja chica", IncomeOrigin.Manual, "REC-1", 500m)],
            500m);

        byte[] excel = ReportExcelExporter.BuildIncomeWorkbook(result);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void BuildExpenseByCategoryWorkbook_WithLines_GeneratesAValidXlsx()
    {
        ExpenseByCategoryReportResult result = new(
            SampleHeader with { ReportName = "Gastos por período y categoría" },
            new DateOnly(2010, 1, 1),
            new DateOnly(2010, 1, 31),
            CategoryId: null,
            [new ExpenseByCategoryReportLine("3.1.01", "Alquiler", 2, 1000m, 130m, 1130m)],
            1130m);

        byte[] excel = ReportExcelExporter.BuildExpenseByCategoryWorkbook(result);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void BudgetVsExecutedReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        BudgetVsExecutedReportResult result = new(
            SampleHeader with { ReportName = "Presupuesto contra lo ejecutado" },
            2010,
            1,
            [
                new BudgetModel(Guid.NewGuid(), Guid.NewGuid(), "3.1.01", "Alquiler", 2010, 1, 1000m, 600m, 400m, -400m, -0.4m, DateTimeOffset.UtcNow, "v1"),
                new BudgetModel(null, Guid.NewGuid(), "3.1.02", "Electricidad y agua", 2010, 1, null, 0m, null, null, null, null, null),
            ],
            1000m,
            600m);

        byte[] pdf = new BudgetVsExecutedReportPdfDocument(result).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void MonthlyComparisonReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        MonthlyComparisonReportResult result = new(
            SampleHeader with { ReportName = "Comparativo mensual de ingresos y gastos" },
            2010, 1, 2010, 2,
            [
                new MonthlyComparisonLine(2010, 1, 400m, 0m),
                new MonthlyComparisonLine(2010, 2, 0m, 250m),
            ],
            400m,
            250m);

        byte[] pdf = new MonthlyComparisonReportPdfDocument(result).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildBudgetVsExecutedWorkbook_WithLines_GeneratesAValidXlsx()
    {
        BudgetVsExecutedReportResult result = new(
            SampleHeader with { ReportName = "Presupuesto contra lo ejecutado" },
            2010,
            1,
            [new BudgetModel(Guid.NewGuid(), Guid.NewGuid(), "3.1.01", "Alquiler", 2010, 1, 1000m, 600m, 400m, -400m, -0.4m, DateTimeOffset.UtcNow, "v1")],
            1000m,
            600m);

        byte[] excel = ReportExcelExporter.BuildBudgetVsExecutedWorkbook(result);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void BuildMonthlyComparisonWorkbook_WithLines_GeneratesAValidXlsx()
    {
        MonthlyComparisonReportResult result = new(
            SampleHeader with { ReportName = "Comparativo mensual de ingresos y gastos" },
            2010, 1, 2010, 2,
            [
                new MonthlyComparisonLine(2010, 1, 400m, 0m),
                new MonthlyComparisonLine(2010, 2, 0m, 250m),
            ],
            400m,
            250m);

        byte[] excel = ReportExcelExporter.BuildMonthlyComparisonWorkbook(result);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    private static readonly CustomerStatementReportResult SampleCustomerStatement = new(
        SampleHeader with { ReportName = "Estado de cuenta de cliente" },
        Guid.NewGuid(),
        "Cliente de prueba",
        [
            new CustomerStatementLine(new DateOnly(2010, 1, 5), "Factura a crédito", "Factura N° 1", 1000m, false),
            new CustomerStatementLine(new DateOnly(2010, 1, 15), "Abono", "Abono AB-0001", 400m, false),
        ],
        600m);

    private static readonly PayableBySupplierReportResult SamplePayableBySupplier = new(
        SampleHeader with { ReportName = "Cuentas por pagar por proveedor" },
        [new PayableBySupplierReportLine(Guid.NewGuid(), "Proveedor de prueba", 2, 500m, new DateOnly(2010, 1, 10))],
        500m);

    [Fact]
    public void CustomerStatementReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        byte[] pdf = new CustomerStatementReportPdfDocument(SampleCustomerStatement).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void PayableBySupplierReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        byte[] pdf = new PayableBySupplierReportPdfDocument(SamplePayableBySupplier).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildCustomerStatementWorkbook_WithLines_GeneratesAValidXlsx()
    {
        byte[] excel = ReportExcelExporter.BuildCustomerStatementWorkbook(SampleCustomerStatement);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void BuildPayableBySupplierWorkbook_WithLines_GeneratesAValidXlsx()
    {
        byte[] excel = ReportExcelExporter.BuildPayableBySupplierWorkbook(SamplePayableBySupplier);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }
}
