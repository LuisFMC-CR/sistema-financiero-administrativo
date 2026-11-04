using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Receivables;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Registra y anula abonos, incluido el reparto a varias facturas del mismo cliente.</summary>
/// <remarks>
/// Conexiones: usa <c>IReceivableService</c> para proponer el reparto (de la más antigua a la más
/// nueva, regla 2) e <c>IReceivablePaymentService</c> para registrarlo o anularlo.
/// <c>ICustomerCatalogService</c>, <c>ILedgerAccountService</c> e <c>IWithholdingTypeService</c>
/// alimentan los selectores. El registro es un asistente de dos pasos:
/// <see cref="Start(Guid?, CancellationToken)"/> pide cliente, monto, cuenta de efectivo, referencia y
/// fecha; <see cref="Start(RegisterReceivablePaymentStartViewModel, CancellationToken)"/> calcula el
/// reparto propuesto y lo muestra editable; <see cref="Register"/> lo registra. Ya no pide la
/// categoría del ingreso: cada ingreso hereda la de su propia factura (bloque 2 del paquete de
/// mejoras finales), y un mismo abono a varias facturas puede heredar categorías distintas. Si el
/// registro falla, se regresa al paso 1 en vez de reconstruir el paso 2 con los datos ya editados,
/// para mantener el flujo simple.
/// </remarks>
[Route("cuentas-por-cobrar/abonos")]
public sealed class ReceivablePaymentsController(
    IReceivableService receivableService,
    IReceivablePaymentService paymentService,
    ICustomerCatalogService customerService,
    ILedgerAccountService ledgerAccountService,
    IWithholdingTypeService withholdingTypeService) : CatalogControllerBase
{
    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("nuevo")]
    public async Task<IActionResult> Start(Guid? clienteId, CancellationToken cancellationToken)
    {
        RegisterReceivablePaymentStartViewModel model = new() { CustomerId = clienteId };
        await PopulateStartOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("nuevo")]
    public async Task<IActionResult> Start(
        RegisterReceivablePaymentStartViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateStartOptionsAsync(model, cancellationToken);
            return View(model);
        }

        IReadOnlyList<ReceivableModel> pending = await receivableService.GetPendingByCustomerAsync(
            model.CustomerId!.Value,
            cancellationToken);

        if (pending.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "El cliente seleccionado no tiene cuentas por cobrar pendientes.");
            await PopulateStartOptionsAsync(model, cancellationToken);
            return View(model);
        }

        RegisterReceivablePaymentAllocateViewModel allocateModel = new()
        {
            CustomerId = model.CustomerId.Value,
            CustomerName = pending[0].CustomerName,
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
        RegisterReceivablePaymentAllocateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        List<ReceivablePaymentAllocationCommand> allocations = model.Allocations
            .Where(line => line.Amount > 0)
            .Select(line => new ReceivablePaymentAllocationCommand(
                line.ReceivableId,
                line.Amount,
                line.WithholdingTypeId is Guid withholdingTypeId
                    ? new WithholdingInputCommand(withholdingTypeId, line.WithholdingBase ?? 0m)
                    : null))
            .ToList();

        if (allocations.Count == 0)
        {
            TempData["ErrorMessage"] = "Debe indicar un monto mayor que cero en al menos una factura.";
            return RedirectToAction(nameof(Start), new { clienteId = model.CustomerId });
        }

        bool actorIsManagement = User.IsInRole(SystemRoles.Management);
        RegisterReceivablePaymentResult result = await paymentService.RegisterAsync(
            new RegisterReceivablePaymentCommand(
                model.CustomerId,
                model.CashAccountId,
                model.Reference,
                model.PaymentDate,
                allocations),
            actorId,
            actorIsManagement,
            cancellationToken);

        if (!result.Operation.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Operation.Message ?? "No fue posible registrar el abono.";
            return RedirectToAction(nameof(Start), new { clienteId = model.CustomerId });
        }

        SetSuccessMessage(result.DuplicateReferenceWarning
            ? "El abono fue registrado. Advertencia: la referencia ya se había usado antes en esta cuenta de efectivo."
            : "El abono fue registrado.");

        Guid firstReceivableId = model.Allocations.First(line => line.Amount > 0).ReceivableId;
        return RedirectToAction("Details", "Receivables", new { id = firstReceivableId });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        ReceivablePaymentModel? payment = await paymentService.GetAsync(id, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        return View(new ReceivablePaymentCancelViewModel
        {
            InvoiceLabel = payment.Reference,
            Amount = payment.Amount,
            Version = payment.Version,
        });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        ReceivablePaymentCancelViewModel model,
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
            ReceivablePaymentModel? payment = await paymentService.GetAsync(id, cancellationToken);
            SetSuccessMessage("El abono fue anulado.");
            return RedirectToAction("Details", "Receivables", new { id = payment?.ReceivableId });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            ReceivablePaymentModel? latest = await paymentService.GetAsync(id, cancellationToken);
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
        RegisterReceivablePaymentStartViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomerOption> customers = await customerService.GetActiveOptionsAsync(cancellationToken);
        model.CustomerOptions = customers
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CustomerId))
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
        RegisterReceivablePaymentAllocateViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountLabel = cashAccounts
            .Where(option => option.Id == model.CashAccountId)
            .Select(option => $"{option.Code} - {option.Name}")
            .SingleOrDefault() ?? "—";

        // RF-95 (bloque 1 del paquete v7): la retención solo se ofrece a clientes con la marca de
        // retención del sector público; si no la tiene, el formulario no muestra ninguna opción.
        CustomerModel? customer = await customerService.GetAsync(model.CustomerId, cancellationToken);

        if (customer?.AppliesStateWithholding != true)
        {
            model.WithholdingTypeOptions = [];
            return;
        }

        IReadOnlyList<WithholdingTypeOption> withholdingTypes = await withholdingTypeService.GetActiveOptionsAsync(cancellationToken);
        model.WithholdingTypeOptions = withholdingTypes
            .Select(option => new SelectListItem($"{option.Name} ({option.Rate:0.##}%)", option.Id.ToString()))
            .ToList();
    }

    private static List<ReceivablePaymentAllocationLineViewModel> ProposeAllocation(
        IReadOnlyList<ReceivableModel> pending,
        decimal totalAmount)
    {
        List<ReceivablePaymentAllocationLineViewModel> lines = [];
        decimal remaining = totalAmount;

        foreach (ReceivableModel receivable in pending)
        {
            decimal proposed = remaining > 0 ? Math.Min(receivable.Balance, remaining) : 0m;
            remaining -= proposed;

            lines.Add(new ReceivablePaymentAllocationLineViewModel
            {
                ReceivableId = receivable.Id,
                InvoiceLabel = receivable.InvoiceNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "—",
                DueDate = receivable.DueDate,
                Balance = receivable.Balance,
                InvoiceNetAmount = receivable.InvoiceNetAmount,
                Amount = proposed,
                WithholdingBase = ProposeWithholdingBase(receivable, proposed),
            });
        }

        return lines;
    }

    /// <summary>
    /// Propone la base de la retención del 2 % del sector público (regla 3, RF-95):
    /// <c>base = neto × monto aplicado / (total − 2 % × neto)</c>, sin superar el neto pendiente de
    /// retención de la factura, aproximado por la proporción de saldo que queda sobre su monto
    /// original. El usuario puede ajustarla dentro de ese límite; el servidor valida el límite real al
    /// registrar el abono.
    /// </summary>
    private static decimal ProposeWithholdingBase(ReceivableModel receivable, decimal amountApplied)
    {
        if (amountApplied <= 0 || receivable.OriginalAmount <= 0)
        {
            return 0m;
        }

        decimal denominator = receivable.OriginalAmount - (0.02m * receivable.InvoiceNetAmount);

        if (denominator <= 0)
        {
            return 0m;
        }

        decimal proposedBase = Math.Round(
            receivable.InvoiceNetAmount * amountApplied / denominator,
            2,
            MidpointRounding.AwayFromZero);

        decimal pendingNetAmount = receivable.Balance / receivable.OriginalAmount * receivable.InvoiceNetAmount;

        return Math.Max(0m, Math.Min(proposedBase, pendingNetAmount));
    }
}
