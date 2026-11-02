using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF de las retenciones aplicadas por clientes (RF-85, bloque 7 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class WithholdingByCustomerReportPdfDocument(WithholdingByCustomerReportResult result) : IDocument
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
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Cliente");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Abono");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Factura");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Fecha");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Tipo de retención");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Base");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Monto");
                    });

                    foreach (WithholdingByCustomerLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.CustomerName);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.ReceivablePaymentCode);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.InvoiceNumber?.ToString(CultureInfo.InvariantCulture) ?? "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                            .Text(line.PaymentDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.WithholdingTypeName);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Base));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Amount));
                    }
                });

                column.Item().PaddingTop(8).AlignRight()
                    .Text($"Total: {ReportDocumentStyles.FormatCrc(result.TotalAmount)}")
                    .FontSize(11).SemiBold();
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
