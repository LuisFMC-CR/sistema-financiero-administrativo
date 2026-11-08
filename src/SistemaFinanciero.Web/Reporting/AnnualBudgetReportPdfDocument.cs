using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Web.Models.Budgets;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF del presupuesto anual a la fecha (RF-87, bloque 7 del paquete de mejoras finales).</summary>
/// <remarks>Público para que las pruebas de integración generen el PDF sin pasar por HTTP.</remarks>
public sealed class AnnualBudgetReportPdfDocument(AnnualBudgetReportResult result) : IDocument
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
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Categoría");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Presupuestado acum.");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Ejecutado acum.");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Presupuesto anual");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Disponible");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Variación");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Variación %");
                        header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Ejecución");
                    });

                    foreach (AnnualBudgetReportLine line in result.Lines)
                    {
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text($"{line.AccountCode} - {line.AccountName}");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.AccumulatedBudgeted));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.AccumulatedExecuted));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.AnnualBudgeted));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.AvailableAmount));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(ReportDocumentStyles.FormatCrc(line.VarianceAmount));
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                            .Text(line.VariancePercent is decimal percent
                                ? percent.ToString("P1", CultureInfo.InvariantCulture)
                                : "—");
                        table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.ExecutionStatus.ToDisplayName());
                    }
                });

                column.Item().PaddingTop(8).Row(row =>
                {
                    row.RelativeItem().AlignRight()
                        .Text($"Total presupuestado acum.: {ReportDocumentStyles.FormatCrc(result.TotalAccumulatedBudgeted)}")
                        .FontSize(10).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Total ejecutado acum.: {ReportDocumentStyles.FormatCrc(result.TotalAccumulatedExecuted)}")
                        .FontSize(10).SemiBold();
                    row.ConstantItem(20);
                    row.RelativeItem().AlignRight()
                        .Text($"Presupuesto anual: {ReportDocumentStyles.FormatCrc(result.TotalAnnualBudgeted)}")
                        .FontSize(10).SemiBold();
                });
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }
}
