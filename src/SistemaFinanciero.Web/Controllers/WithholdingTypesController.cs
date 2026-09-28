using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta los tipos de retención que se aplican al registrar abonos y pagos.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/tipos-retencion")]
public sealed class WithholdingTypesController(IWithholdingTypeService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<WithholdingTypeModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<WithholdingTypeModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new WithholdingTypeCreateViewModel());
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        WithholdingTypeCreateViewModel model,
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
            new CreateWithholdingTypeCommand(model.Code, model.Name, model.Rate, model.Description),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El tipo de retención fue creado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        WithholdingTypeModel? withholdingType = await service.GetAsync(id, cancellationToken);
        return withholdingType is null ? NotFound() : View(ToEditModel(withholdingType));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        WithholdingTypeEditViewModel model,
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

        CatalogOperationResult result = await service.UpdateAsync(
            id,
            new UpdateWithholdingTypeCommand(model.Code, model.Name, model.Rate, model.Description),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El tipo de retención fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        WithholdingTypeModel? latest = await service.GetAsync(id, cancellationToken);
        if (latest is null)
        {
            return NotFound();
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            model.Version = latest.Version;
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
            return StateFailure(result, "WithholdingTypes");
        }

        SetSuccessMessage(model.IsActive
            ? "El tipo de retención fue reactivado."
            : "El tipo de retención fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    private static WithholdingTypeEditViewModel ToEditModel(WithholdingTypeModel withholdingType)
    {
        return new WithholdingTypeEditViewModel
        {
            Code = withholdingType.Code,
            Name = withholdingType.Name,
            Rate = withholdingType.Rate,
            Description = withholdingType.Description,
            Version = withholdingType.Version,
        };
    }
}
