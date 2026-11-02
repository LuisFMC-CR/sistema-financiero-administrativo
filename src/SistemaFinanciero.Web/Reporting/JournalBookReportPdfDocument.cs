using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Web.Models.Ledger;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del libro diario (RF-80, bloque 6 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class JournalBookReportPdfDocument(JournalBookReportResult result) : IDocument
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
                foreach (JournalBookEntry entry in result.Entries)
                {
                    column.Item().PaddingTop(6).Text(
                        $"Asiento {entry.Number} — {entry.EntryDate:dd/MM/yyyy} — "
                        + $"{entry.SourceType.ToDisplayName()}: {entry.Description}")
                        .FontSize(9).SemiBold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(5);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Cuenta");
                            header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Débito");
                            header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Crédito");
                        });

                        foreach (JournalBookLine line in entry.Lines)
                        {
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                                .Text($"{line.AccountCode} — {line.AccountName}");
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                                .Text(line.Debit > 0 ? ReportDocumentStyles.FormatCrc(line.Debit) : "—");
                            table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                                .Text(line.Credit > 0 ? ReportDocumentStyles.FormatCrc(line.Credit) : "—");
                        }
                    });
                }

                column.Item().PaddingTop(8).AlignRight().Row(row =>
                {
                    row.AutoItem().PaddingRight(20)
                        .Text($"Total débitos: {ReportDocumentStyles.FormatCrc(result.TotalDebit)}")
                        .FontSize(11).SemiBold();
                    row.AutoItem()
                        .Text($"Total créditos: {ReportDocumentStyles.FormatCrc(result.TotalCredit)}")
                        .FontSize(11).SemiBold();
                });

                column.Item().Element(ReportDocumentStyles.ComposeAccountingNote);
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
