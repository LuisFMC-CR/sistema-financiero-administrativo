using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Receivables;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el listado y el detalle de las cuentas por cobrar.</summary>
/// <remarks>
/// Conexiones: usa <c>IReceivableService</c> para la cuenta por cobrar, <c>IReceivablePaymentService</c>
/// para su historial de abonos e <c>IBalanceAdjustmentService</c> para su historial de ajustes de
/// saldo (regla 4). Solo Gerencia y Finanzas ven esta pantalla (política <c>Reportes.Ver</c>):
/// Asistente "no ve... saldos" (perfiles). Registrar y anular un abono lo hace
/// <c>ReceivablePaymentsController</c>; ajustar el saldo lo hace <c>BalanceAdjustmentsController</c>.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("cuentas-por-cobrar")]
public sealed class ReceivablesController(
    IReceivableService service,
    IReceivablePaymentService paymentService,
    IBalanceAdjustmentService adjustmentService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? invoiceNumber,
        string? customerName,
        decimal? balanceMin,
        decimal? balanceMax,
        DateOnly? dueDateFrom,
        DateOnly? dueDateTo,
        ReceivableStatusFilter status = ReceivableStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ReceivableQuery query = new(
            NormalizeSearch(invoiceNumber),
            NormalizeSearch(customerName),
            balanceMin,
            balanceMax,
            dueDateFrom,
            dueDateTo,
            NormalizeStatus(status),
            page);
        PagedResult<ReceivableModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new ReceivableIndexViewModel(results, query));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        ReceivableModel? receivable = await service.GetAsync(id, cancellationToken);

        if (receivable is null)
        {
            return NotFound();
        }

        IReadOnlyList<ReceivablePaymentModel> payments = await paymentService.SearchByReceivableAsync(
            id,
            cancellationToken);
        IReadOnlyList<BalanceAdjustmentModel> adjustments = await adjustmentService.SearchByReceivableAsync(
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

        return View(new ReceivableDetailsViewModel(
            receivable, payments, adjustments, canRegisterPayment, canCancelPayment, canAdjust));
    }

    private static ReceivableStatusFilter NormalizeStatus(ReceivableStatusFilter status) =>
        Enum.IsDefined(status) ? status : ReceivableStatusFilter.All;
}
