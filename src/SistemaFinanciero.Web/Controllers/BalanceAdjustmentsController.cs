using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Adjustments;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Registra ajustes manuales de saldo sobre cuentas por cobrar y por pagar (regla de negocio 4).
/// Exclusivo de Gerencia.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IBalanceAdjustmentService</c> para registrar el ajuste, e
/// <c>IReceivableService</c>/<c>IPayableService</c> solo para mostrar la cuenta y su saldo actual en
/// el formulario. No toca efectivo, documento ni presupuesto (regla 4).
/// </remarks>
[Authorize(Policy = SystemPolicies.AdjustFinancialBalances)]
public sealed class BalanceAdjustmentsController(
    IBalanceAdjustmentService adjustmentService,
    IReceivableService receivableService,
    IPayableService payableService) : CatalogControllerBase
{
    [HttpGet("cuentas-por-cobrar/{id:guid}/ajustar")]
    public async Task<IActionResult> AdjustReceivable(Guid id, CancellationToken cancellationToken)
    {
        ReceivableModel? receivable = await receivableService.GetAsync(id, cancellationToken);

        if (receivable is null)
        {
            return NotFound();
        }

        return View(new BalanceAdjustmentFormViewModel
        {
            AccountLabel = $"Factura N° {receivable.InvoiceNumber?.ToString(CultureInfo.InvariantCulture) ?? "—"}",
            CurrentBalance = receivable.Balance,
            OriginalAmount = receivable.OriginalAmount,
            Version = receivable.Version,
        });
    }

    [HttpPost("cuentas-por-cobrar/{id:guid}/ajustar")]
    public async Task<IActionResult> AdjustReceivable(
        Guid id,
        BalanceAdjustmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await adjustmentService.AdjustReceivableAsync(
            id, model.Amount, model.Direction, model.Reason, model.Version, actorId, cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El saldo de la cuenta por cobrar fue ajustado.");
            return RedirectToAction("Details", "Receivables", new { id });
        }

        ReceivableModel? latest = await receivableService.GetAsync(id, cancellationToken);

        if (latest is null)
        {
            return NotFound();
        }

        model.AccountLabel = $"Factura N° {latest.InvoiceNumber?.ToString(CultureInfo.InvariantCulture) ?? "—"}";
        model.CurrentBalance = latest.Balance;
        model.OriginalAmount = latest.OriginalAmount;

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(AdjustReceivable));
    }

    [HttpGet("cuentas-por-pagar/{id:guid}/ajustar")]
    public async Task<IActionResult> AdjustPayable(Guid id, CancellationToken cancellationToken)
    {
        PayableModel? payable = await payableService.GetAsync(id, cancellationToken);

        if (payable is null)
        {
            return NotFound();
        }

        return View(new BalanceAdjustmentFormViewModel
        {
            AccountLabel = $"Gasto {payable.ExpenseCode}",
            CurrentBalance = payable.Balance,
            OriginalAmount = payable.OriginalAmount,
            Version = payable.Version,
        });
    }

    [HttpPost("cuentas-por-pagar/{id:guid}/ajustar")]
    public async Task<IActionResult> AdjustPayable(
        Guid id,
        BalanceAdjustmentFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await adjustmentService.AdjustPayableAsync(
            id, model.Amount, model.Direction, model.Reason, model.Version, actorId, cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El saldo de la cuenta por pagar fue ajustado.");
            return RedirectToAction("Details", "Payables", new { id });
        }

        PayableModel? latest = await payableService.GetAsync(id, cancellationToken);

        if (latest is null)
        {
            return NotFound();
        }

        model.AccountLabel = $"Gasto {latest.ExpenseCode}";
        model.CurrentBalance = latest.Balance;
        model.OriginalAmount = latest.OriginalAmount;

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(AdjustPayable));
    }
}
