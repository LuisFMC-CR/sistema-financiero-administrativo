using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.VatSettlements;
using SistemaFinanciero.Web.Models.VatSettlements;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta el listado, la vista previa, el registro y la anulación de las liquidaciones mensuales
/// del IVA (paquete v8, bloque 2), dentro del módulo Impuestos y retenciones.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IVatSettlementService</c> para todo e <c>ILedgerAccountService</c> para el
/// selector de cuentas de efectivo. Registrar exige <c>ConfirmFinancialTransactions</c> (Finanzas y
/// Gerencia); anular exige <c>VoidFinancialTransactions</c> (solo Gerencia, y solo la liquidación
/// vigente más reciente). Ruta propia (<c>/liquidacion-iva</c>, como Traspasos), fuera de
/// <c>/impuestos-y-retenciones</c> para no compartir su prefijo de rutas.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("liquidacion-iva")]
public sealed class VatSettlementsController(
    IVatSettlementService service,
    ILedgerAccountService ledgerAccountService,
    IAuthorizationService authorizationService,
    TimeProvider timeProvider) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        int? periodYear,
        VatSettlementStatusFilter status = VatSettlementStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        VatSettlementQuery query = new(periodYear, NormalizeStatus(status), page);
        PagedResult<VatSettlementModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new VatSettlementIndexViewModel(results, query));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        VatSettlementModel? settlement = await service.GetAsync(id, cancellationToken);

        if (settlement is null)
        {
            return NotFound();
        }

        bool canCancel = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;

        return View(new VatSettlementDetailsViewModel(
            settlement,
            canCancel && settlement.Status == VatSettlementStatus.Active));
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("nueva")]
    public async Task<IActionResult> Create(
        int? periodYear,
        int? periodMonth,
        CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        RegisterVatSettlementViewModel model = new()
        {
            PeriodYear = periodYear ?? today.Year,
            PeriodMonth = periodMonth ?? today.Month,
        };

        if (periodYear is int year && periodMonth is int month)
        {
            model.Preview = await service.PreviewAsync(year, month, cancellationToken);
            model.SettlementDate = DefaultSettlementDate(year, month, today);
        }

        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("nueva")]
    public async Task<IActionResult> Register(
        RegisterVatSettlementViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            if (model.PeriodYear is int year && model.PeriodMonth is int month)
            {
                model.Preview = await service.PreviewAsync(year, month, cancellationToken);
            }

            await PopulateOptionsAsync(model, cancellationToken);
            return View(nameof(Create), model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        bool actorIsManagement = User.IsInRole(SystemRoles.Management);
        RegisterVatSettlementResult result = await service.RegisterAsync(
            new RegisterVatSettlementCommand(
                model.PeriodYear!.Value,
                model.PeriodMonth!.Value,
                model.SettlementDate!.Value,
                model.CashAccountId,
                model.Reference),
            actorId,
            actorIsManagement,
            cancellationToken);

        if (!result.Operation.IsSuccess)
        {
            model.Preview = await service.PreviewAsync(model.PeriodYear.Value, model.PeriodMonth.Value, cancellationToken);
            await PopulateOptionsAsync(model, cancellationToken);
            return FormFailure(result.Operation, model, nameof(Create));
        }

        SetSuccessMessage(result.OpenPeriodWarning
            ? "La liquidación fue registrada. Advertencia: el período aún tiene días sin cerrar; lo que se "
                + "registre después con fecha de este período pasará a la liquidación siguiente."
            : "La liquidación fue registrada.");

        return RedirectToAction(nameof(Details), new { id = result.VatSettlementId });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        VatSettlementModel? settlement = await service.GetAsync(id, cancellationToken);

        if (settlement is null)
        {
            return NotFound();
        }

        return View(new VatSettlementCancelViewModel
        {
            CodeLabel = settlement.Code,
            PaymentAmount = settlement.PaymentAmount,
            Version = settlement.Version,
        });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        VatSettlementCancelViewModel model,
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

        CatalogOperationResult result = await service.CancelAsync(
            id,
            model.Reason,
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La liquidación fue anulada.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            VatSettlementModel? latest = await service.GetAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private async Task PopulateOptionsAsync(RegisterVatSettlementViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CashAccountId))
            .ToList();
    }

    private static DateOnly DefaultSettlementDate(int periodYear, int periodMonth, DateOnly today)
    {
        DateOnly cutoff = new DateOnly(periodYear, periodMonth, 1).AddMonths(1).AddDays(-1);
        DateOnly earliestValid = cutoff.AddDays(1);
        return today > earliestValid ? today : earliestValid;
    }

    private static VatSettlementStatusFilter NormalizeStatus(VatSettlementStatusFilter status) =>
        Enum.IsDefined(status) ? status : VatSettlementStatusFilter.All;
}
