using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Web.Models.Incomes;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el listado de ingresos y el registro y la anulación de los manuales.</summary>
/// <remarks>
/// Conexiones: usa <c>IIncomeService</c> para todo. Los ingresos automáticos (factura de contado,
/// abono) solo se consultan aquí; se anulan desde su factura o su abono (regla 4). Solo Gerencia y
/// Finanzas y Asistente ven esta pantalla (política <c>Movimientos.Ver</c>); el ingreso manual requiere
/// <c>Ingresos.RegistrarManual</c> y solo Gerencia puede anularlo.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialTransactions)]
[Route("ingresos")]
public sealed class IncomesController(
    IIncomeService service,
    ILedgerAccountService ledgerAccountService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateOnly? incomeDateFrom,
        DateOnly? incomeDateTo,
        IncomeOriginFilter origin = IncomeOriginFilter.All,
        string? accountName = null,
        string? cashAccountName = null,
        string? referenceOrDescription = null,
        decimal? amountMin = null,
        decimal? amountMax = null,
        IncomeStatusFilter status = IncomeStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        IncomeQuery query = new(
            incomeDateFrom,
            incomeDateTo,
            NormalizeOrigin(origin),
            NormalizeSearch(accountName),
            NormalizeSearch(cashAccountName),
            NormalizeSearch(referenceOrDescription),
            amountMin,
            amountMax,
            NormalizeStatus(status),
            page);
        PagedResult<IncomeModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new IncomeIndexViewModel(results, query));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        IncomeModel? income = await service.GetAsync(id, cancellationToken);

        if (income is null)
        {
            return NotFound();
        }

        bool canCancel = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;

        return View(new IncomeDetailsViewModel(income, canCancel && income.Origin == IncomeOrigin.Manual
            && income.Status == IncomeStatus.Active));
    }

    [Authorize(Policy = SystemPolicies.RegisterManualIncome)]
    [HttpGet("nuevo")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        CreateManualIncomeViewModel model = new();
        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.RegisterManualIncome)]
    [HttpPost("nuevo")]
    public async Task<IActionResult> Create(
        CreateManualIncomeViewModel model,
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

        IncomeCreationResult result = await service.CreateManualAsync(
            new CreateManualIncomeCommand(
                model.AccountId!.Value,
                model.CashAccountId!.Value,
                model.Amount,
                model.IncomeDate,
                model.PaymentMethod,
                model.Reference,
                model.Description),
            actorId,
            cancellationToken);

        if (result.Operation.IsSuccess)
        {
            SetSuccessMessage(result.DuplicateReferenceWarning
                ? "El ingreso fue registrado. Advertencia: la referencia ya se había usado antes en esta cuenta de efectivo."
                : "El ingreso fue registrado.");
            return RedirectToAction(nameof(Index));
        }

        await PopulateOptionsAsync(model, cancellationToken);
        return FormFailure(result.Operation, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        IncomeModel? income = await service.GetAsync(id, cancellationToken);

        if (income is null)
        {
            return NotFound();
        }

        if (income.Origin != IncomeOrigin.Manual)
        {
            TempData["ErrorMessage"] = "Este ingreso se generó automáticamente; anúlelo desde la factura o el abono que lo originó.";
            return RedirectToAction(nameof(Index));
        }

        return View(new IncomeCancelViewModel
        {
            Description = income.Description ?? string.Empty,
            Amount = income.Amount,
            Version = income.Version,
        });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        IncomeCancelViewModel model,
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

        CatalogOperationResult result = await service.CancelAsync(id, model.Reason, model.Version, actorId, cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El ingreso fue anulado.");
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            IncomeModel? latest = await service.GetAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private async Task PopulateOptionsAsync(CreateManualIncomeViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CashAccountId))
            .ToList();

        IReadOnlyList<LedgerAccountOption> categories = await ledgerAccountService.GetActiveOptionsAsync(
            LedgerAccountType.Income,
            cancellationToken: cancellationToken);
        model.CategoryOptions = categories
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.AccountId))
            .ToList();
    }

    private static IncomeStatusFilter NormalizeStatus(IncomeStatusFilter status) =>
        Enum.IsDefined(status) ? status : IncomeStatusFilter.Active;

    private static IncomeOriginFilter NormalizeOrigin(IncomeOriginFilter origin) =>
        Enum.IsDefined(origin) ? origin : IncomeOriginFilter.All;
}
