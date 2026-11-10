using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.IncomeTax;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Web.Models.IncomeTax;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Listado, registro y anulación de los pagos de impuesto sobre la renta hechos a Hacienda (RF-99),
/// dentro del módulo Impuestos y retenciones, junto a la liquidación del IVA.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IIncomeTaxService</c> para todo, <c>ILedgerAccountService</c> para el selector
/// de cuentas de efectivo e <c>ISystemParametersService</c> para el período fiscal vigente. Registrar
/// exige <c>ConfirmFinancialTransactions</c> (Finanzas y Gerencia; sobre el límite, solo Gerencia lo
/// valida el servicio); anular exige <c>VoidFinancialTransactions</c> (solo Gerencia). Mismo esquema
/// de permisos que <c>VatSettlementsController</c>.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("pagos-renta")]
public sealed class IncomeTaxPaymentsController(
    IIncomeTaxService service,
    ILedgerAccountService ledgerAccountService,
    ISystemParametersService parametersService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? code,
        DateOnly? fiscalPeriodStartDate,
        DateOnly? paymentDateFrom,
        DateOnly? paymentDateTo,
        Guid? cashAccountId,
        IncomeTaxPaymentStatusFilter status = IncomeTaxPaymentStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        IncomeTaxPaymentQuery query = new(
            code,
            fiscalPeriodStartDate,
            paymentDateFrom,
            paymentDateTo,
            cashAccountId,
            Enum.IsDefined(status) ? status : IncomeTaxPaymentStatusFilter.All,
            page);

        PagedResult<IncomeTaxPaymentModel> results = await service.SearchPaymentsAsync(query, cancellationToken);

        return View(new IncomeTaxPaymentIndexViewModel(
            results,
            query,
            await GetCashAccountOptionsAsync(cashAccountId, cancellationToken),
            await GetFiscalPeriodOptionsAsync(fiscalPeriodStartDate, cancellationToken)));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        IncomeTaxPaymentModel? payment = await service.GetPaymentAsync(id, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        bool canCancel = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;

        return View(new IncomeTaxPaymentDetailsViewModel(
            payment,
            canCancel && payment.Status == IncomeTaxPaymentStatus.Active));
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("nuevo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        CreateIncomeTaxPaymentViewModel model = new();
        await PopulateOptionsAsync(model, cancellationToken);
        model.FiscalPeriodStartDate ??= await GetCurrentFiscalPeriodStartAsync(cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("nuevo")]
    public async Task<IActionResult> Create(
        CreateIncomeTaxPaymentViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        RegisterIncomeTaxPaymentResult result = await service.RegisterPaymentAsync(
            new RegisterIncomeTaxPaymentCommand(
                model.FiscalPeriodStartDate!.Value,
                model.CashAccountId!.Value,
                model.PaymentMethod,
                model.Amount,
                model.Reference,
                model.Description,
                model.PaymentDate),
            actorId,
            User.IsInRole(SystemRoles.Management),
            cancellationToken);

        if (!result.Operation.IsSuccess)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return FormFailure(result.Operation, model, nameof(Create));
        }

        SetSuccessMessage(result.DuplicateReferenceWarning
            ? "El pago fue registrado. Advertencia: la referencia ya se había usado antes en esta cuenta de efectivo."
            : "El pago fue registrado.");

        return RedirectToAction(nameof(Details), new { id = result.PaymentId });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        IncomeTaxPaymentModel? payment = await service.GetPaymentAsync(id, cancellationToken);

        if (payment is null)
        {
            return NotFound();
        }

        return View(new IncomeTaxPaymentCancelViewModel
        {
            CodeLabel = payment.Code,
            Amount = payment.Amount,
            Version = payment.Version,
        });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        IncomeTaxPaymentCancelViewModel model,
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

        CatalogOperationResult result = await service.CancelPaymentAsync(
            id,
            model.Reason,
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El pago fue anulado.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            IncomeTaxPaymentModel? latest = await service.GetPaymentAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private async Task PopulateOptionsAsync(CreateIncomeTaxPaymentViewModel model, CancellationToken cancellationToken)
    {
        model.CashAccountOptions = await GetCashAccountOptionsAsync(model.CashAccountId, cancellationToken);
        model.FiscalPeriodOptions = await GetFiscalPeriodOptionsAsync(model.FiscalPeriodStartDate, cancellationToken);
    }

    private async Task<IReadOnlyList<SelectListItem>> GetCashAccountOptionsAsync(
        Guid? selectedId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);

        return cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == selectedId))
            .ToList();
    }

    /// <summary>
    /// El período fiscal vigente y el anterior: el pago de la declaración de un período se hace cuando
    /// ese período ya terminó.
    /// </summary>
    private async Task<IReadOnlyList<SelectListItem>> GetFiscalPeriodOptionsAsync(
        DateOnly? selected,
        CancellationToken cancellationToken)
    {
        DateOnly current = await GetCurrentFiscalPeriodStartAsync(cancellationToken);
        DateOnly[] periods = [current, current.AddYears(-1)];

        if (selected is DateOnly selectedPeriod && !periods.Contains(selectedPeriod))
        {
            periods = [.. periods, selectedPeriod];
        }

        DateOnly effectiveSelection = selected ?? current;

        return periods
            .Select(start => new SelectListItem(
                start.ToFiscalPeriodLabel(),
                start.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                start == effectiveSelection))
            .ToList();
    }

    private async Task<DateOnly> GetCurrentFiscalPeriodStartAsync(CancellationToken cancellationToken)
    {
        SystemParametersModel? parameters = await parametersService.GetAsync(cancellationToken);
        return parameters?.FiscalPeriodStartDate ?? new DateOnly(DateTime.Now.Year, 1, 1);
    }
}
