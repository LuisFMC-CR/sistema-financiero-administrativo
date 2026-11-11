using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Reports;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Reports;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta en pantalla y exporta a PDF y Excel los reportes financieros (regla de negocio 8).
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IReportService</c> (Application/Infrastructure) y, para los selectores de
/// cuenta contable y de cliente, <c>ILedgerAccountService</c> e <c>ICustomerCatalogService</c>. Misma
/// política que Presupuesto y Consultas: Asistente no entra. Cuando el reporte no tiene datos para
/// los parámetros indicados, no se genera ningún archivo: se vuelve al formulario con un mensaje
/// (regla 8).
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("reportes")]
public sealed class ReportsController(
    IReportService reportService,
    ICustomerCatalogService customerService,
    ILedgerAccountService ledgerAccountService,
    ISystemParametersService systemParametersService,
    TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("ingresos")]
    public async Task<IActionResult> Income(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        IncomeReportResult? result = null;

        if (periodFrom is not null && periodTo is not null)
        {
            result = await reportService.GetIncomeReportAsync(from, to, CurrentUserName(), cancellationToken);
        }

        return View(new IncomeReportViewModel(from, to, result));
    }

    [HttpGet("ingresos/pdf")]
    public async Task<IActionResult> IncomePdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        IncomeReportResult result = await reportService.GetIncomeReportAsync(
            periodFrom,
            periodTo,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(Income), periodFrom, periodTo);
        }

        byte[] pdf = new IncomeReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "ingresos-por-periodo.pdf");
    }

    [HttpGet("ingresos/excel")]
    public async Task<IActionResult> IncomeExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        IncomeReportResult result = await reportService.GetIncomeReportAsync(
            periodFrom,
            periodTo,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(Income), periodFrom, periodTo);
        }

        byte[] excel = ReportExcelExporter.BuildIncomeWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "ingresos-por-periodo.xlsx");
    }

    [HttpGet("gastos-por-categoria")]
    public async Task<IActionResult> ExpenseByCategory(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        ExpenseByCategoryReportResult? result = null;

        if (periodFrom is not null && periodTo is not null)
        {
            result = await reportService.GetExpenseByCategoryReportAsync(
                from,
                to,
                accountId,
                CurrentUserName(),
                cancellationToken);
        }

        return View(new ExpenseByCategoryReportViewModel(
            from,
            to,
            accountId,
            await GetCategoryOptionsAsync(cancellationToken),
            result));
    }

    [HttpGet("gastos-por-categoria/pdf")]
    public async Task<IActionResult> ExpenseByCategoryPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        ExpenseByCategoryReportResult result = await reportService.GetExpenseByCategoryReportAsync(
            periodFrom,
            periodTo,
            accountId,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(ExpenseByCategory), periodFrom, periodTo, accountId);
        }

        byte[] pdf = new ExpenseByCategoryReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "gastos-por-categoria.pdf");
    }

    [HttpGet("gastos-por-categoria/excel")]
    public async Task<IActionResult> ExpenseByCategoryExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        ExpenseByCategoryReportResult result = await reportService.GetExpenseByCategoryReportAsync(
            periodFrom,
            periodTo,
            accountId,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(ExpenseByCategory), periodFrom, periodTo, accountId);
        }

        byte[] excel = ReportExcelExporter.BuildExpenseByCategoryWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "gastos-por-categoria.xlsx");
    }

    [HttpGet("presupuesto-vs-ejecutado")]
    public async Task<IActionResult> BudgetVsExecuted(
        int? periodYear,
        int? periodMonth,
        CancellationToken cancellationToken)
    {
        (int year, int month) = NormalizeMonth(periodYear, periodMonth);

        BudgetVsExecutedReportResult result = await reportService.GetBudgetVsExecutedReportAsync(
            year,
            month,
            CurrentUserName(),
            cancellationToken);

        return View(new BudgetVsExecutedReportViewModel(year, month, result));
    }

    [HttpGet("presupuesto-vs-ejecutado/pdf")]
    public async Task<IActionResult> BudgetVsExecutedPdf(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken)
    {
        BudgetVsExecutedReportResult result = await reportService.GetBudgetVsExecutedReportAsync(
            periodYear,
            periodMonth,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(BudgetVsExecuted), new { periodYear, periodMonth });
        }

        byte[] pdf = new BudgetVsExecutedReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "presupuesto-vs-ejecutado.pdf");
    }

    [HttpGet("presupuesto-vs-ejecutado/excel")]
    public async Task<IActionResult> BudgetVsExecutedExcel(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken)
    {
        BudgetVsExecutedReportResult result = await reportService.GetBudgetVsExecutedReportAsync(
            periodYear,
            periodMonth,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(BudgetVsExecuted), new { periodYear, periodMonth });
        }

        byte[] excel = ReportExcelExporter.BuildBudgetVsExecutedWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "presupuesto-vs-ejecutado.xlsx");
    }

    [HttpGet("comparativo-mensual")]
    public async Task<IActionResult> MonthlyComparison(
        int? periodYearFrom,
        int? periodMonthFrom,
        int? periodYearTo,
        int? periodMonthTo,
        CancellationToken cancellationToken)
    {
        (int yearFrom, int monthFrom, int yearTo, int monthTo) = NormalizeMonthRange(
            periodYearFrom, periodMonthFrom, periodYearTo, periodMonthTo);

        MonthlyComparisonReportResult result = await reportService.GetMonthlyComparisonReportAsync(
            yearFrom,
            monthFrom,
            yearTo,
            monthTo,
            CurrentUserName(),
            cancellationToken);

        return View(new MonthlyComparisonReportViewModel(yearFrom, monthFrom, yearTo, monthTo, result));
    }

    [HttpGet("comparativo-mensual/pdf")]
    public async Task<IActionResult> MonthlyComparisonPdf(
        int periodYearFrom,
        int periodMonthFrom,
        int periodYearTo,
        int periodMonthTo,
        CancellationToken cancellationToken)
    {
        MonthlyComparisonReportResult result = await reportService.GetMonthlyComparisonReportAsync(
            periodYearFrom,
            periodMonthFrom,
            periodYearTo,
            periodMonthTo,
            CurrentUserName(),
            cancellationToken);

        if (result.GrandTotalIncome == 0 && result.GrandTotalExpense == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(MonthlyComparison), new { periodYearFrom, periodMonthFrom, periodYearTo, periodMonthTo });
        }

        byte[] pdf = new MonthlyComparisonReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "comparativo-mensual.pdf");
    }

    [HttpGet("comparativo-mensual/excel")]
    public async Task<IActionResult> MonthlyComparisonExcel(
        int periodYearFrom,
        int periodMonthFrom,
        int periodYearTo,
        int periodMonthTo,
        CancellationToken cancellationToken)
    {
        MonthlyComparisonReportResult result = await reportService.GetMonthlyComparisonReportAsync(
            periodYearFrom,
            periodMonthFrom,
            periodYearTo,
            periodMonthTo,
            CurrentUserName(),
            cancellationToken);

        if (result.GrandTotalIncome == 0 && result.GrandTotalExpense == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(MonthlyComparison), new { periodYearFrom, periodMonthFrom, periodYearTo, periodMonthTo });
        }

        byte[] excel = ReportExcelExporter.BuildMonthlyComparisonWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "comparativo-mensual.xlsx");
    }

    [HttpGet("estado-cuenta-cliente")]
    public async Task<IActionResult> CustomerStatement(Guid? customerId, CancellationToken cancellationToken)
    {
        CustomerStatementReportResult? result = customerId is Guid id
            ? await reportService.GetCustomerStatementReportAsync(id, CurrentUserName(), cancellationToken)
            : null;

        return View(new CustomerStatementReportViewModel(
            customerId,
            await GetCustomerOptionsAsync(cancellationToken),
            result));
    }

    [HttpGet("estado-cuenta-cliente/pdf")]
    public async Task<IActionResult> CustomerStatementPdf(Guid customerId, CancellationToken cancellationToken)
    {
        CustomerStatementReportResult result = await reportService.GetCustomerStatementReportAsync(
            customerId, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(CustomerStatement), new { customerId });
        }

        byte[] pdf = new CustomerStatementReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "estado-cuenta-cliente.pdf");
    }

    [HttpGet("estado-cuenta-cliente/excel")]
    public async Task<IActionResult> CustomerStatementExcel(Guid customerId, CancellationToken cancellationToken)
    {
        CustomerStatementReportResult result = await reportService.GetCustomerStatementReportAsync(
            customerId, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(CustomerStatement), new { customerId });
        }

        byte[] excel = ReportExcelExporter.BuildCustomerStatementWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "estado-cuenta-cliente.xlsx");
    }

    [HttpGet("cuentas-por-pagar-proveedor")]
    public async Task<IActionResult> PayableBySupplier(CancellationToken cancellationToken)
    {
        PayableBySupplierReportResult result = await reportService.GetPayableBySupplierReportAsync(
            CurrentUserName(), cancellationToken);

        return View(new PayableBySupplierReportViewModel(result));
    }

    [HttpGet("cuentas-por-pagar-proveedor/pdf")]
    public async Task<IActionResult> PayableBySupplierPdf(CancellationToken cancellationToken)
    {
        PayableBySupplierReportResult result = await reportService.GetPayableBySupplierReportAsync(
            CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(PayableBySupplier));
        }

        byte[] pdf = new PayableBySupplierReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "cuentas-por-pagar-proveedor.pdf");
    }

    [HttpGet("cuentas-por-pagar-proveedor/excel")]
    public async Task<IActionResult> PayableBySupplierExcel(CancellationToken cancellationToken)
    {
        PayableBySupplierReportResult result = await reportService.GetPayableBySupplierReportAsync(
            CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(PayableBySupplier));
        }

        byte[] excel = ReportExcelExporter.BuildPayableBySupplierWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "cuentas-por-pagar-proveedor.xlsx");
    }

    [HttpGet("cuentas-por-cobrar-cliente")]
    public async Task<IActionResult> ReceivableByCustomer(CancellationToken cancellationToken)
    {
        ReceivableByCustomerReportResult result = await reportService.GetReceivableByCustomerReportAsync(
            CurrentUserName(), cancellationToken);

        return View(new ReceivableByCustomerReportViewModel(result));
    }

    [HttpGet("cuentas-por-cobrar-cliente/pdf")]
    public async Task<IActionResult> ReceivableByCustomerPdf(CancellationToken cancellationToken)
    {
        ReceivableByCustomerReportResult result = await reportService.GetReceivableByCustomerReportAsync(
            CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(ReceivableByCustomer));
        }

        byte[] pdf = new ReceivableByCustomerReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "cuentas-por-cobrar-cliente.pdf");
    }

    [HttpGet("cuentas-por-cobrar-cliente/excel")]
    public async Task<IActionResult> ReceivableByCustomerExcel(CancellationToken cancellationToken)
    {
        ReceivableByCustomerReportResult result = await reportService.GetReceivableByCustomerReportAsync(
            CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(ReceivableByCustomer));
        }

        byte[] excel = ReportExcelExporter.BuildReceivableByCustomerWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "cuentas-por-cobrar-cliente.xlsx");
    }

    [HttpGet("libro-diario")]
    public async Task<IActionResult> JournalBook(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);
        JournalBookReportResult result = await reportService.GetJournalBookReportAsync(
            from, to, CurrentUserName(), cancellationToken);

        return View(new JournalBookReportViewModel(from, to, result));
    }

    [HttpGet("libro-diario/pdf")]
    public async Task<IActionResult> JournalBookPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        JournalBookReportResult result = await reportService.GetJournalBookReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Entries.Count == 0)
        {
            return NoDataResult(nameof(JournalBook), periodFrom, periodTo);
        }

        byte[] pdf = new JournalBookReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "libro-diario.pdf");
    }

    [HttpGet("libro-diario/excel")]
    public async Task<IActionResult> JournalBookExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        JournalBookReportResult result = await reportService.GetJournalBookReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Entries.Count == 0)
        {
            return NoDataResult(nameof(JournalBook), periodFrom, periodTo);
        }

        byte[] excel = ReportExcelExporter.BuildJournalBookWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "libro-diario.xlsx");
    }

    [HttpGet("libro-mayor")]
    public async Task<IActionResult> LedgerBook(
        Guid? accountId,
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        LedgerBookReportResult? result = accountId is Guid id
            ? await reportService.GetLedgerBookReportAsync(id, from, to, CurrentUserName(), cancellationToken)
            : null;

        return View(new LedgerBookReportViewModel(
            accountId, from, to, await GetLedgerAccountOptionsAsync(cancellationToken), result));
    }

    [HttpGet("libro-mayor/pdf")]
    public async Task<IActionResult> LedgerBookPdf(
        Guid accountId,
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        LedgerBookReportResult result = await reportService.GetLedgerBookReportAsync(
            accountId, periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Movements.Count == 0 && result.OpeningBalance == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(LedgerBook), new { accountId, periodFrom, periodTo });
        }

        byte[] pdf = new LedgerBookReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "libro-mayor.pdf");
    }

    [HttpGet("libro-mayor/excel")]
    public async Task<IActionResult> LedgerBookExcel(
        Guid accountId,
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        LedgerBookReportResult result = await reportService.GetLedgerBookReportAsync(
            accountId, periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Movements.Count == 0 && result.OpeningBalance == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(LedgerBook), new { accountId, periodFrom, periodTo });
        }

        byte[] excel = ReportExcelExporter.BuildLedgerBookWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "libro-mayor.xlsx");
    }

    [HttpGet("balanza-comprobacion")]
    public async Task<IActionResult> TrialBalance(DateOnly? cutoffDate, CancellationToken cancellationToken)
    {
        DateOnly date = cutoffDate ?? DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        TrialBalanceReportResult result = await reportService.GetTrialBalanceReportAsync(
            date, CurrentUserName(), cancellationToken);

        return View(new TrialBalanceReportViewModel(date, result));
    }

    [HttpGet("balanza-comprobacion/pdf")]
    public async Task<IActionResult> TrialBalancePdf(DateOnly cutoffDate, CancellationToken cancellationToken)
    {
        TrialBalanceReportResult result = await reportService.GetTrialBalanceReportAsync(
            cutoffDate, CurrentUserName(), cancellationToken);

        if (result.Groups.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(TrialBalance), new { cutoffDate });
        }

        byte[] pdf = new TrialBalanceReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "balanza-de-comprobacion.pdf");
    }

    [HttpGet("balanza-comprobacion/excel")]
    public async Task<IActionResult> TrialBalanceExcel(DateOnly cutoffDate, CancellationToken cancellationToken)
    {
        TrialBalanceReportResult result = await reportService.GetTrialBalanceReportAsync(
            cutoffDate, CurrentUserName(), cancellationToken);

        if (result.Groups.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(TrialBalance), new { cutoffDate });
        }

        byte[] excel = ReportExcelExporter.BuildTrialBalanceWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "balanza-de-comprobacion.xlsx");
    }

    [HttpGet("estado-resultados")]
    public async Task<IActionResult> IncomeStatement(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);
        IncomeStatementReportResult result = await reportService.GetIncomeStatementReportAsync(
            from, to, CurrentUserName(), cancellationToken);

        return View(new IncomeStatementReportViewModel(from, to, result));
    }

    [HttpGet("estado-resultados/pdf")]
    public async Task<IActionResult> IncomeStatementPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        IncomeStatementReportResult result = await reportService.GetIncomeStatementReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.IncomeGroups.Count == 0 && result.ExpenseGroups.Count == 0)
        {
            return NoDataResult(nameof(IncomeStatement), periodFrom, periodTo);
        }

        byte[] pdf = new IncomeStatementReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "estado-de-resultados.pdf");
    }

    [HttpGet("estado-resultados/excel")]
    public async Task<IActionResult> IncomeStatementExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        IncomeStatementReportResult result = await reportService.GetIncomeStatementReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.IncomeGroups.Count == 0 && result.ExpenseGroups.Count == 0)
        {
            return NoDataResult(nameof(IncomeStatement), periodFrom, periodTo);
        }

        byte[] excel = ReportExcelExporter.BuildIncomeStatementWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "estado-de-resultados.xlsx");
    }

    /// <summary>
    /// Estimación del impuesto sobre la renta de un período fiscal (RF-99). Sin período indicado, usa
    /// el período fiscal vigente; se ofrecen el vigente y los dos anteriores.
    /// </summary>
    [HttpGet("estimacion-renta")]
    public async Task<IActionResult> IncomeTaxEstimate(
        DateOnly? fiscalPeriodStartDate,
        [FromServices] IIncomeTaxService incomeTaxService,
        CancellationToken cancellationToken)
    {
        DateOnly current = await GetCurrentFiscalPeriodStartAsync(cancellationToken);
        DateOnly selected = NormalizeFiscalPeriodStart(fiscalPeriodStartDate) ?? current;

        IncomeTaxEstimateReportResult? result = await incomeTaxService.GetEstimateAsync(
            selected, CurrentUserName(), cancellationToken);

        DateOnly[] options = [current, current.AddYears(-1), current.AddYears(-2)];
        if (!options.Contains(selected))
        {
            options = [.. options, selected];
        }

        return View(new IncomeTaxEstimateReportViewModel(selected, options, result));
    }

    [HttpGet("estimacion-renta/pdf")]
    public async Task<IActionResult> IncomeTaxEstimatePdf(
        DateOnly fiscalPeriodStartDate,
        [FromServices] IIncomeTaxService incomeTaxService,
        CancellationToken cancellationToken)
    {
        IncomeTaxEstimateReportResult? result = await GetIncomeTaxEstimateOrNullAsync(
            fiscalPeriodStartDate, incomeTaxService, cancellationToken);

        if (result is null || !result.HasData)
        {
            return IncomeTaxEstimateNoData(fiscalPeriodStartDate);
        }

        byte[] pdf = new IncomeTaxEstimateReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "estimacion-impuesto-renta.pdf");
    }

    [HttpGet("estimacion-renta/excel")]
    public async Task<IActionResult> IncomeTaxEstimateExcel(
        DateOnly fiscalPeriodStartDate,
        [FromServices] IIncomeTaxService incomeTaxService,
        CancellationToken cancellationToken)
    {
        IncomeTaxEstimateReportResult? result = await GetIncomeTaxEstimateOrNullAsync(
            fiscalPeriodStartDate, incomeTaxService, cancellationToken);

        if (result is null || !result.HasData)
        {
            return IncomeTaxEstimateNoData(fiscalPeriodStartDate);
        }

        byte[] excel = ReportExcelExporter.BuildIncomeTaxEstimateWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "estimacion-impuesto-renta.xlsx");
    }

    private async Task<IncomeTaxEstimateReportResult?> GetIncomeTaxEstimateOrNullAsync(
        DateOnly fiscalPeriodStartDate,
        IIncomeTaxService incomeTaxService,
        CancellationToken cancellationToken)
    {
        return NormalizeFiscalPeriodStart(fiscalPeriodStartDate) is DateOnly start
            ? await incomeTaxService.GetEstimateAsync(start, CurrentUserName(), cancellationToken)
            : null;
    }

    private RedirectToActionResult IncomeTaxEstimateNoData(DateOnly fiscalPeriodStartDate)
    {
        TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
        return RedirectToAction(
            nameof(IncomeTaxEstimate),
            new { fiscalPeriodStartDate = fiscalPeriodStartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });
    }

    /// <summary>Un período fiscal válido inicia el primer día de un mes; cualquier otro valor se descarta.</summary>
    private static DateOnly? NormalizeFiscalPeriodStart(DateOnly? value) =>
        value is DateOnly date && date.Day == 1 && date.Year < DateOnly.MaxValue.Year ? date : null;

    private async Task<DateOnly> GetCurrentFiscalPeriodStartAsync(CancellationToken cancellationToken)
    {
        SystemParametersModel? parameters = await systemParametersService.GetAsync(cancellationToken);
        return parameters?.FiscalPeriodStartDate
            ?? new DateOnly(timeProvider.GetLocalNow().Year, 1, 1);
    }

    [HttpGet("presupuesto-anual")]
    public async Task<IActionResult> AnnualBudget(
        int? periodYear,
        int? upToMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly fiscalStart, DateOnly fiscalEnd) = await GetFiscalPeriodAsync(cancellationToken);
        if (periodYear is int requestedYear && requestedYear != fiscalStart.Year)
        {
            TempData["ErrorMessage"] = "Este reporte utiliza el período fiscal configurado actualmente.";
            return RedirectToAction(nameof(AnnualBudget), new
            {
                periodYear = fiscalStart.Year,
                upToMonth = upToMonth ?? GetCurrentFiscalMonth(fiscalStart, fiscalEnd),
            });
        }

        int year = fiscalStart.Year;
        int month = upToMonth ?? GetCurrentFiscalMonth(fiscalStart, fiscalEnd);

        AnnualBudgetReportResult result = await reportService.GetAnnualBudgetReportAsync(
            year, month, CurrentUserName(), cancellationToken);

        return View(new AnnualBudgetReportViewModel(year, month, result, fiscalStart, fiscalEnd));
    }

    [HttpGet("presupuesto-anual/pdf")]
    public async Task<IActionResult> AnnualBudgetPdf(
        int periodYear,
        int upToMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly fiscalStart, _) = await GetFiscalPeriodAsync(cancellationToken);
        if (periodYear != fiscalStart.Year)
        {
            TempData["ErrorMessage"] = "Este reporte utiliza el período fiscal configurado actualmente.";
            return RedirectToAction(nameof(AnnualBudget), new
            {
                periodYear = fiscalStart.Year,
                upToMonth = upToMonth,
            });
        }

        AnnualBudgetReportResult result = await reportService.GetAnnualBudgetReportAsync(
            periodYear, upToMonth, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(AnnualBudget), new { periodYear, upToMonth });
        }

        byte[] pdf = new AnnualBudgetReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "presupuesto-anual.pdf");
    }

    [HttpGet("presupuesto-anual/excel")]
    public async Task<IActionResult> AnnualBudgetExcel(
        int periodYear,
        int upToMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly fiscalStart, _) = await GetFiscalPeriodAsync(cancellationToken);
        if (periodYear != fiscalStart.Year)
        {
            TempData["ErrorMessage"] = "Este reporte utiliza el período fiscal configurado actualmente.";
            return RedirectToAction(nameof(AnnualBudget), new
            {
                periodYear = fiscalStart.Year,
                upToMonth = upToMonth,
            });
        }

        AnnualBudgetReportResult result = await reportService.GetAnnualBudgetReportAsync(
            periodYear, upToMonth, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(AnnualBudget), new { periodYear, upToMonth });
        }

        byte[] excel = ReportExcelExporter.BuildAnnualBudgetWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "presupuesto-anual.xlsx");
    }

    [HttpGet("flujo-efectivo")]
    public async Task<IActionResult> CashFlow(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        Guid? cashAccountId,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        CashFlowReportResult result = await reportService.GetCashFlowReportAsync(
            from, to, cashAccountId, CurrentUserName(), cancellationToken);

        return View(new CashFlowReportViewModel(
            from, to, cashAccountId, await GetCashAccountOptionsAsync(cancellationToken), result));
    }

    [HttpGet("flujo-efectivo/pdf")]
    public async Task<IActionResult> CashFlowPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? cashAccountId,
        CancellationToken cancellationToken)
    {
        CashFlowReportResult result = await reportService.GetCashFlowReportAsync(
            periodFrom, periodTo, cashAccountId, CurrentUserName(), cancellationToken);

        if (result.GrandTotalInflow == 0 && result.GrandTotalOutflow == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(CashFlow), new { periodFrom, periodTo, cashAccountId });
        }

        byte[] pdf = new CashFlowReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "flujo-efectivo-mensual.pdf");
    }

    [HttpGet("flujo-efectivo/excel")]
    public async Task<IActionResult> CashFlowExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? cashAccountId,
        CancellationToken cancellationToken)
    {
        CashFlowReportResult result = await reportService.GetCashFlowReportAsync(
            periodFrom, periodTo, cashAccountId, CurrentUserName(), cancellationToken);

        if (result.GrandTotalInflow == 0 && result.GrandTotalOutflow == 0)
        {
            TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
            return RedirectToAction(nameof(CashFlow), new { periodFrom, periodTo, cashAccountId });
        }

        byte[] excel = ReportExcelExporter.BuildCashFlowWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "flujo-efectivo-mensual.xlsx");
    }

    [HttpGet("iva-periodo")]
    public async Task<IActionResult> VatPeriod(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        VatPeriodReportResult result = await reportService.GetVatPeriodReportAsync(
            from, to, CurrentUserName(), cancellationToken);

        return View(new VatPeriodReportViewModel(from, to, result));
    }

    [HttpGet("iva-periodo/pdf")]
    public async Task<IActionResult> VatPeriodPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        VatPeriodReportResult result = await reportService.GetVatPeriodReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(VatPeriod), periodFrom, periodTo);
        }

        byte[] pdf = new VatPeriodReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "iva-del-periodo.pdf");
    }

    [HttpGet("iva-periodo/excel")]
    public async Task<IActionResult> VatPeriodExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        VatPeriodReportResult result = await reportService.GetVatPeriodReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(VatPeriod), periodFrom, periodTo);
        }

        byte[] excel = ReportExcelExporter.BuildVatPeriodWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "iva-del-periodo.xlsx");
    }

    [HttpGet("retenciones-por-clientes")]
    public async Task<IActionResult> WithholdingByCustomer(
        DateOnly? periodFrom,
        DateOnly? periodTo,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        WithholdingByCustomerReportResult result = await reportService.GetWithholdingByCustomerReportAsync(
            from, to, CurrentUserName(), cancellationToken);

        return View(new WithholdingByCustomerReportViewModel(from, to, result));
    }

    [HttpGet("retenciones-por-clientes/pdf")]
    public async Task<IActionResult> WithholdingByCustomerPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        WithholdingByCustomerReportResult result = await reportService.GetWithholdingByCustomerReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(WithholdingByCustomer), periodFrom, periodTo);
        }

        byte[] pdf = new WithholdingByCustomerReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "retenciones-por-clientes.pdf");
    }

    [HttpGet("retenciones-por-clientes/excel")]
    public async Task<IActionResult> WithholdingByCustomerExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        CancellationToken cancellationToken)
    {
        WithholdingByCustomerReportResult result = await reportService.GetWithholdingByCustomerReportAsync(
            periodFrom, periodTo, CurrentUserName(), cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(WithholdingByCustomer), periodFrom, periodTo);
        }

        byte[] excel = ReportExcelExporter.BuildWithholdingByCustomerWorkbook(result);
        return File(
            excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "retenciones-por-clientes.xlsx");
    }

    private (int Year, int Month) NormalizeMonth(int? periodYear, int? periodMonth)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return (periodYear ?? today.Year, periodMonth ?? today.Month);
    }

    private async Task<(DateOnly Start, DateOnly End)> GetFiscalPeriodAsync(
        CancellationToken cancellationToken)
    {
        SystemParametersModel? parameters = await systemParametersService.GetAsync(cancellationToken);
        if (parameters?.FiscalPeriodStartDate is DateOnly start
            && parameters.FiscalPeriodEndDate is DateOnly end)
        {
            return (start, end);
        }

        if (parameters?.FiscalPeriodStartDate is not null || parameters?.FiscalPeriodEndDate is not null)
        {
            throw new InvalidOperationException("La configuración del período fiscal está incompleta.");
        }

        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DateOnly calendarStart = new(today.Year, 1, 1);
        return (calendarStart, calendarStart.AddYears(1).AddDays(-1));
    }

    private int GetCurrentFiscalMonth(DateOnly fiscalStart, DateOnly fiscalEnd)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        if (today < fiscalStart)
        {
            return 1;
        }

        if (today > fiscalEnd)
        {
            return 12;
        }

        return (today.Year - fiscalStart.Year) * 12 + today.Month - fiscalStart.Month + 1;
    }

    private (int YearFrom, int MonthFrom, int YearTo, int MonthTo) NormalizeMonthRange(
        int? periodYearFrom, int? periodMonthFrom, int? periodYearTo, int? periodMonthTo)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DateOnly defaultFrom = new DateOnly(today.Year, today.Month, 1).AddMonths(-5);

        return (
            periodYearFrom ?? defaultFrom.Year,
            periodMonthFrom ?? defaultFrom.Month,
            periodYearTo ?? today.Year,
            periodMonthTo ?? today.Month);
    }

    private RedirectToActionResult NoDataResult(
        string actionName,
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? accountId = null)
    {
        TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
        return RedirectToAction(actionName, new { periodFrom, periodTo, accountId });
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCategoryOptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerAccountOption> categories = await ledgerAccountService.GetActiveOptionsAsync(
            LedgerAccountType.Expense,
            cancellationToken: cancellationToken);

        return categories
            .Select(option => new SelectListItem($"{option.Code} — {option.Name}", option.Id.ToString()))
            .ToArray();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCustomerOptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomerOption> customers = await customerService.GetActiveOptionsAsync(cancellationToken);

        return customers
            .Select(option => new SelectListItem($"{option.Code} — {option.Name}", option.Id.ToString()))
            .ToArray();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetLedgerAccountOptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerAccountOption> accounts = await ledgerAccountService.GetDetailAccountOptionsAsync(cancellationToken);

        return accounts
            .Select(option => new SelectListItem($"{option.Code} — {option.Name}", option.Id.ToString()))
            .ToArray();
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCashAccountOptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> accounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);

        return accounts
            .Select(option => new SelectListItem($"{option.Code} — {option.Name}", option.Id.ToString()))
            .ToArray();
    }

    private string CurrentUserName() =>
        User.FindFirst(SystemClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? "—";

    private (DateOnly From, DateOnly To) NormalizePeriod(DateOnly? periodFrom, DateOnly? periodTo)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DateOnly defaultFrom = new(today.Year, today.Month, 1);
        DateOnly defaultTo = defaultFrom.AddMonths(1).AddDays(-1);

        return (periodFrom ?? defaultFrom, periodTo ?? defaultTo);
    }
}
