using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta categorías financieras jerárquicas de ingreso y gasto.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/categorias-financieras")]
public sealed class FinancialCategoriesController(
    IFinancialCategoryService service,
    ILedgerAccountService ledgerAccounts) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<FinancialCategoryModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<FinancialCategoryModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        FinancialCategoryInputViewModel model = new();
        await PopulateOptionsAsync(model, null, null, null, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        FinancialCategoryInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, null, null, null, cancellationToken);
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
            SetSuccessMessage("La categoría financiera fue creada correctamente.");
            return RedirectToAction(nameof(Index));
        }

        await PopulateOptionsAsync(model, null, null, null, cancellationToken);
        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        FinancialCategoryModel? category = await service.GetAsync(id, cancellationToken);
        if (category is null)
        {
            return NotFound();
        }

        FinancialCategoryEditViewModel model = ToEditModel(category);
        await PopulateOptionsAsync(model, id, category.ParentName, LedgerAccountLabel(category), cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        FinancialCategoryEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(model, id, null, null, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateAsync(
            id,
            ToCommand(model),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La categoría financiera fue actualizada correctamente.");
            return RedirectToAction(nameof(Index));
        }

        string? selectedParentName = null;
        string? selectedLedgerAccountLabel = null;
        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            FinancialCategoryModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.Version = current.Version;
            selectedParentName = current.ParentId == model.ParentId ? current.ParentName : null;
            selectedLedgerAccountLabel = current.LedgerAccountId == model.LedgerAccountId
                ? LedgerAccountLabel(current)
                : null;
            ModelState.Remove(nameof(model.Version));
        }

        await PopulateOptionsAsync(model, id, selectedParentName, selectedLedgerAccountLabel, cancellationToken);
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
            return StateFailure(result, "FinancialCategories");
        }

        SetSuccessMessage(model.IsActive ? "La categoría fue reactivada." : "La categoría fue desactivada.");
        return RedirectToAction(nameof(Index));
    }

    private static SaveFinancialCategoryCommand ToCommand(FinancialCategoryInputViewModel model)
    {
        // La cuenta es obligatoria en el formulario; Guid.Empty solo se usaría si esa validación se omitiera
        // y el servicio la rechaza igualmente.
        return new(
            model.Code,
            model.Name,
            model.Kind,
            model.ParentId,
            model.LedgerAccountId ?? Guid.Empty,
            model.Description);
    }

    private static string LedgerAccountLabel(FinancialCategoryModel category) =>
        $"{category.LedgerAccountCode} - {category.LedgerAccountName}";

    private static FinancialCategoryEditViewModel ToEditModel(FinancialCategoryModel category)
    {
        return new FinancialCategoryEditViewModel
        {
            Code = category.Code,
            Name = category.Name,
            Kind = category.Kind,
            ParentId = category.ParentId,
            LedgerAccountId = category.LedgerAccountId,
            Description = category.Description,
            Version = category.Version,
        };
    }

    private async Task PopulateOptionsAsync(
        FinancialCategoryInputViewModel model,
        Guid? excludedCategoryId,
        string? selectedParentName,
        string? selectedLedgerAccountLabel,
        CancellationToken cancellationToken)
    {
        await PopulateParentOptionsAsync(model, excludedCategoryId, selectedParentName, cancellationToken);
        await PopulateLedgerAccountOptionsAsync(model, selectedLedgerAccountLabel, cancellationToken);
    }

    private async Task PopulateLedgerAccountOptionsAsync(
        FinancialCategoryInputViewModel model,
        string? selectedLedgerAccountLabel,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<LedgerAccountOption> incomeAccounts = await ledgerAccounts.GetActiveOptionsAsync(
            LedgerAccountType.Income,
            cancellationToken);
        IReadOnlyList<LedgerAccountOption> expenseAccounts = await ledgerAccounts.GetActiveOptionsAsync(
            LedgerAccountType.Expense,
            cancellationToken);

        List<FinancialCategoryLedgerAccountOptionViewModel> options = incomeAccounts
            .Select(option => new FinancialCategoryLedgerAccountOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                FinancialCategoryKind.Income))
            .Concat(expenseAccounts.Select(option => new FinancialCategoryLedgerAccountOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                FinancialCategoryKind.Expense)))
            .ToList();

        // Si la cuenta guardada ya no está activa se muestra, para no cambiarla sin que el usuario lo decida;
        // el servicio exigirá elegir una activa al guardar.
        if (model.LedgerAccountId is Guid selectedId && options.All(option => option.Id != selectedId))
        {
            options.Add(new FinancialCategoryLedgerAccountOptionViewModel(
                selectedId,
                selectedLedgerAccountLabel ?? "Cuenta contable seleccionada no disponible",
                model.Kind));
        }

        model.LedgerAccountOptions = options;
    }

    private async Task PopulateParentOptionsAsync(
        FinancialCategoryInputViewModel model,
        Guid? excludedCategoryId,
        string? selectedParentName,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FinancialCategoryOption> incomeOptions = await service.GetActiveOptionsAsync(
            FinancialCategoryKind.Income,
            excludedCategoryId,
            cancellationToken);
        IReadOnlyList<FinancialCategoryOption> expenseOptions = await service.GetActiveOptionsAsync(
            FinancialCategoryKind.Expense,
            excludedCategoryId,
            cancellationToken);

        List<FinancialCategoryParentOptionViewModel> options = incomeOptions
            .Select(option => new FinancialCategoryParentOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                FinancialCategoryKind.Income))
            .Concat(expenseOptions.Select(option => new FinancialCategoryParentOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                FinancialCategoryKind.Expense)))
            .ToList();

        if (model.ParentId is Guid selectedId && options.All(option => option.Id != selectedId))
        {
            options.Add(new FinancialCategoryParentOptionViewModel(
                selectedId,
                selectedParentName ?? "Categoría seleccionada no disponible",
                model.Kind));
        }

        model.ParentOptions = options;
    }
}
