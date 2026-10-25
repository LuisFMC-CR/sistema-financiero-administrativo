using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el catálogo unificado de productos y servicios facturables.</summary>
/// <remarks>
/// Conexiones: usa <c>ICatalogItemService</c> (Application) para el elemento en sí,
/// <c>IFinancialCategoryService</c> para armar el selector de categoría de ingreso predeterminada
/// (<see cref="PopulateCategoryOptionsAsync"/>) e <c>ITaxTypeService</c> para el selector de tarifa de
/// IVA predeterminada (RF-52, <see cref="PopulateTaxTypeOptionsAsync"/>). Solo Finanzas mantiene
/// (<c>ManageBusinessCatalogs</c>). Alimentará el selector de artículos que necesitará la futura
/// pantalla de facturas, vía <c>InvoiceLine.CatalogItemId</c>.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/productos-servicios")]
public sealed class CatalogItemsController(
    ICatalogItemService service,
    IFinancialCategoryService categoryService,
    ITaxTypeService taxTypeService) : CatalogControllerBase
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
        await PopulateTaxTypeOptionsAsync(model, null, cancellationToken);
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
            await PopulateTaxTypeOptionsAsync(model, null, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.CreateAsync(
            ToCreateCommand(model),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El producto o servicio fue creado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        await PopulateCategoryOptionsAsync(model, null, cancellationToken);
        await PopulateTaxTypeOptionsAsync(model, null, cancellationToken);
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
        await PopulateTaxTypeOptionsAsync(model, item.DefaultTaxTypeName, cancellationToken);
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
            await PopulateTaxTypeOptionsAsync(model, null, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateAsync(
            id,
            ToUpdateCommand(model),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El producto o servicio fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        string? selectedCategoryName = null;
        string? selectedTaxTypeName = null;
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
            selectedTaxTypeName = current.DefaultTaxTypeId == model.DefaultTaxTypeId
                ? current.DefaultTaxTypeName
                : null;
            ModelState.Remove(nameof(model.Version));
        }

        await PopulateCategoryOptionsAsync(model, selectedCategoryName, cancellationToken);
        await PopulateTaxTypeOptionsAsync(model, selectedTaxTypeName, cancellationToken);
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

    private static CreateCatalogItemCommand ToCreateCommand(CatalogItemInputViewModel model)
    {
        return new(
            model.Name,
            model.Type,
            model.Description,
            model.UnitOfMeasure,
            model.ReferencePriceCrc,
            model.DefaultIncomeCategoryId,
            model.DefaultTaxTypeId);
    }

    private static UpdateCatalogItemCommand ToUpdateCommand(CatalogItemInputViewModel model)
    {
        return new(
            model.Name,
            model.Description,
            model.UnitOfMeasure,
            model.ReferencePriceCrc,
            model.DefaultIncomeCategoryId,
            model.DefaultTaxTypeId);
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
            DefaultIncomeCategoryId = item.DefaultIncomeCategoryId,
            DefaultTaxTypeId = item.DefaultTaxTypeId,
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

    private async Task PopulateTaxTypeOptionsAsync(
        CatalogItemInputViewModel model,
        string? selectedTaxTypeName,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TaxTypeOption> options = await taxTypeService.GetActiveOptionsAsync(cancellationToken);

        List<SelectListItem> selectItems = options
            .Select(option => new SelectListItem(
                $"{option.Name} ({option.Rate:0.##}%)",
                option.Id.ToString(),
                option.Id == model.DefaultTaxTypeId))
            .ToList();

        if (model.DefaultTaxTypeId is Guid selectedId &&
            selectItems.All(option => option.Value != selectedId.ToString()))
        {
            selectItems.Add(new SelectListItem(
                selectedTaxTypeName ?? "Tarifa de IVA seleccionada no disponible",
                selectedId.ToString(),
                true));
        }

        model.DefaultTaxTypeOptions = selectItems;
    }
}
