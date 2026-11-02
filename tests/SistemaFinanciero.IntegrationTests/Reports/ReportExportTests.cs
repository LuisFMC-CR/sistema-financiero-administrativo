using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Ledger;
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

    private static readonly JournalBookReportResult SampleJournalBook = new(
        SampleHeader with { ReportName = "Libro diario" },
        new DateOnly(2010, 1, 1),
        new DateOnly(2010, 1, 31),
        [
            new JournalBookEntry(
                1,
                new DateOnly(2010, 1, 10),
                "Factura 1",
                JournalEntrySourceType.Invoice,
                [
                    new JournalBookLine("1.1.01", "Caja chica", 113m, 0m),
                    new JournalBookLine("4.1.01", "Soporte técnico", 0m, 100m),
                    new JournalBookLine("2.2.01", "IVA por pagar", 0m, 13m),
                ]),
        ],
        113m,
        113m);

    private static readonly LedgerBookReportResult SampleLedgerBook = new(
        SampleHeader with { ReportName = "Libro mayor" },
        Guid.NewGuid(),
        "1.1.01",
        "Caja chica",
        new DateOnly(2010, 1, 1),
        new DateOnly(2010, 1, 31),
        200m,
        [new LedgerBookMovement(new DateOnly(2010, 1, 10), 1, "Factura 1", 113m, 0m, 313m)],
        313m);

    private static readonly TrialBalanceReportResult SampleTrialBalance = new(
        SampleHeader with { ReportName = "Balanza de comprobación" },
        new DateOnly(2010, 1, 31),
        [
            new TrialBalanceGroupLine(
                "1.1",
                "Efectivo",
                [new TrialBalanceAccountLine("1.1.01", "Caja chica", 313m, 0m, 313m, 0m)],
                313m,
                0m,
                313m,
                0m),
        ],
        313m,
        313m,
        313m,
        313m);

    private static readonly IncomeStatementReportResult SampleIncomeStatement = new(
        SampleHeader with { ReportName = "Estado de resultados" },
        new DateOnly(2010, 1, 1),
        new DateOnly(2010, 1, 31),
        [
            new IncomeStatementGroupLine(
                "4.1",
                "Ingresos por servicios",
                [new IncomeStatementAccountLine("4.1.01", "Soporte técnico", 100m)],
                100m),
        ],
        100m,
        [
            new IncomeStatementGroupLine(
                "5.1",
                "Gastos de operación",
                [new IncomeStatementAccountLine("5.1.01", "Alquiler", 40m)],
                40m),
        ],
        40m,
        60m);

    [Fact]
    public void JournalBookReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        byte[] pdf = new JournalBookReportPdfDocument(SampleJournalBook).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildJournalBookWorkbook_WithLines_GeneratesAValidXlsx()
    {
        byte[] excel = ReportExcelExporter.BuildJournalBookWorkbook(SampleJournalBook);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void LedgerBookReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        byte[] pdf = new LedgerBookReportPdfDocument(SampleLedgerBook).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildLedgerBookWorkbook_WithLines_GeneratesAValidXlsx()
    {
        byte[] excel = ReportExcelExporter.BuildLedgerBookWorkbook(SampleLedgerBook);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void TrialBalanceReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        byte[] pdf = new TrialBalanceReportPdfDocument(SampleTrialBalance).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildTrialBalanceWorkbook_WithLines_GeneratesAValidXlsx()
    {
        byte[] excel = ReportExcelExporter.BuildTrialBalanceWorkbook(SampleTrialBalance);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }

    [Fact]
    public void IncomeStatementReportPdfDocument_WithLines_GeneratesAValidPdf()
    {
        byte[] pdf = new IncomeStatementReportPdfDocument(SampleIncomeStatement).GeneratePdf();

        Assert.True(pdf.Length > 0);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf, 0, 4));
    }

    [Fact]
    public void BuildIncomeStatementWorkbook_WithLines_GeneratesAValidXlsx()
    {
        byte[] excel = ReportExcelExporter.BuildIncomeStatementWorkbook(SampleIncomeStatement);

        Assert.True(excel.Length > 0);
        Assert.Equal((byte)'P', excel[0]);
        Assert.Equal((byte)'K', excel[1]);
    }
}
