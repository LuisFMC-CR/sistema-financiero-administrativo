using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del IVA del período (RF-84, bloque 7 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class VatPeriodReportPdfDocument(VatPeriodReportResult result) : IDocument
{
    private const string Disclaimer = "Apoyo para la declaración; no sustituye la declaración ante Hacienda.";

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
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Tarifa");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Base ventas");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("IVA cobrado");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Base compras");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("IVA pagado");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Diferencia");
                    });

                    foreach (VatPeriodReportLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text($"{line.TaxCode} - {line.TaxName}");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.SalesBase));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.VatCollected));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.PurchasesBase));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.VatPaid));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.Difference));
                    }
                });

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().AlignRight()
                        .Text($"Total IVA cobrado: {ReportDocumentStyles.FormatCrc(result.TotalVatCollected)}")
                        .FontSize(10).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Total IVA pagado: {ReportDocumentStyles.FormatCrc(result.TotalVatPaid)}")
                        .FontSize(10).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Diferencia: {ReportDocumentStyles.FormatCrc(result.TotalVatCollected - result.TotalVatPaid)}")
                        .FontSize(11).SemiBold();
                });

                if (result.Settlement is { } settlement)
                {
                    string settlementText = settlement.PaymentAmount > 0
                        ? $"Liquidación {settlement.Code} vigente de este mes: pago a Hacienda "
                            + $"{ReportDocumentStyles.FormatCrc(settlement.PaymentAmount)}."
                        : $"Liquidación {settlement.Code} vigente de este mes: saldo a favor "
                            + $"{ReportDocumentStyles.FormatCrc(settlement.CreditCarriedForward)}.";
                    column.Item().PaddingTop(4).Element(innerContainer => ReportDocumentStyles.ComposeNote(innerContainer, settlementText));
                }

                column.Item().Element(innerContainer => ReportDocumentStyles.ComposeNote(innerContainer, Disclaimer));
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
