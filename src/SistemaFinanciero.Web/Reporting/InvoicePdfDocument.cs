using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Models.Invoices;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>
/// Documento PDF de la factura interna (regla de negocio 15, RF-37): no sustituye el comprobante
/// electrónico oficial, es un documento administrativo interno.
/// </summary>
/// <remarks>
/// Público para que las pruebas de integración generen el PDF sin pasar por HTTP.
/// <paramref name="companyData"/> puede ser nulo si Gerencia todavía no configuró los datos de la
/// empresa (RF-55); en ese caso el encabezado muestra "—" en su lugar.
/// </remarks>
public sealed class InvoicePdfDocument(InvoiceModel invoice, SystemParametersModel? companyData) : IDocument
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

            page.Header().Element(ComposeHeader);
            page.Content().PaddingTop(10).Element(ComposeContent);
            page.Footer().Element(ReportDocumentStyles.ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(companyColumn =>
                {
                    companyColumn.Item().Text(companyData?.CompanyName ?? "—").FontSize(13).SemiBold();
                    companyColumn.Item().Text($"Cédula jurídica: {companyData?.CompanyLegalId ?? "—"}").FontSize(9);
                    if (!string.IsNullOrWhiteSpace(companyData?.CompanyAddress))
                    {
                        companyColumn.Item().Text(companyData.CompanyAddress).FontSize(9);
                    }
                    if (!string.IsNullOrWhiteSpace(companyData?.CompanyPhone) || !string.IsNullOrWhiteSpace(companyData?.CompanyEmail))
                    {
                        companyColumn.Item().Text(
                            $"{companyData?.CompanyPhone ?? "—"} · {companyData?.CompanyEmail ?? "—"}").FontSize(9);
                    }
                });
                row.RelativeItem().AlignRight().Column(invoiceColumn =>
                {
                    invoiceColumn.Item().AlignRight().Text("Factura interna").FontSize(13).SemiBold();
                    invoiceColumn.Item().AlignRight().Text(
                        $"N° {invoice.Number?.ToString(CultureInfo.InvariantCulture) ?? "—"}").FontSize(11);
                    invoiceColumn.Item().AlignRight().Text(
                        $"Fecha: {invoice.IssueDate:dd/MM/yyyy}").FontSize(9);
                    invoiceColumn.Item().AlignRight().Text(
                        $"Plazo: {invoice.PaymentTerm.ToDisplayName()}").FontSize(9);
                    invoiceColumn.Item().AlignRight().Text(
                        $"Vencimiento: {(invoice.DueDate is DateOnly due ? due.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "—")}").FontSize(9);
                    invoiceColumn.Item().AlignRight().Text(
                        $"Estado: {invoice.Status.ToDisplayName()}").FontSize(9).SemiBold();
                });
            });

            if (invoice.Status == InvoiceStatus.Cancelled)
            {
                column.Item().PaddingTop(6).Background(Colors.Red.Lighten4).Padding(6).Column(cancelColumn =>
                {
                    cancelColumn.Item().Text("ANULADA").FontColor(Colors.Red.Darken2).FontSize(12).SemiBold();
                    cancelColumn.Item().Text($"Motivo: {invoice.CancellationReason ?? "—"}").FontSize(9);
                });
            }

            column.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(column =>
        {
            column.Item().PaddingBottom(6).Text($"Cliente: {invoice.CustomerName}").FontSize(10).SemiBold();

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Código");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Descripción");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Unidad");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Cantidad");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Precio");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Subtotal");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Descuento");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Neto");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Tarifa");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("IVA");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Total");
                });

                foreach (InvoiceLineModel line in invoice.Lines.OrderBy(line => line.Position))
                {
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.Position.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.Description);
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(line.UnitOfMeasure);
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(line.Quantity.ToString("0.####", CultureInfo.InvariantCulture));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(line.UnitPrice));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(line.GrossAmount));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(line.DiscountAmount));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(line.NetAmount));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(line.TaxRate.ToString("0.##", CultureInfo.InvariantCulture) + "%");
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(line.TaxAmount));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight().Text(ReportDocumentStyles.FormatCrc(line.TotalAmount));
                }
            });

            column.Item().PaddingTop(10).Text("Resumen de IVA por tarifa").FontSize(10).SemiBold();
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).Text("Tarifa");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Base");
                    header.Cell().Element(ReportDocumentStyles.HeaderCellStyle).AlignRight().Text("Monto de IVA");
                });

                foreach (var group in invoice.Lines
                    .GroupBy(line => new { line.TaxName, line.TaxRate })
                    .OrderBy(group => group.Key.TaxRate))
                {
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).Text(group.Key.TaxName);
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                        .Text(ReportDocumentStyles.FormatCrc(group.Sum(line => line.TaxableAmount)));
                    table.Cell().Element(ReportDocumentStyles.BodyCellStyle).AlignRight()
                        .Text(ReportDocumentStyles.FormatCrc(group.Sum(line => line.TaxAmount)));
                }
            });

            column.Item().PaddingTop(8).AlignRight().Column(totalsColumn =>
            {
                totalsColumn.Item().Text($"Neto: {ReportDocumentStyles.FormatCrc(invoice.NetAmount)}").FontSize(10);
                totalsColumn.Item().Text($"IVA: {ReportDocumentStyles.FormatCrc(invoice.TaxAmount)}").FontSize(10);
                totalsColumn.Item().Text($"Total: {ReportDocumentStyles.FormatCrc(invoice.TotalAmount)}").FontSize(12).SemiBold();
            });

            if (invoice.PaymentTerm == PaymentTerm.Cash && invoice.PaymentMethod is PaymentMethod paymentMethod)
            {
                column.Item().PaddingTop(10).Text($"Medio de pago: {paymentMethod.ToDisplayName()}").FontSize(9);
            }

            column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(4).Text("Documento administrativo interno. No sustituye el comprobante electrónico oficial.")
                .FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        });
    }
}
