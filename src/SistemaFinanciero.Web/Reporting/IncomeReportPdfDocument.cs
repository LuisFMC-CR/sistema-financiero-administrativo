using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Web.Models.Incomes;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del reporte de ingresos por período (regla de negocio 8).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class IncomeReportPdfDocument(IncomeReportResult result) : IDocument
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
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Código");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Fecha");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Categoría");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Cuenta de efectivo");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Origen");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Referencia");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Monto");
                    });

                    foreach (IncomeReportLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.Code);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                            .Text(line.IncomeDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.CategoryName);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.CashAccountName);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.Origin.ToDisplayName());
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.Reference ?? "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Amount));
                    }
                });

                column.Item().AlignRight().PaddingTop(8)
                    .Text($"Total: {ReportDocumentStyles.FormatCrc(result.TotalAmount)}")
                    .FontSize(11).SemiBold();
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
