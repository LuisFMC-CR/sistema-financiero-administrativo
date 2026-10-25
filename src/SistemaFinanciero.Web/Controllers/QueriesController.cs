using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Expenses;
using SistemaFinanciero.Web.Models.Incomes;
using SistemaFinanciero.Web.Models.Invoices;
using SistemaFinanciero.Web.Models.Overdue;
using SistemaFinanciero.Web.Models.Payables;
using SistemaFinanciero.Web.Models.Queries;
using SistemaFinanciero.Web.Models.Receivables;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Pantalla única de Consultas con filtros combinados (regla 11): un selector de tipo de documento
/// más los filtros (fecha, estado, cliente, proveedor, categoría, cuenta contable) que apliquen a ese
/// tipo. No duplica lógica de búsqueda: reutiliza el <c>SearchAsync</c> que ya existe de cada módulo y
/// traduce su resultado a una fila genérica que enlaza al <c>Details</c> ya construido de ese módulo,
/// que ya muestra sus registros relacionados e historial.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IInvoiceService</c>, <c>IExpenseService</c>, <c>IReceivableService</c>,
/// <c>IPayableService</c> e <c>IIncomeService</c>. También incluye el indicador de cuentas vencidas
/// (regla 9) compartido con <c>HomeController</c>, mediante <c>_OverdueIndicator.cshtml</c>. Solo
/// Gerencia y Finanzas entran (política <c>Reportes.Ver</c>): los filtros de los listados propios de
/// cada módulo no la reemplazan (regla 11), pero esta sí muestra información que Asistente no ve.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("consultas")]
public sealed class QueriesController(
    IInvoiceService invoiceService,
    IExpenseService expenseService,
    IReceivableService receivableService,
    IPayableService payableService,
    IIncomeService incomeService,
    ISystemParametersService systemParametersService) : CatalogControllerBase
{
    private const int DefaultOverdueAlertDays = 7;

    [HttpGet("")]
    public async Task<IActionResult> Index(
        QueryDocumentType type = QueryDocumentType.Invoice,
        DateOnly? dateFrom = null,
        DateOnly? dateTo = null,
        string? status = null,
        string? customerName = null,
        string? supplierName = null,
        string? categoryName = null,
        string? cashAccountName = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
        {
            type = QueryDocumentType.Invoice;
        }

        PagedResult<QueryResultRow> results = type switch
        {
            QueryDocumentType.Invoice => await SearchInvoicesAsync(dateFrom, dateTo, status, customerName, page, cancellationToken),
            QueryDocumentType.Expense => await SearchExpensesAsync(dateFrom, dateTo, status, supplierName, categoryName, page, cancellationToken),
            QueryDocumentType.Receivable => await SearchReceivablesAsync(dateFrom, dateTo, status, customerName, page, cancellationToken),
            QueryDocumentType.Payable => await SearchPayablesAsync(dateFrom, dateTo, status, supplierName, page, cancellationToken),
            QueryDocumentType.Income => await SearchIncomesAsync(dateFrom, dateTo, status, categoryName, cashAccountName, page, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        OverdueIndicatorViewModel overdueIndicator = await BuildOverdueIndicatorAsync(cancellationToken);

        return View(new QueriesIndexViewModel(
            type,
            dateFrom,
            dateTo,
            status,
            customerName,
            supplierName,
            categoryName,
            cashAccountName,
            results,
            overdueIndicator));
    }

    private async Task<PagedResult<QueryResultRow>> SearchInvoicesAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? status,
        string? customerName,
        int page,
        CancellationToken cancellationToken)
    {
        InvoiceStatusFilter statusFilter = ParseStatus(status, InvoiceStatusFilter.All);
        PagedResult<InvoiceSummaryModel> page_ = await invoiceService.SearchAsync(
            new InvoiceQuery(
                search: NormalizeSearch(customerName),
                status: statusFilter,
                createdByUserId: null,
                issueDateFrom: dateFrom,
                issueDateTo: dateTo,
                page: page),
            cancellationToken);

        return Map(page_, invoice => new QueryResultRow(
            invoice.Id,
            invoice.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "Borrador",
            invoice.IssueDate,
            invoice.CustomerName,
            invoice.TotalAmount,
            invoice.Status.ToDisplayName(),
            "Invoices"));
    }

    private async Task<PagedResult<QueryResultRow>> SearchExpensesAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? status,
        string? supplierName,
        string? categoryName,
        int page,
        CancellationToken cancellationToken)
    {
        ExpenseStatusFilter statusFilter = ParseStatus(status, ExpenseStatusFilter.All);
        PagedResult<ExpenseModel> page_ = await expenseService.SearchAsync(
            new ExpenseQuery(
                code: null,
                supplierName: NormalizeSearch(supplierName),
                categoryName: NormalizeSearch(categoryName),
                issueDateFrom: dateFrom,
                issueDateTo: dateTo,
                amountMin: null,
                amountMax: null,
                status: statusFilter,
                createdByUserId: null,
                page: page),
            cancellationToken);

        return Map(page_, expense => new QueryResultRow(
            expense.Id,
            expense.Code,
            expense.IssueDate,
            $"{expense.SupplierName} · {expense.CategoryName}",
            expense.TotalAmount,
            expense.Status.ToDisplayName(),
            "Expenses"));
    }

    private async Task<PagedResult<QueryResultRow>> SearchReceivablesAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? status,
        string? customerName,
        int page,
        CancellationToken cancellationToken)
    {
        ReceivableStatusFilter statusFilter = ParseStatus(status, ReceivableStatusFilter.All);
        PagedResult<ReceivableModel> page_ = await receivableService.SearchAsync(
            new ReceivableQuery(
                invoiceNumber: null,
                customerName: NormalizeSearch(customerName),
                balanceMin: null,
                balanceMax: null,
                dueDateFrom: dateFrom,
                dueDateTo: dateTo,
                status: statusFilter,
                page: page),
            cancellationToken);

        return Map(page_, receivable => new QueryResultRow(
            receivable.Id,
            receivable.InvoiceNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—",
            receivable.DueDate,
            receivable.CustomerName,
            receivable.Balance,
            receivable.Status.ToDisplayName(),
            "Receivables"));
    }

    private async Task<PagedResult<QueryResultRow>> SearchPayablesAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? status,
        string? supplierName,
        int page,
        CancellationToken cancellationToken)
    {
        PayableStatusFilter statusFilter = ParseStatus(status, PayableStatusFilter.All);
        PagedResult<PayableModel> page_ = await payableService.SearchAsync(
            new PayableQuery(
                expenseCode: null,
                supplierName: NormalizeSearch(supplierName),
                balanceMin: null,
                balanceMax: null,
                dueDateFrom: dateFrom,
                dueDateTo: dateTo,
                status: statusFilter,
                page: page),
            cancellationToken);

        return Map(page_, payable => new QueryResultRow(
            payable.Id,
            payable.ExpenseCode,
            payable.DueDate,
            payable.SupplierName,
            payable.Balance,
            payable.Status.ToDisplayName(),
            "Payables"));
    }

    private async Task<PagedResult<QueryResultRow>> SearchIncomesAsync(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? status,
        string? categoryName,
        string? cashAccountName,
        int page,
        CancellationToken cancellationToken)
    {
        IncomeStatusFilter statusFilter = ParseStatus(status, IncomeStatusFilter.Active);
        PagedResult<IncomeModel> page_ = await incomeService.SearchAsync(
            new IncomeQuery(
                incomeDateFrom: dateFrom,
                incomeDateTo: dateTo,
                origin: IncomeOriginFilter.All,
                categoryName: NormalizeSearch(categoryName),
                cashAccountName: NormalizeSearch(cashAccountName),
                referenceOrDescription: null,
                amountMin: null,
                amountMax: null,
                status: statusFilter,
                page: page),
            cancellationToken);

        return Map(page_, income => new QueryResultRow(
            income.Id,
            income.Code,
            income.IncomeDate,
            income.CategoryName,
            income.Amount,
            income.Status.ToDisplayName(),
            "Incomes"));
    }

    private async Task<OverdueIndicatorViewModel> BuildOverdueIndicatorAsync(CancellationToken cancellationToken)
    {
        SystemParametersModel? parameters = await systemParametersService.GetAsync(cancellationToken);
        int alertDays = parameters?.OverdueAlertDays ?? DefaultOverdueAlertDays;

        OverdueSummary receivablesSummary = await receivableService.GetOverdueSummaryAsync(alertDays, cancellationToken);
        OverdueSummary payablesSummary = await payableService.GetOverdueSummaryAsync(alertDays, cancellationToken);

        return new OverdueIndicatorViewModel(receivablesSummary, payablesSummary, alertDays);
    }

    private static TEnum ParseStatus<TEnum>(string? status, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (status is not null && Enum.TryParse(status, ignoreCase: true, out TEnum parsed) && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static PagedResult<QueryResultRow> Map<T>(PagedResult<T> page, Func<T, QueryResultRow> map)
    {
        return new PagedResult<QueryResultRow>(
            page.Items.Select(map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }
}
