using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Customers;
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
/// categoría y de cliente, <c>IFinancialCategoryService</c> e <c>ICustomerCatalogService</c>. Misma
/// política que Presupuesto y Consultas: Asistente no entra. Cuando el reporte no tiene datos para
/// los parámetros indicados, no se genera ningún archivo: se vuelve al formulario con un mensaje
/// (regla 8).
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("reportes")]
public sealed class ReportsController(
    IReportService reportService,
    IFinancialCategoryService categoryService,
    ICustomerCatalogService customerService) : Controller
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
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        (DateOnly from, DateOnly to) = NormalizePeriod(periodFrom, periodTo);

        ExpenseByCategoryReportResult? result = null;

        if (periodFrom is not null && periodTo is not null)
        {
            result = await reportService.GetExpenseByCategoryReportAsync(
                from,
                to,
                categoryId,
                CurrentUserName(),
                cancellationToken);
        }

        return View(new ExpenseByCategoryReportViewModel(
            from,
            to,
            categoryId,
            await GetCategoryOptionsAsync(cancellationToken),
            result));
    }

    [HttpGet("gastos-por-categoria/pdf")]
    public async Task<IActionResult> ExpenseByCategoryPdf(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        ExpenseByCategoryReportResult result = await reportService.GetExpenseByCategoryReportAsync(
            periodFrom,
            periodTo,
            categoryId,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(ExpenseByCategory), periodFrom, periodTo, categoryId);
        }

        byte[] pdf = new ExpenseByCategoryReportPdfDocument(result).GeneratePdf();
        return File(pdf, "application/pdf", "gastos-por-categoria.pdf");
    }

    [HttpGet("gastos-por-categoria/excel")]
    public async Task<IActionResult> ExpenseByCategoryExcel(
        DateOnly periodFrom,
        DateOnly periodTo,
        Guid? categoryId,
        CancellationToken cancellationToken)
    {
        ExpenseByCategoryReportResult result = await reportService.GetExpenseByCategoryReportAsync(
            periodFrom,
            periodTo,
            categoryId,
            CurrentUserName(),
            cancellationToken);

        if (result.Lines.Count == 0)
        {
            return NoDataResult(nameof(ExpenseByCategory), periodFrom, periodTo, categoryId);
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

    private static (int Year, int Month) NormalizeMonth(int? periodYear, int? periodMonth)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        return (periodYear ?? today.Year, periodMonth ?? today.Month);
    }

    private static (int YearFrom, int MonthFrom, int YearTo, int MonthTo) NormalizeMonthRange(
        int? periodYearFrom, int? periodMonthFrom, int? periodYearTo, int? periodMonthTo)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
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
        Guid? categoryId = null)
    {
        TempData["ErrorMessage"] = "No hay datos para los parámetros indicados; no se generó ningún archivo.";
        return RedirectToAction(actionName, new { periodFrom, periodTo, categoryId });
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCategoryOptionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<FinancialCategoryOption> categories = await categoryService.GetActiveOptionsAsync(
            FinancialCategoryKind.Expense,
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

    private string CurrentUserName() =>
        User.FindFirst(SystemClaimTypes.FullName)?.Value ?? User.Identity?.Name ?? "—";

    private static (DateOnly From, DateOnly To) NormalizePeriod(DateOnly? periodFrom, DateOnly? periodTo)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        DateOnly defaultFrom = new(today.Year, today.Month, 1);
        DateOnly defaultTo = defaultFrom.AddMonths(1).AddDays(-1);

        return (periodFrom ?? defaultFrom, periodTo ?? defaultTo);
    }
}
