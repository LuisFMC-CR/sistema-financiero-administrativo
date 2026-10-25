using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del reporte de gastos por período y categoría (regla de negocio 8).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class ExpenseByCategoryReportPdfDocument(ExpenseByCategoryReportResult result) : IDocument
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
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Código");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Categoría");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Cant.");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Neto");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("IVA");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Total");
                    });

                    foreach (ExpenseByCategoryReportLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.CategoryCode);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.CategoryName);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.NetAmount));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TaxAmount));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalAmount));
                    }
                });

                column.Item().AlignRight().PaddingTop(8)
                    .Text($"Total: {ReportDocumentStyles.FormatCrc(result.GrandTotal)}")
                    .FontSize(11).SemiBold();
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
