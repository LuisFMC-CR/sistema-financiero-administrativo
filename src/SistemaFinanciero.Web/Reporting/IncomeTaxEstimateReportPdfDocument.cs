using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Web.Models.IncomeTax;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>Documento PDF de la estimación del impuesto sobre la renta (RF-99).</summary>
/// <remarks>Público para que las pruebas generen el PDF sin pasar por HTTP.</remarks>
public sealed class IncomeTaxEstimateReportPdfDocument(IncomeTaxEstimateReportResult result) : IDocument
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
                column.Item().Text("Renta del período").FontSize(11).SemiBold();
                column.Item().PaddingTop(4).Element(ComposeIncomeSummary);

                column.Item().PaddingTop(14).Text($"Cálculo del impuesto — {result.Regime.ToDisplayName()}")
                    .FontSize(11).SemiBold();
                column.Item().PaddingTop(4).Element(ComposeBrackets);

                column.Item().PaddingTop(14).Text("Saldo estimado").FontSize(11).SemiBold();
                column.Item().PaddingTop(4).Element(ComposeBalance);

                if (result.Payments.Count > 0)
                {
                    column.Item().PaddingTop(14).Text("Pagos a Hacienda del período").FontSize(11).SemiBold();
                    column.Item().PaddingTop(4).Element(ComposePayments);
                }

                column.Item().Element(container =>
                    ReportDocumentStyles.ComposeNote(container, ReportDocumentStyles.IncomeTaxEstimateNote));
            });

            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private void ComposeIncomeSummary(IContainer container)
    {
        ComposeAmountTable(container,
        [
            ("Renta bruta (total de ingresos)", result.GrossIncome, false),
            ("Gastos", result.TotalExpense, false),
            (result.NetIncome >= 0 ? "Renta neta (utilidad)" : "Pérdida del período", Math.Abs(result.NetIncome), true),
        ]);
    }

    private void ComposeBrackets(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(4);
                columns.RelativeColumn(1);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Tramo");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("%");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Renta en el tramo");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Impuesto");
            });

            foreach (IncomeTaxBracketLine line in result.Brackets)
            {
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(DescribeBracket(line));
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text($"{line.Rate:N2}");
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                    .Text(ReportDocumentStyles.FormatCrc(line.TaxableAmount));
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                    .Text(ReportDocumentStyles.FormatCrc(line.Tax));
            }

            table.Cell().ColumnSpan(3).Element(TotalCellStyle).Text("Impuesto estimado del período");
            table.Cell().Element(TotalCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(result.EstimatedTax));
        });
    }

    private void ComposeBalance(IContainer container)
    {
        ComposeAmountTable(container,
        [
            ("Impuesto estimado", result.EstimatedTax, false),
            ("Menos: retenciones del 2 % del sector público", result.Withholdings, false),
            ("Menos: pagos a Hacienda del período", result.PaymentsTotal, false),
            (result.AmountInFavor > 0 ? "Saldo estimado a favor" : "Saldo estimado por pagar",
                result.AmountInFavor > 0 ? result.AmountInFavor : result.AmountDue, true),
        ]);
    }

    private void ComposePayments(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn(4);
                columns.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Código");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Fecha");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Referencia");
                header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Monto");
            });

            foreach (IncomeTaxEstimatePaymentLine payment in result.Payments)
            {
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(payment.Code);
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(payment.PaymentDate.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture));
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(payment.Reference);
                table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                    .Text(ReportDocumentStyles.FormatCrc(payment.Amount));
            }
        });
    }

    private static void ComposeAmountTable(IContainer container, (string Label, decimal Amount, bool IsTotal)[] rows)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(7);
                columns.RelativeColumn(2);
            });

            foreach ((string label, decimal amount, bool isTotal) in rows)
            {
                Func<IContainer, IContainer> style = isTotal ? TotalCellStyle : ReportDocumentStyles.BodyCellStyle;
                table.Cell().Element(style).Text(label);
                table.Cell().Element(style).AlignRight().Text(ReportDocumentStyles.FormatCrc(amount));
            }
        });
    }

    /// <summary>"Hasta ₡5.621.000,00", "Sobre el exceso de ₡5.621.000,00 hasta ₡8.433.000,00", etc.</summary>
    public static string DescribeBracket(IncomeTaxBracketLine line)
    {
        if (line.From == 0m && line.To is null)
        {
            return "Toda la renta neta";
        }

        if (line.From == 0m)
        {
            return $"Hasta {ReportDocumentStyles.FormatCrc(line.To!.Value)}";
        }

        return line.To is decimal to
            ? $"Sobre el exceso de {ReportDocumentStyles.FormatCrc(line.From)} hasta {ReportDocumentStyles.FormatCrc(to)}"
            : $"Sobre el exceso de {ReportDocumentStyles.FormatCrc(line.From)}";
    }

    private static IContainer TotalCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.SemiBold().FontSize(9))
            .Background(Colors.Grey.Lighten3)
            .PaddingVertical(3);
}
