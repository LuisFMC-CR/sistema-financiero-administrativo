using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Accounts;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta cajas y cuentas bancarias con moneda fija.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/cuentas-financieras")]
public sealed class FinancialAccountsController(IFinancialAccountService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<FinancialAccountModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<FinancialAccountModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new FinancialAccountCreateViewModel());
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        FinancialAccountCreateViewModel model,
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

        CatalogOperationResult result = await service.CreateAsync(
            new CreateFinancialAccountCommand(
                model.Code,
                model.Name,
                model.Type,
                model.Currency,
                model.Reference),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La cuenta financiera fue creada correctamente.");
            return RedirectToAction(nameof(Index));
        }

        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        FinancialAccountModel? account = await service.GetAsync(id, cancellationToken);
        return account is null ? NotFound() : View(ToEditModel(account));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        FinancialAccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            FinancialAccountModel? invalidCurrent = await service.GetAsync(id, cancellationToken);
            if (invalidCurrent is null)
            {
                return NotFound();
            }

            RestoreFixedData(model, invalidCurrent, replaceVersion: false);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateAsync(
            id,
            new UpdateFinancialAccountCommand(model.Code, model.Name, model.Reference),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La cuenta financiera fue actualizada correctamente.");
            return RedirectToAction(nameof(Index));
        }

        FinancialAccountModel? current = await service.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }

        bool replaceVersion = result.Status == CatalogOperationStatus.ConcurrencyConflict;
        RestoreFixedData(model, current, replaceVersion);
        if (replaceVersion)
        {
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Edit));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("estado/{id:guid}")]
    public async Task<IActionResult> SetActive(
        Guid id,
        CatalogStatusViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "No fue posible validar el estado solicitado.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.SetActiveAsync(
            id,
            model.IsActive,
            model.Version,
            actorId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return StateFailure(result, "FinancialAccounts");
        }

        SetSuccessMessage(model.IsActive ? "La cuenta fue reactivada." : "La cuenta fue desactivada.");
        return RedirectToAction(nameof(Index));
    }

    private static FinancialAccountEditViewModel ToEditModel(FinancialAccountModel account)
    {
        return new FinancialAccountEditViewModel
        {
            Code = account.Code,
            Name = account.Name,
            Type = account.Type,
            Currency = account.Currency,
            Reference = account.Reference,
            Version = account.Version,
        };
    }

    private static void RestoreFixedData(
        FinancialAccountEditViewModel model,
        FinancialAccountModel current,
        bool replaceVersion)
    {
        model.Type = current.Type;
        model.Currency = current.Currency;
        if (replaceVersion)
        {
            model.Version = current.Version;
        }
    }
}
