using System.Globalization;
using ClosedXML.Excel;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Web.Models.Incomes;

namespace SistemaFinanciero.Web.Reporting;

/// <summary>
/// Exporta cada reporte a Excel con ClosedXML (regla de negocio 8): los montos van como números, no
/// como texto, y el encabezado (nombre, parámetros, fecha y hora y usuario) ocupa las primeras filas.
/// </summary>
/// <remarks>Pública para que las pruebas de integración generen el libro sin pasar por HTTP.</remarks>
public static class ReportExcelExporter
{
    public static byte[] BuildIncomeWorkbook(IncomeReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Ingresos");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Código", "Fecha", "Categoría", "Cuenta de efectivo", "Origen", "Referencia", "Monto"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (IncomeReportLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.Code;
            sheet.Cell(row, 2).Value = line.IncomeDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 2).Style.DateFormat.Format = "dd/mm/yyyy";
            sheet.Cell(row, 3).Value = line.CategoryName;
            sheet.Cell(row, 4).Value = line.CashAccountName;
            sheet.Cell(row, 5).Value = line.Origin.ToDisplayName();
            sheet.Cell(row, 6).Value = line.Reference ?? string.Empty;
            sheet.Cell(row, 7).Value = line.Amount;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 6).Value = "Total";
        sheet.Cell(row, 7).Value = result.TotalAmount;
        sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildExpenseByCategoryWorkbook(ExpenseByCategoryReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Gastos por categoría");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Código", "Categoría", "Cantidad", "Neto", "IVA", "Total"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (ExpenseByCategoryReportLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.CategoryCode;
            sheet.Cell(row, 2).Value = line.CategoryName;
            sheet.Cell(row, 3).Value = line.Count;
            sheet.Cell(row, 4).Value = line.NetAmount;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 5).Value = line.TaxAmount;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Value = line.TotalAmount;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 5).Value = "Total";
        sheet.Cell(row, 6).Value = result.GrandTotal;
        sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildBudgetVsExecutedWorkbook(BudgetVsExecutedReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Presupuesto vs ejecutado");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Código", "Categoría", "Presupuestado", "Ejecutado", "Disponible", "Variación", "Variación %"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (BudgetModel line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.CategoryCode;
            sheet.Cell(row, 2).Value = line.CategoryName;

            if (line.BudgetedAmount is decimal budgeted)
            {
                sheet.Cell(row, 3).Value = budgeted;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            }
            else
            {
                sheet.Cell(row, 3).Value = "Sin presupuesto";
            }

            sheet.Cell(row, 4).Value = line.ExecutedAmount;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

            if (line.AvailableAmount is decimal available)
            {
                sheet.Cell(row, 5).Value = available;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            }

            if (line.VarianceAmount is decimal variance)
            {
                sheet.Cell(row, 6).Value = variance;
                sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            }

            if (line.VariancePercent is decimal percent)
            {
                sheet.Cell(row, 7).Value = percent;
                sheet.Cell(row, 7).Style.NumberFormat.Format = "0.0%";
            }

            row++;
        }

        sheet.Cell(row, 2).Value = "Total";
        sheet.Cell(row, 3).Value = result.TotalBudgeted;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 4).Value = result.TotalExecuted;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildMonthlyComparisonWorkbook(MonthlyComparisonReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Comparativo mensual");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Mes", "Ingresos", "Gastos"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        CultureInfo culture = CultureInfo.GetCultureInfo("es-CR");

        foreach (MonthlyComparisonLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = new DateOnly(line.PeriodYear, line.PeriodMonth, 1)
                .ToString("MMMM yyyy", culture);
            sheet.Cell(row, 2).Value = line.TotalIncome;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 3).Value = line.TotalExpense;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 1).Value = "Total";
        sheet.Cell(row, 2).Value = result.GrandTotalIncome;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 3).Value = result.GrandTotalExpense;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildCustomerStatementWorkbook(CustomerStatementReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Estado de cuenta");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Fecha", "Movimiento", "Descripción", "Monto"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (CustomerStatementLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            sheet.Cell(row, 2).Value = line.MovementType;
            sheet.Cell(row, 3).Value = line.IsCancelled ? $"{line.Description} (anulado)" : line.Description;
            sheet.Cell(row, 4).Value = line.Amount;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 3).Value = "Saldo actual";
        sheet.Cell(row, 4).Value = result.CurrentBalance;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildPayableBySupplierWorkbook(PayableBySupplierReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Cuentas por pagar");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Proveedor", "Cantidad", "Saldo total", "Vence más próximo"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (PayableBySupplierReportLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.SupplierName;
            sheet.Cell(row, 2).Value = line.PayableCount;
            sheet.Cell(row, 3).Value = line.TotalBalance;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 4).Value = line.EarliestDueDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 4).Style.DateFormat.Format = "dd/mm/yyyy";
            row++;
        }

        sheet.Cell(row, 2).Value = "Total";
        sheet.Cell(row, 3).Value = result.GrandTotalBalance;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    private static int WriteHeader(IXLWorksheet sheet, ReportHeader header)
    {
        sheet.Cell(1, 1).Value = header.ReportName;
        sheet.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(2, 1).Value = header.ParametersDescription;
        sheet.Cell(3, 1).Value = $"Generado: {header.GeneratedAtUtc.ToLocalTime():dd/MM/yyyy HH:mm}";
        sheet.Cell(4, 1).Value = $"Usuario: {header.GeneratedByUserName}";
        return 6;
    }

    private static void WriteColumnHeaders(IXLWorksheet sheet, int row, string[] columns)
    {
        for (int index = 0; index < columns.Length; index++)
        {
            sheet.Cell(row, index + 1).Value = columns[index];
        }

        sheet.Row(row).Style.Font.SetBold();
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using MemoryStream stream = new();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
