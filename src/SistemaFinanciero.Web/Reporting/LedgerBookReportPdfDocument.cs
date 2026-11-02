using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del libro mayor de una cuenta (RF-81, bloque 6 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class LedgerBookReportPdfDocument(LedgerBookReportResult result) : IDocument
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
                column.Item().Text($"Saldo anterior: {ReportDocumentStyles.FormatCrc(result.OpeningBalance)}")
                    .FontSize(10).SemiBold();

                column.Item().PaddingTop(6).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Fecha");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Asiento");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Descripción");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Débito");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Crédito");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Saldo");
                    });

                    if (result.Movements.Count == 0)
                    {
                        table.Cell().ColumnSpan(6).Element(ReportDocumentStyles.BodyCellStyle)
                            .AlignCenter().Text("Esta cuenta no tiene movimientos en el período.");
                    }

                    foreach (LedgerBookMovement movement in result.Movements)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                            .Text(movement.EntryDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle)
                            .Text(movement.JournalEntryNumber.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(movement.Description);
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(movement.Debit > 0 ? ReportDocumentStyles.FormatCrc(movement.Debit) : "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(movement.Credit > 0 ? ReportDocumentStyles.FormatCrc(movement.Credit) : "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(movement.RunningBalance));
                    }
                });

                column.Item().PaddingTop(8).AlignRight()
                    .Text($"Saldo final: {ReportDocumentStyles.FormatCrc(result.ClosingBalance)}")
                    .FontSize(11).SemiBold();

                column.Item().Element(ReportDocumentStyles.ComposeAccountingNote);
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
