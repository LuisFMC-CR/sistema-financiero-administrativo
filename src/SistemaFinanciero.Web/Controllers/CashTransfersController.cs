using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Web.Models.CashTransfers;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta el listado, el registro y la anulación de traspasos entre cuentas de efectivo propias
/// (paquete v8, bloque 1). En el menú vive junto a "Catálogo contable" (módulo Gestionar Cuentas
/// Contables); la ruta es propia (<c>/traspasos</c>, como Ingresos o Gastos) para no compartir el
/// prefijo de <c>LedgerAccountsController</c> ni sus pruebas de autorización por ruta.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>ICashTransferService</c> para todo e <c>ILedgerAccountService</c> para los
/// selectores de cuentas de efectivo y el saldo informativo de la cuenta de origen (según los
/// asientos; no bloquea por saldo insuficiente, regla del bloque 1). Registrar exige
/// <c>ConfirmFinancialTransactions</c> (Finanzas y Gerencia); anular exige
/// <c>VoidFinancialTransactions</c> (solo Gerencia).
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("traspasos")]
public sealed class CashTransfersController(
    ICashTransferService service,
    ILedgerAccountService ledgerAccountService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? code,
        DateOnly? transferDateFrom,
        DateOnly? transferDateTo,
        Guid? cashAccountId,
        CashTransferStatusFilter status = CashTransferStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CashTransferQuery query = new(
            code,
            transferDateFrom,
            transferDateTo,
            cashAccountId,
            NormalizeStatus(status),
            page);

        PagedResult<CashTransferModel> results = await service.SearchAsync(query, cancellationToken);

        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        List<SelectListItem> cashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == cashAccountId))
            .ToList();

        return View(new CashTransferIndexViewModel(results, query, cashAccountOptions));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        CashTransferModel? transfer = await service.GetAsync(id, cancellationToken);

        if (transfer is null)
        {
            return NotFound();
        }

        bool canCancel = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;

        return View(new CashTransferDetailsViewModel(
            transfer,
            canCancel && transfer.Status == CashTransferStatus.Active));
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("nuevo")]
    public async Task<IActionResult> Create(Guid? sourceCashAccountId, CancellationToken cancellationToken)
    {
        CreateCashTransferViewModel model = new() { SourceCashAccountId = sourceCashAccountId };
        await PopulateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("nuevo")]
    public async Task<IActionResult> Create(
        CreateCashTransferViewModel model,
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

        bool actorIsManagement = User.IsInRole(SystemRoles.Management);
        RegisterCashTransferResult result = await service.RegisterAsync(
            new RegisterCashTransferCommand(
                model.SourceCashAccountId!.Value,
                model.DestinationCashAccountId!.Value,
                model.Amount,
                model.Reference,
                model.Description,
                model.TransferDate),
            actorId,
            actorIsManagement,
            cancellationToken);

        if (!result.Operation.IsSuccess)
        {
            await PopulateOptionsAsync(model, cancellationToken);
            return FormFailure(result.Operation, model, nameof(Create));
        }

        SetSuccessMessage(result.DuplicateReferenceWarning
            ? "El traspaso fue registrado. Advertencia: la referencia ya se había usado antes en una de las dos cuentas de efectivo."
            : "El traspaso fue registrado.");

        return RedirectToAction(nameof(Details), new { id = result.CashTransferId });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        CashTransferModel? transfer = await service.GetAsync(id, cancellationToken);

        if (transfer is null)
        {
            return NotFound();
        }

        return View(new CashTransferCancelViewModel
        {
            ReferenceLabel = transfer.Reference,
            Amount = transfer.Amount,
            Version = transfer.Version,
        });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        CashTransferCancelViewModel model,
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
            SetSuccessMessage("El traspaso fue anulado.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            CashTransferModel? latest = await service.GetAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private async Task PopulateOptionsAsync(CreateCashTransferViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);

        model.SourceCashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.SourceCashAccountId))
            .ToList();

        model.DestinationCashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.DestinationCashAccountId))
            .ToList();

        if (model.SourceCashAccountId is Guid sourceCashAccountId)
        {
            LedgerAccountModel? sourceAccount = await ledgerAccountService.GetAsync(sourceCashAccountId, cancellationToken);
            model.SourceCashAccountBalance = sourceAccount?.Balance;
        }
    }

    private static CashTransferStatusFilter NormalizeStatus(CashTransferStatusFilter status) =>
        Enum.IsDefined(status) ? status : CashTransferStatusFilter.All;
}
