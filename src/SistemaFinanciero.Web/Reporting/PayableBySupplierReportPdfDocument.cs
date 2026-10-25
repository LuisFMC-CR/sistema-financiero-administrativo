using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del reporte de cuentas por pagar por proveedor (regla de negocio 8).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class PayableBySupplierReportPdfDocument(PayableBySupplierReportResult result) : IDocument
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
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Proveedor");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Cantidad");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Saldo total");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Vence más próximo");
                    });

                    foreach (PayableBySupplierReportLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.SupplierName);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.PayableCount.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalBalance));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.EarliestDueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                    }
                });

                column.Item().PaddingTop(8).AlignRight()
                    .Text($"Saldo total: {ReportDocumentStyles.FormatCrc(result.GrandTotalBalance)}")
                    .FontSize(11).SemiBold();
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
