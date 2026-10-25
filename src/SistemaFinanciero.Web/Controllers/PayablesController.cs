using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Payables;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el listado y el detalle de las cuentas por pagar.</summary>
/// <remarks>
/// Conexiones: usa <c>IPayableService</c> para la cuenta por pagar, <c>ISupplierPaymentService</c>
/// para su historial de pagos e <c>IBalanceAdjustmentService</c> para su historial de ajustes de
/// saldo (regla 4). Solo Gerencia y Finanzas ven esta pantalla (política <c>Reportes.Ver</c>), igual
/// criterio que <c>ReceivablesController</c>. Registrar y anular un pago lo hace
/// <c>SupplierPaymentsController</c>; ajustar el saldo lo hace <c>BalanceAdjustmentsController</c>.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("cuentas-por-pagar")]
public sealed class PayablesController(
    IPayableService service,
    ISupplierPaymentService paymentService,
    IBalanceAdjustmentService adjustmentService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? expenseCode,
        string? supplierName,
        decimal? balanceMin,
        decimal? balanceMax,
        DateOnly? dueDateFrom,
        DateOnly? dueDateTo,
        PayableStatusFilter status = PayableStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        PayableQuery query = new(
            NormalizeSearch(expenseCode),
            NormalizeSearch(supplierName),
            balanceMin,
            balanceMax,
            dueDateFrom,
            dueDateTo,
            NormalizeStatus(status),
            page);
        PagedResult<PayableModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new PayableIndexViewModel(results, query));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        PayableModel? payable = await service.GetAsync(id, cancellationToken);

        if (payable is null)
        {
            return NotFound();
        }

        IReadOnlyList<SupplierPaymentModel> payments = await paymentService.SearchByPayableAsync(
            id,
            cancellationToken);
        IReadOnlyList<BalanceAdjustmentModel> adjustments = await adjustmentService.SearchByPayableAsync(
            id,
            cancellationToken);

        bool canRegisterPayment = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.ConfirmFinancialTransactions)).Succeeded;
        bool canCancelPayment = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;
        bool canAdjust = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.AdjustFinancialBalances)).Succeeded;

        return View(new PayableDetailsViewModel(
            payable, payments, adjustments, canRegisterPayment, canCancelPayment, canAdjust));
    }

    private static PayableStatusFilter NormalizeStatus(PayableStatusFilter status) =>
        Enum.IsDefined(status) ? status : PayableStatusFilter.All;
}
