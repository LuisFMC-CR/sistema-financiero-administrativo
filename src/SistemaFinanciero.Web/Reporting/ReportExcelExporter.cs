using System.Globalization;
using ClosedXML.Excel;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Web.Models.Budgets;
using SistemaFinanciero.Web.Models.Incomes;
using SistemaFinanciero.Web.Models.Ledger;

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

        string[] columns = ["Código", "Categoría", "Presupuestado", "Ejecutado", "Disponible", "Variación", "Variación %", "Ejecución"];
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

            sheet.Cell(row, 8).Value = line.ExecutionStatus?.ToDisplayName() ?? string.Empty;

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

    public static byte[] BuildJournalBookWorkbook(JournalBookReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Libro diario");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Asiento", "Fecha", "Tipo", "Descripción", "Cuenta", "Débito", "Crédito"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (JournalBookEntry entry in result.Entries)
        {
            foreach (JournalBookLine line in entry.Lines)
            {
                sheet.Cell(row, 1).Value = entry.Number;
                sheet.Cell(row, 2).Value = entry.EntryDate.ToDateTime(TimeOnly.MinValue);
                sheet.Cell(row, 2).Style.DateFormat.Format = "dd/mm/yyyy";
                sheet.Cell(row, 3).Value = entry.SourceType.ToDisplayName();
                sheet.Cell(row, 4).Value = entry.Description;
                sheet.Cell(row, 5).Value = $"{line.AccountCode} — {line.AccountName}";
                sheet.Cell(row, 6).Value = line.Debit;
                sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 7).Value = line.Credit;
                sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }
        }

        sheet.Cell(row, 5).Value = "Total";
        sheet.Cell(row, 6).Value = result.TotalDebit;
        sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 7).Value = result.TotalCredit;
        sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();
        row++;

        WriteAccountingNote(sheet, row);
        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildLedgerBookWorkbook(LedgerBookReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Libro mayor");

        int row = WriteHeader(sheet, result.Header);

        sheet.Cell(row, 1).Value = "Saldo anterior";
        sheet.Cell(row, 2).Value = result.OpeningBalance;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        row += 2;

        string[] columns = ["Fecha", "Asiento", "Descripción", "Débito", "Crédito", "Saldo"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (LedgerBookMovement movement in result.Movements)
        {
            sheet.Cell(row, 1).Value = movement.EntryDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 1).Style.DateFormat.Format = "dd/mm/yyyy";
            sheet.Cell(row, 2).Value = movement.JournalEntryNumber;
            sheet.Cell(row, 3).Value = movement.Description;
            sheet.Cell(row, 4).Value = movement.Debit;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 5).Value = movement.Credit;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Value = movement.RunningBalance;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 5).Value = "Saldo final";
        sheet.Cell(row, 6).Value = result.ClosingBalance;
        sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();
        row++;

        WriteAccountingNote(sheet, row);
        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildTrialBalanceWorkbook(TrialBalanceReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Balanza de comprobación");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Código", "Cuenta", "Débitos", "Créditos", "Saldo deudor", "Saldo acreedor"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (TrialBalanceGroupLine group in result.Groups)
        {
            foreach (TrialBalanceAccountLine account in group.Accounts)
            {
                sheet.Cell(row, 1).Value = account.AccountCode;
                sheet.Cell(row, 2).Value = account.AccountName;
                sheet.Cell(row, 3).Value = account.TotalDebit;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 4).Value = account.TotalCredit;
                sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 5).Value = account.DebitBalance;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 6).Value = account.CreditBalance;
                sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            sheet.Cell(row, 1).Value = $"Subtotal {group.GroupCode}";
            sheet.Cell(row, 2).Value = group.GroupName;
            sheet.Cell(row, 3).Value = group.TotalDebit;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 4).Value = group.TotalCredit;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 5).Value = group.DebitBalance;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Value = group.CreditBalance;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Row(row).Style.Font.SetBold();
            row++;
        }

        sheet.Cell(row, 2).Value = "Total";
        sheet.Cell(row, 3).Value = result.GrandTotalDebit;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 4).Value = result.GrandTotalCredit;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 5).Value = result.GrandTotalDebitBalance;
        sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 6).Value = result.GrandTotalCreditBalance;
        sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();
        row++;

        WriteAccountingNote(sheet, row);
        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildIncomeStatementWorkbook(IncomeStatementReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Estado de resultados");

        int row = WriteHeader(sheet, result.Header);

        sheet.Cell(row, 1).Value = "Ingresos";
        sheet.Row(row).Style.Font.SetBold();
        row++;
        row = WriteIncomeStatementSide(sheet, row, result.IncomeGroups, "Total ingresos", result.TotalIncome);

        row++;
        sheet.Cell(row, 1).Value = "Gastos";
        sheet.Row(row).Style.Font.SetBold();
        row++;
        row = WriteIncomeStatementSide(sheet, row, result.ExpenseGroups, "Total gastos", result.TotalExpense);

        row++;
        sheet.Cell(row, 1).Value = result.NetResult >= 0 ? "Utilidad del período" : "Pérdida del período";
        sheet.Cell(row, 2).Value = Math.Abs(result.NetResult);
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();
        row += 2;

        WriteAccountingNote(sheet, row);
        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    private static int WriteIncomeStatementSide(
        IXLWorksheet sheet,
        int row,
        IReadOnlyList<IncomeStatementGroupLine> groups,
        string totalLabel,
        decimal total)
    {
        string[] columns = ["Código", "Cuenta", "Monto"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (IncomeStatementGroupLine group in groups)
        {
            foreach (IncomeStatementAccountLine account in group.Accounts)
            {
                sheet.Cell(row, 1).Value = account.AccountCode;
                sheet.Cell(row, 2).Value = account.AccountName;
                sheet.Cell(row, 3).Value = account.Amount;
                sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
                row++;
            }

            sheet.Cell(row, 1).Value = $"Subtotal {group.GroupCode}";
            sheet.Cell(row, 2).Value = group.GroupName;
            sheet.Cell(row, 3).Value = group.Subtotal;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Row(row).Style.Font.SetBold();
            row++;
        }

        sheet.Cell(row, 2).Value = totalLabel;
        sheet.Cell(row, 3).Value = total;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();
        return row + 1;
    }

    public static byte[] BuildAnnualBudgetWorkbook(AnnualBudgetReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Presupuesto anual");

        int row = WriteHeader(sheet, result.Header);

        string[] columns =
        [
            "Código", "Categoría", "Presupuestado acum.", "Ejecutado acum.", "Presupuesto anual",
            "Disponible", "Variación", "Variación %", "Ejecución",
        ];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (AnnualBudgetReportLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.CategoryCode;
            sheet.Cell(row, 2).Value = line.CategoryName;
            sheet.Cell(row, 3).Value = line.AccumulatedBudgeted;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 4).Value = line.AccumulatedExecuted;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 5).Value = line.AnnualBudgeted;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Value = line.AvailableAmount;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 7).Value = line.VarianceAmount;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";

            if (line.VariancePercent is decimal percent)
            {
                sheet.Cell(row, 8).Value = percent;
                sheet.Cell(row, 8).Style.NumberFormat.Format = "0.0%";
            }

            sheet.Cell(row, 9).Value = line.ExecutionStatus.ToDisplayName();

            row++;
        }

        sheet.Cell(row, 2).Value = "Total";
        sheet.Cell(row, 3).Value = result.TotalAccumulatedBudgeted;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 4).Value = result.TotalAccumulatedExecuted;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 5).Value = result.TotalAnnualBudgeted;
        sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildCashFlowWorkbook(CashFlowReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Flujo de efectivo");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Mes", "Entradas", "Salidas", "Neto"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        CultureInfo culture = CultureInfo.GetCultureInfo("es-CR");

        foreach (CashFlowReportLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = new DateOnly(line.PeriodYear, line.PeriodMonth, 1).ToString("MMMM yyyy", culture);
            sheet.Cell(row, 2).Value = line.TotalInflow;
            sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 3).Value = line.TotalOutflow;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 4).Value = line.Net;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 1).Value = "Total";
        sheet.Cell(row, 2).Value = result.GrandTotalInflow;
        sheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 3).Value = result.GrandTotalOutflow;
        sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 4).Value = result.GrandTotalNet;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();

        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildVatPeriodWorkbook(VatPeriodReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("IVA del período");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Código", "Tarifa", "Base ventas", "IVA cobrado", "Base compras", "IVA pagado", "Diferencia"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (VatPeriodReportLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.TaxCode;
            sheet.Cell(row, 2).Value = line.TaxName;
            sheet.Cell(row, 3).Value = line.SalesBase;
            sheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 4).Value = line.VatCollected;
            sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 5).Value = line.PurchasesBase;
            sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 6).Value = line.VatPaid;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
            sheet.Cell(row, 7).Value = line.Difference;
            sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Cell(row, 3).Value = "Total";
        sheet.Cell(row, 4).Value = result.TotalVatCollected;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 6).Value = result.TotalVatPaid;
        sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
        sheet.Cell(row, 7).Value = result.TotalVatCollected - result.TotalVatPaid;
        sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
        sheet.Row(row).Style.Font.SetBold();
        row++;

        WriteNote(sheet, row, "Apoyo para la declaración; no sustituye la declaración ante Hacienda.");
        sheet.Columns().AdjustToContents();
        return ToBytes(workbook);
    }

    public static byte[] BuildWithholdingByCustomerWorkbook(WithholdingByCustomerReportResult result)
    {
        using XLWorkbook workbook = new();
        IXLWorksheet sheet = workbook.Worksheets.Add("Retenciones por cliente");

        int row = WriteHeader(sheet, result.Header);

        string[] columns = ["Cliente", "Abono", "Factura", "Fecha", "Tipo de retención", "Base", "Monto"];
        WriteColumnHeaders(sheet, row, columns);
        row++;

        foreach (WithholdingByCustomerLine line in result.Lines)
        {
            sheet.Cell(row, 1).Value = line.CustomerName;
            sheet.Cell(row, 2).Value = line.ReceivablePaymentCode;
            sheet.Cell(row, 3).Value = line.InvoiceNumber?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            sheet.Cell(row, 4).Value = line.PaymentDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 4).Style.DateFormat.Format = "dd/mm/yyyy";
            sheet.Cell(row, 5).Value = line.WithholdingTypeName;
            sheet.Cell(row, 6).Value = line.Base;
            sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
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

    private static void WriteAccountingNote(IXLWorksheet sheet, int row) => WriteNote(sheet, row, ReportDocumentStyles.AccountingNote);

    private static void WriteNote(IXLWorksheet sheet, int row, string text)
    {
        sheet.Cell(row, 1).Value = text;
        sheet.Cell(row, 1).Style.Font.SetItalic();
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
