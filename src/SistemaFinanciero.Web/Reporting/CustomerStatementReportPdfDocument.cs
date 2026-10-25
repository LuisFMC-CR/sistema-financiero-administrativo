using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del estado de cuenta de un cliente (regla de negocio 8).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class CustomerStatementReportPdfDocument(CustomerStatementReportResult result) : IDocument
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
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Fecha");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Movimiento");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Descripción");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Monto");
                    });

                    foreach (CustomerStatementLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                            .Text(line.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.MovementType);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                            .Text(line.IsCancelled ? $"{line.Description} (anulado)" : line.Description);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Amount));
                    }
                });

                column.Item().PaddingTop(8).AlignRight()
                    .Text($"Saldo actual: {ReportDocumentStyles.FormatCrc(result.CurrentBalance)}")
                    .FontSize(11).SemiBold();
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
