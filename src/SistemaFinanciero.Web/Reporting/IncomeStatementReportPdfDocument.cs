using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del estado de resultados (RF-83, bloque 6 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class IncomeStatementReportPdfDocument(IncomeStatementReportResult result) : IDocument
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
                column.Item().Text("Ingresos").FontSize(11).SemiBold();
                column.Item().PaddingTop(4).Element(container => ComposeSection(container, result.IncomeGroups));
                column.Item().PaddingTop(6).AlignRight()
                    .Text($"Total ingresos: {ReportDocumentStyles.FormatCrc(result.TotalIncome)}").FontSize(10).SemiBold();

                column.Item().PaddingTop(14).Text("Gastos").FontSize(11).SemiBold();
                column.Item().PaddingTop(4).Element(container => ComposeSection(container, result.ExpenseGroups));
                column.Item().PaddingTop(6).AlignRight()
                    .Text($"Total gastos: {ReportDocumentStyles.FormatCrc(result.TotalExpense)}").FontSize(10).SemiBold();

                column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                column.Item().PaddingTop(6).AlignRight().Text(
                    $"{(result.NetResult >= 0 ? "Utilidad" : "Pérdida")} del período: "
                    + $"{ReportDocumentStyles.FormatCrc(Math.Abs(result.NetResult))}")
                    .FontSize(12).SemiBold();

                column.Item().Element(ReportDocumentStyles.ComposeAccountingNote);
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private static void ComposeSection(IContainer container, IReadOnlyList<IncomeStatementGroupLine> groups)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(5);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Código");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Cuenta");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Monto");
            });

            if (groups.Count == 0)
            {
                table.Cell().ColumnSpan(3).Element(ReportDocumentStyles.BodyCellStyle)
                    .AlignCenter().Text("Sin movimientos en el período.");
            }

            foreach (IncomeStatementGroupLine group in groups)
            {
                foreach (IncomeStatementAccountLine account in group.Accounts)
                {
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(account.AccountCode);
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(account.AccountName);
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                        .Text(ReportDocumentStyles.FormatCrc(account.Amount));
                }

                table.Cell().Element(SubtotalCellStyle).Text($"Subtotal {group.GroupCode} — {group.GroupName}");
                table.Cell().Element(SubtotalCellStyle).Text(string.Empty);
                table.Cell().Element(SubtotalCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(group.Subtotal));
            }
        });
    }

    private static IContainer SubtotalCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.SemiBold().FontSize(9))
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(3);
}
