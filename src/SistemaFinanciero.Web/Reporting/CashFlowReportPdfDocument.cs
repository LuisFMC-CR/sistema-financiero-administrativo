using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del flujo de efectivo mensual (RF-86, bloque 7 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class CashFlowReportPdfDocument(CashFlowReportResult result) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(style => style.FontSize(8));

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
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Mes");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Entradas");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Salidas");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Traspasos recibidos");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Traspasos enviados");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Pago de IVA");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Neto");
                    });

                    foreach (CashFlowReportLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(FormatMonth(line.PeriodYear, line.PeriodMonth));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalInflow));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalOutflow));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TransfersReceived));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TransfersSent));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.VatPayments));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Net));
                    }
                });

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().AlignRight()
                        .Text($"Total entradas: {ReportDocumentStyles.FormatCrc(result.GrandTotalInflow)}")
                        .FontSize(10).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Total salidas: {ReportDocumentStyles.FormatCrc(result.GrandTotalOutflow)}")
                        .FontSize(10).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Neto: {ReportDocumentStyles.FormatCrc(result.GrandTotalNet)}")
                        .FontSize(11).SemiBold();
                });
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private static string FormatMonth(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMMM yyyy", CultureInfo.GetCultureInfo("es-CR"));
}
