using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>
/// Documento PDF del reporte de cuentas por cobrar por cliente con antigüedad de saldos (RF-98,
/// paquete v7).
/// </summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class ReceivableByCustomerReportPdfDocument(ReceivableByCustomerReportResult result) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
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
                        columns.RelativeColumn(1.2f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.6f);
                        columns.RelativeColumn(1.8f);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Cliente");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Cantidad");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Al día");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("1-30 días");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("31-60 días");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("61-90 días");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Más de 90 días");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Saldo total");
                    });

                    foreach (ReceivableByCustomerReportLine line in result.Lines)
                    {
                        table.Cell().Element(GroupCellStyle).Text(line.CustomerName);
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(line.ReceivableCount.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Aging.Current));
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Aging.Overdue1To30));
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Aging.Overdue31To60));
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Aging.Overdue61To90));
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Aging.OverdueOver90));
                        table.Cell().Element(GroupCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.TotalBalance));

                        table.Cell().ColumnSpan(8).Element(container => ComposeDetails(container, line.Details));
                    }
                });

                column.Item().PaddingTop(8).AlignRight()
                    .Text($"Saldo total: {ReportDocumentStyles.FormatCrc(result.GrandTotalBalance)}")
                    .FontSize(11).SemiBold();
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    /// <summary>Desglose de las cuentas por cobrar individuales que forman el saldo de un cliente.</summary>
    private static void ComposeDetails(IContainer container, IReadOnlyList<ReceivableDetailLine> details)
    {
        container.PaddingLeft(14).PaddingVertical(2).Table(table =>
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
                header.Cell().Element(DetailHeaderCellStyle).Text("Factura");
                header.Cell().Element(DetailHeaderCellStyle).AlignRight().Text("Emisión");
                header.Cell().Element(DetailHeaderCellStyle).AlignRight().Text("Vencimiento");
                header.Cell().Element(DetailHeaderCellStyle).AlignRight().Text("Saldo");
            });

            foreach (ReceivableDetailLine detail in details)
            {
                table.Cell().Element(DetailBodyCellStyle)
                    .Text(detail.InvoiceNumber is long number
                        ? $"Factura N° {number.ToString(CultureInfo.InvariantCulture)}"
                        : "—");
                table.Cell().Element(DetailBodyCellStyle).AlignRight()
                    .Text(detail.IssueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                table.Cell().Element(DetailBodyCellStyle).AlignRight()
                    .Text(detail.DueDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                table.Cell().Element(DetailBodyCellStyle).AlignRight()
                    .Text(ReportDocumentStyles.FormatCrc(detail.Balance));
            }
        });
    }

    private static IContainer GroupCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.SemiBold().FontSize(9))
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(4);

    private static IContainer DetailHeaderCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.SemiBold().FontSize(8).FontColor(Colors.Grey.Darken1))
            .PaddingVertical(2);

    private static IContainer DetailBodyCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.FontSize(8))
            .PaddingVertical(2)
            .BorderBottom(0.5f)
            .BorderColor(Colors.Grey.Lighten3);
}
