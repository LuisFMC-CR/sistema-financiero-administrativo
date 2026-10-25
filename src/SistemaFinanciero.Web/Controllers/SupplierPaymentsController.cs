using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Payables;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Registra y anula pagos, incluido el reparto a varias cuentas por pagar del mismo proveedor.</summary>
/// <remarks>
/// Conexiones: usa <c>IPayableService</c> para proponer el reparto (de la más antigua a la más nueva,
/// regla 2) e <c>ISupplierPaymentService</c> para registrarlo o anularlo. <c>ISupplierCatalogService</c>
/// e <c>ILedgerAccountService</c> alimentan los selectores. El registro es un asistente de dos pasos,
/// igual criterio que <c>ReceivablePaymentsController</c> pero sin retención ni categoría: un pago no
/// genera ningún ingreso ni documento equivalente (regla 2/3).
/// </remarks>
[Route("cuentas-por-pagar/pagos")]
public sealed class SupplierPaymentsController(
    IPayableService payableService,
    ISupplierPaymentService paymentService,
    ISupplierCatalogService supplierService,
    ILedgerAccountService ledgerAccountService) : CatalogControllerBase
{
    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("nuevo")]
    public async Task<IActionResult> Start(Guid? proveedorId, CancellationToken cancellationToken)
    {
        RegisterSupplierPaymentStartViewModel model = new() { SupplierId = proveedorId };
        await PopulateStartOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("nuevo")]
    public async Task<IActionResult> Start(
        RegisterSupplierPaymentStartViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateStartOptionsAsync(model, cancellationToken);
            return View(model);
        }

        IReadOnlyList<PayableModel> pending = await payableService.GetPendingBySupplierAsync(
            model.SupplierId!.Value,
            cancellationToken);

        if (pending.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El proveedor seleccionado no tiene cuentas por pagar pendientes.");
            await PopulateStartOptionsAsync(model, cancellationToken);
            return View(model);
        }

        RegisterSupplierPaymentAllocateViewModel allocateModel = new()
        {
            SupplierId = model.SupplierId.Value,
            SupplierName = pending[0].SupplierName,
            CashAccountId = model.CashAccountId!.Value,
            Reference = model.Reference,
            PaymentDate = model.PaymentDate,
            Allocations = ProposeAllocation(pending, model.Amount),
        };

        await PopulateAllocateLabelsAsync(allocateModel, cancellationToken);
        return View("Allocate", allocateModel);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("aplicar")]
    public async Task<IActionResult> Register(
        RegisterSupplierPaymentAllocateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        List<SupplierPaymentAllocationCommand> allocations = model.Allocations
            .Where(line => line.Amount > 0)
            .Select(line => new SupplierPaymentAllocationCommand(line.PayableId, line.Amount))
            .ToList();

        if (allocations.Count == 0)
        {
            TempData["ErrorMessage"] = "Debe indicar un monto mayor que cero en al menos una cuenta por pagar.";
            return RedirectToAction(nameof(Start), new { proveedorId = model.SupplierId });
        }

        bool actorIsManagement = User.IsInRole(SystemRoles.Management);
        RegisterSupplierPaymentResult result = await paymentService.RegisterAsync(
            new RegisterSupplierPaymentCommand(
                model.SupplierId,
                model.CashAccountId,
                model.Reference,
                model.PaymentDate,
                allocations),
            actorId,
            actorIsManagement,
            cancellationToken);

        if (!result.Operation.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Operation.Message ?? "No fue posible registrar el pago.";
            return RedirectToAction(nameof(Start), new { proveedorId = model.SupplierId });
        }

        SetSuccessMessage(result.DuplicateReferenceWarning
            ? "El pago fue registrado. Advertencia: la referencia ya se había usado antes en esta cuenta de efectivo."
            : "El pago fue registrado.");

        Guid firstPayableId = model.Allocations.First(line => line.Amount > 0).PayableId;
        return RedirectToAction("Details", "Payables", new { id = firstPayableId });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        SupplierPaymentModel? payment = await paymentService.GetAsync(id, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        return View(new SupplierPaymentCancelViewModel
        {
            ReferenceLabel = payment.Reference,
            Amount = payment.Amount,
            Version = payment.Version,
        });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        SupplierPaymentCancelViewModel model,
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

        CatalogOperationResult result = await paymentService.CancelAsync(
            id,
            model.Reason,
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SupplierPaymentModel? payment = await paymentService.GetAsync(id, cancellationToken);
            SetSuccessMessage("El pago fue anulado.");
            return RedirectToAction("Details", "Payables", new { id = payment?.PayableId });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            SupplierPaymentModel? latest = await paymentService.GetAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private async Task PopulateStartOptionsAsync(
        RegisterSupplierPaymentStartViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SupplierOption> suppliers = await supplierService.GetActiveOptionsAsync(cancellationToken);
        model.SupplierOptions = suppliers
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.SupplierId))
            .ToList();

        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CashAccountId))
            .ToList();
    }

    private async Task PopulateAllocateLabelsAsync(
        RegisterSupplierPaymentAllocateViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountLabel = cashAccounts
            .Where(option => option.Id == model.CashAccountId)
            .Select(option => $"{option.Code} - {option.Name}")
            .SingleOrDefault() ?? "—";
    }

    private static List<SupplierPaymentAllocationLineViewModel> ProposeAllocation(
        IReadOnlyList<PayableModel> pending,
        decimal totalAmount)
    {
        List<SupplierPaymentAllocationLineViewModel> lines = [];
        decimal remaining = totalAmount;

        foreach (PayableModel payable in pending)
        {
            decimal proposed = remaining > 0 ? Math.Min(payable.Balance, remaining) : 0m;
            remaining -= proposed;

            lines.Add(new SupplierPaymentAllocationLineViewModel
            {
                PayableId = payable.Id,
                ExpenseLabel = payable.ExpenseCode,
                DueDate = payable.DueDate,
                Balance = payable.Balance,
                Amount = proposed,
            });
        }

        return lines;
    }
}
