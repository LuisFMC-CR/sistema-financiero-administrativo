using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>
/// Composición común a todo reporte en PDF: el encabezado con nombre, parámetros, fecha y hora y
/// usuario (regla de negocio 8), y el estilo de las tablas. Cada documento de
/// <c>Web/Reporting</c> lo usa en vez de repetir el mismo bloque.
/// </summary>
internal static class ReportDocumentStyles
{
    /// <summary>
    /// Pie de los reportes contables (bloque 5/6 del paquete de mejoras finales, punto 5): los saldos
    /// salen de los asientos registrados, nunca de un saldo inicial que el sistema no administra.
    /// </summary>
    public const string AccountingNote =
        "Saldos calculados a partir de las operaciones registradas en el sistema; no incluye saldos iniciales.";

    /// <summary>Pie del reporte "Estimación del impuesto sobre la renta" (RF-99).</summary>
    public const string IncomeTaxEstimateNote =
        "Montos estimados a partir de las operaciones registradas en el sistema y de la escala del impuesto "
        + "sobre la renta vigente en Parámetros.";

    public static void ComposeAccountingNote(IContainer container) => ComposeNote(container, AccountingNote);

    /// <summary>Pie de un reporte con una nota propia (por ejemplo, el aviso de apoyo para la declaración de IVA).</summary>
    public static void ComposeNote(IContainer container, string text)
    {
        container.PaddingTop(10).Column(column =>
        {
            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
            column.Item().PaddingTop(4).Text(text).FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
        });
    }

    public static void ComposeHeader(IContainer container, ReportHeader header)
    {
        container.Column(column =>
        {
            column.Item().Text(header.ReportName).FontSize(16).SemiBold();
            column.Item().Text(header.ParametersDescription).FontSize(10);
            column.Item().PaddingTop(2).Row(row =>
            {
                string generatedAt = header.GeneratedAtUtc.ToLocalTime()
                    .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                row.RelativeItem().Text($"Generado: {generatedAt}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignRight()
                    .Text($"Usuario: {header.GeneratedByUserName}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
            column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    public static void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(text =>
        {
            text.CurrentPageNumber();
            text.Span(" / ");
            text.TotalPages();
        });
    }

    public static IContainer HeaderCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.SemiBold().FontSize(9))
            .PaddingVertical(4)
            .BorderBottom(1)
            .BorderColor(Colors.Black);

    public static IContainer BodyCellStyle(IContainer container) =>
        container
            .DefaultTextStyle(style => style.FontSize(9))
            .PaddingVertical(3)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2);

    public static string FormatCrc(decimal amount) =>
        $"₡{amount.ToString("#,##0.00", CultureInfo.InvariantCulture)}";
}
