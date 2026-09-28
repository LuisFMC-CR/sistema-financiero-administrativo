using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el catálogo de cuentas contables, incluidas las cajas y los bancos.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/cuentas-contables")]
public sealed class LedgerAccountsController(ILedgerAccountService service) : CatalogControllerBase
{
    private static readonly LedgerAccountType[] AllTypes = Enum.GetValues<LedgerAccountType>();

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<LedgerAccountModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<LedgerAccountModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        LedgerAccountCreateViewModel model = new();
        await PopulateParentOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        LedgerAccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateParentOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.CreateAsync(
            ToCommand(model),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La cuenta contable fue creada correctamente.");
            return RedirectToAction(nameof(Index));
        }

        await PopulateParentOptionsAsync(model, cancellationToken);
        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        LedgerAccountModel? account = await service.GetAsync(id, cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        LedgerAccountEditViewModel model = ToEditModel(account);
        await PopulateParentOptionsAsync(model, id, account.ParentName, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        LedgerAccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        // Tipo, subtipo de efectivo y moneda son inmutables: nunca se toman del formulario.
        LedgerAccountModel? current = await service.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }

        RestoreFixedData(model, current);

        if (!ModelState.IsValid)
        {
            // El nombre solo se conoce si la cuenta superior elegida es la que ya tenía guardada.
            string? currentParentName = current.ParentId == model.ParentId ? current.ParentName : null;
            await PopulateParentOptionsAsync(model, id, currentParentName, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateAsync(
            id,
            new UpdateLedgerAccountCommand(
                model.Code,
                model.Name,
                model.ParentId,
                model.Reference,
                model.Description),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La cuenta contable fue actualizada correctamente.");
            return RedirectToAction(nameof(Index));
        }

        LedgerAccountModel? latest = await service.GetAsync(id, cancellationToken);
        if (latest is null)
        {
            return NotFound();
        }

        RestoreFixedData(model, latest);
        string? selectedParentName = latest.ParentId == model.ParentId ? latest.ParentName : null;

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        await PopulateParentOptionsAsync(model, id, selectedParentName, cancellationToken);
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
            return StateFailure(result, "LedgerAccounts");
        }

        SetSuccessMessage(model.IsActive ? "La cuenta contable fue reactivada." : "La cuenta contable fue desactivada.");
        return RedirectToAction(nameof(Index));
    }

    private static CreateLedgerAccountCommand ToCommand(LedgerAccountCreateViewModel model)
    {
        // El subtipo y la moneda solo cuentan cuando la cuenta es de efectivo; si no, se descartan.
        return new CreateLedgerAccountCommand(
            model.Code,
            model.Name,
            model.Type,
            model.ParentId,
            model.IsCash ? model.CashKind : null,
            model.IsCash ? model.Currency : null,
            model.Reference,
            model.Description);
    }

    private static LedgerAccountEditViewModel ToEditModel(LedgerAccountModel account)
    {
        return new LedgerAccountEditViewModel
        {
            Code = account.Code,
            Name = account.Name,
            ParentId = account.ParentId,
            Reference = account.Reference,
            Description = account.Description,
            Type = account.Type,
            CashKind = account.CashKind,
            Currency = account.Currency,
            Version = account.Version,
        };
    }

    private static void RestoreFixedData(LedgerAccountEditViewModel model, LedgerAccountModel current)
    {
        model.Type = current.Type;
        model.CashKind = current.CashKind;
        model.Currency = current.Currency;
    }

    private async Task PopulateParentOptionsAsync(
        LedgerAccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        List<LedgerAccountParentOptionViewModel> options = [];

        foreach (LedgerAccountType type in AllTypes)
        {
            IReadOnlyList<LedgerAccountOption> typeOptions = await service.GetParentOptionsAsync(
                type,
                excludedAccountId: null,
                cancellationToken);
            options.AddRange(typeOptions.Select(option => new LedgerAccountParentOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                type)));
        }

        model.ParentOptions = options;
    }

    private async Task PopulateParentOptionsAsync(
        LedgerAccountEditViewModel model,
        Guid accountId,
        string? selectedParentName,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerAccountOption> typeOptions = await service.GetParentOptionsAsync(
            model.Type,
            accountId,
            cancellationToken);

        List<LedgerAccountParentOptionViewModel> options = typeOptions
            .Select(option => new LedgerAccountParentOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                model.Type))
            .ToList();

        // Si la cuenta superior actual ya no está disponible (por ejemplo, está inactiva), se muestra
        // para no cambiarla sin que el usuario lo decida.
        if (model.ParentId is Guid selectedId && options.All(option => option.Id != selectedId))
        {
            options.Add(new LedgerAccountParentOptionViewModel(
                selectedId,
                selectedParentName ?? "Cuenta superior seleccionada no disponible",
                model.Type));
        }

        model.ParentOptions = options;
    }
}
