using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF de la balanza de comprobación (RF-82, bloque 6 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class TrialBalanceReportPdfDocument(TrialBalanceReportResult result) : IDocument
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
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Código");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Cuenta");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Débitos");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Créditos");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Saldo deudor");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Saldo acreedor");
                    });

                    foreach (TrialBalanceGroupLine group in result.Groups)
                    {
                        foreach (TrialBalanceAccountLine account in group.Accounts)
                        {
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(account.AccountCode);
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(account.AccountName);
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                                .Text(ReportDocumentStyles.FormatCrc(account.TotalDebit));
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                                .Text(ReportDocumentStyles.FormatCrc(account.TotalCredit));
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                                .Text(account.DebitBalance > 0 ? ReportDocumentStyles.FormatCrc(account.DebitBalance) : "—");
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                                .Text(account.CreditBalance > 0 ? ReportDocumentStyles.FormatCrc(account.CreditBalance) : "—");
                        }

                        table.Cell().Element(SubtotalCellStyle).Text($"Subtotal {group.GroupCode} — {group.GroupName}");
                        table.Cell().Element(SubtotalCellStyle).Text(string.Empty);
                        table.Cell().Element(SubtotalCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(group.TotalDebit));
                        table.Cell().Element(SubtotalCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(group.TotalCredit));
                        table.Cell().Element(SubtotalCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(group.DebitBalance));
                        table.Cell().Element(SubtotalCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(group.CreditBalance));
                    }
                });

                column.Item().PaddingTop(8).AlignRight().Column(totalsColumn =>
                {
                    totalsColumn.Item().Text(
                        $"Total débitos: {ReportDocumentStyles.FormatCrc(result.GrandTotalDebit)}  "
                        + $"Total créditos: {ReportDocumentStyles.FormatCrc(result.GrandTotalCredit)}")
                        .FontSize(10).SemiBold();
                    totalsColumn.Item().Text(
                        $"Saldo deudor: {ReportDocumentStyles.FormatCrc(result.GrandTotalDebitBalance)}  "
                        + $"Saldo acreedor: {ReportDocumentStyles.FormatCrc(result.GrandTotalCreditBalance)}")
                        .FontSize(10).SemiBold();
                });

                column.Item().Element(ReportDocumentStyles.ComposeAccountingNote);
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private static QuestPDF.Infrastructure.IContainer SubtotalCellStyle(QuestPDF.Infrastructure.IContainer container) =>
        container
            .DefaultTextStyle(style => style.SemiBold().FontSize(9))
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(3);
}
