using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Application.Parameters;

namespace SistemaFinanciero.Web.Reporting;

public sealed class CreditNotePdfDocument(CreditNoteModel note, SystemParametersModel? companyData) : IDocument
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
            page.Header().Column(column =>
            {
                column.Item().Text(companyData?.CompanyName ?? "—").FontSize(13).SemiBold();
                column.Item().Text($"Cédula jurídica: {companyData?.CompanyLegalId ?? "—"}");
                column.Item().PaddingTop(8).Text($"NOTA DE CRÉDITO {note.Code}").FontSize(14).SemiBold();
                column.Item().Text($"Factura: {note.InvoiceNumber}  |  Fecha: {note.IssueDate:dd/MM/yyyy}");
                column.Item().Text($"Cliente: {note.CustomerName}");
                column.Item().Text($"Motivo: {note.Reason}");
                column.Item().PaddingTop(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            });
            page.Content().PaddingTop(10).Column(column =>
            {
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(28);
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("#");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Neto");
                        header.Cell().Element(HeaderCell).AlignRight().Text("IVA (%)");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Monto IVA");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });
                    foreach (CreditNoteLineModel line in note.Lines)
                    {
                        table.Cell().Element(BodyCell).Text(line.Position.ToString(CultureInfo.InvariantCulture));
                        table.Cell().Element(BodyCell).Text(line.Description);
                        table.Cell().Element(BodyCell).AlignRight().Text(line.Quantity.ToString("N4", CultureInfo.GetCultureInfo("es-CR")));
                        table.Cell().Element(BodyCell).AlignRight().Text(Colones(line.NetAmount));
                        table.Cell().Element(BodyCell).AlignRight().Text($"{line.TaxRate.ToString("0.##", CultureInfo.GetCultureInfo("es-CR"))}%");
                        table.Cell().Element(BodyCell).AlignRight().Text(Colones(line.TaxAmount));
                        table.Cell().Element(BodyCell).AlignRight().Text(Colones(line.TotalAmount));
                    }
                });

                column.Item().PaddingTop(12).AlignRight().Column(totals =>
                {
                    totals.Item().Text($"Neto: {Colones(note.NetAmount)}");
                    totals.Item().Text($"IVA: {Colones(note.TaxAmount)}");
                    totals.Item().Text($"Total acreditado: {Colones(note.TotalAmount)}").SemiBold();
                    totals.Item().Text($"Aplicado a saldo: {Colones(note.AppliedToBalance)}");
                    totals.Item().Text($"Retenciones revertidas: {Colones(note.WithholdingReversedAmount)}");
                    totals.Item().Text($"Devuelto en efectivo: {Colones(note.RefundedAmount)}");
                    if (note.RefundedAmount > 0m)
                    {
                        totals.Item().Text($"Referencia de devolución: {note.RefundReference}");
                        totals.Item().Text($"Cuenta de efectivo: {note.CashAccountName}");
                    }
                });
            });
            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(Colors.Grey.Lighten3).Padding(5).DefaultTextStyle(style => style.SemiBold());

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5);

    private static string Colones(decimal amount) =>
        $"₡{amount.ToString("N2", CultureInfo.GetCultureInfo("es-CR"))}";
}
