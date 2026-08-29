using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el catálogo unificado de productos y servicios facturables.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/productos-servicios")]
public sealed class CatalogItemsController(
    ICatalogItemService service,
    IFinancialCategoryService categoryService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<CatalogItemModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<CatalogItemModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        CatalogItemInputViewModel model = new();
        await PopulateCategoryOptionsAsync(model, null, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        CatalogItemInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoryOptionsAsync(model, null, cancellationToken);
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
            SetSuccessMessage("El producto o servicio fue creado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        await PopulateCategoryOptionsAsync(model, null, cancellationToken);
        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        CatalogItemModel? item = await service.GetAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        CatalogItemEditViewModel model = ToEditModel(item);
        await PopulateCategoryOptionsAsync(model, item.DefaultIncomeCategoryName, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        CatalogItemEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoryOptionsAsync(model, null, cancellationToken);
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
            SetSuccessMessage("El producto o servicio fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        string? selectedCategoryName = null;
        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            CatalogItemModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.Version = current.Version;
            selectedCategoryName = current.DefaultIncomeCategoryId == model.DefaultIncomeCategoryId
                ? current.DefaultIncomeCategoryName
                : null;
            ModelState.Remove(nameof(model.Version));
        }

        await PopulateCategoryOptionsAsync(model, selectedCategoryName, cancellationToken);
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
            return StateFailure(result, "CatalogItems");
        }

        SetSuccessMessage(model.IsActive
            ? "El producto o servicio fue reactivado."
            : "El producto o servicio fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    private static SaveCatalogItemCommand ToCommand(CatalogItemInputViewModel model)
    {
        return new(
            model.Code,
            model.Name,
            model.Type,
            model.Description,
            model.UnitOfMeasure,
            model.ReferencePriceCrc,
            model.ReferencePriceUsd,
            model.DefaultIncomeCategoryId);
    }

    private static CatalogItemEditViewModel ToEditModel(CatalogItemModel item)
    {
        return new CatalogItemEditViewModel
        {
            Code = item.Code,
            Name = item.Name,
            Type = item.Type,
            Description = item.Description,
            UnitOfMeasure = item.UnitOfMeasure,
            ReferencePriceCrc = item.ReferencePriceCrc,
            ReferencePriceUsd = item.ReferencePriceUsd,
            DefaultIncomeCategoryId = item.DefaultIncomeCategoryId,
            Version = item.Version,
        };
    }

    private async Task PopulateCategoryOptionsAsync(
        CatalogItemInputViewModel model,
        string? selectedCategoryName,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FinancialCategoryOption> options = await categoryService.GetActiveOptionsAsync(
            FinancialCategoryKind.Income,
            cancellationToken: cancellationToken);

        List<SelectListItem> selectItems = options
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.DefaultIncomeCategoryId))
            .ToList();

        if (model.DefaultIncomeCategoryId is Guid selectedId &&
            selectItems.All(option => option.Value != selectedId.ToString()))
        {
            selectItems.Add(new SelectListItem(
                selectedCategoryName ?? "Categoría seleccionada no disponible",
                selectedId.ToString(),
                true));
        }

        model.IncomeCategoryOptions = selectItems;
    }
}
