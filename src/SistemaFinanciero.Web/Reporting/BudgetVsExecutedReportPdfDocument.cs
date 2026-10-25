using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del reporte de presupuesto contra lo ejecutado (regla de negocio 8).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class BudgetVsExecutedReportPdfDocument(BudgetVsExecutedReportResult result) : IDocument
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
                        columns.RelativeColumn(4);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Categoría");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Presupuestado");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Ejecutado");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Disponible");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Variación");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Variación %");
                    });

                    foreach (BudgetModel line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text($"{line.CategoryCode} - {line.CategoryName}");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.BudgetedAmount is decimal budgeted ? ReportDocumentStyles.FormatCrc(budgeted) : "Sin presupuesto");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.ExecutedAmount));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.AvailableAmount is decimal available ? ReportDocumentStyles.FormatCrc(available) : "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.VarianceAmount is decimal variance ? ReportDocumentStyles.FormatCrc(variance) : "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.VariancePercent is decimal percent
                                ? percent.ToString("P1", CultureInfo.InvariantCulture)
                                : "—");
                    }
                });

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().AlignRight()
                        .Text($"Total presupuestado: {ReportDocumentStyles.FormatCrc(result.TotalBudgeted)}")
                        .FontSize(11).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Total ejecutado: {ReportDocumentStyles.FormatCrc(result.TotalExecuted)}")
                        .FontSize(11).SemiBold();
                });
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
