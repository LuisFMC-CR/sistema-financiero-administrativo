using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del comparativo mensual de ingresos y gastos (regla de negocio 8).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class MonthlyComparisonReportPdfDocument(MonthlyComparisonReportResult result) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(9));

            page.Header().Element(header => ReportDocumentStyles.ComposeHeader(header, result.Header));

            page.Content().PaddingTop(10).Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Mes");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Ingresos");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Gastos");
                    });

                    foreach (MonthlyComparisonLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(FormatMonth(line.PeriodYear, line.PeriodMonth));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalIncome));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalExpense));
                    }
                });

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().AlignRight()
                        .Text($"Total ingresos: {ReportDocumentStyles.FormatCrc(result.GrandTotalIncome)}")
                        .FontSize(11).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Total gastos: {ReportDocumentStyles.FormatCrc(result.GrandTotalExpense)}")
                        .FontSize(11).SemiBold();
                });
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private static string FormatMonth(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-CR"));
}
