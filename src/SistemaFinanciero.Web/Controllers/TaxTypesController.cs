using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta los tipos de impuesto que las líneas de factura pueden aplicar.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/tipos-impuesto")]
public sealed class TaxTypesController(ITaxTypeService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<TaxTypeModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<TaxTypeModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new TaxTypeCreateViewModel());
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        TaxTypeCreateViewModel model,
        CancellationToken cancellationToken)
    {
        ValidatePercentageRate(model.CalculationType, model.Rate, nameof(model.Rate));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.CreateAsync(
            new CreateTaxTypeCommand(
                model.Code,
                model.Name,
                model.CalculationType,
                model.Rate,
                model.Description),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El tipo de impuesto fue creado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        TaxTypeModel? taxType = await service.GetAsync(id, cancellationToken);
        return taxType is null ? NotFound() : View(ToEditModel(taxType));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        TaxTypeEditViewModel model,
        CancellationToken cancellationToken)
    {
        // El método de cálculo nunca se toma del formulario: es inmutable y se lee del registro actual.
        TaxTypeModel? current = await service.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }

        model.CalculationType = current.CalculationType;
        ValidatePercentageRate(model.CalculationType, model.Rate, nameof(model.Rate));

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
            new UpdateTaxTypeCommand(model.Code, model.Name, model.Rate, model.Description),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El tipo de impuesto fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        TaxTypeModel? latest = await service.GetAsync(id, cancellationToken);
        if (latest is null)
        {
            return NotFound();
        }

        model.CalculationType = latest.CalculationType;
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
            return StateFailure(result, "TaxTypes");
        }

        SetSuccessMessage(model.IsActive
            ? "El tipo de impuesto fue reactivado."
            : "El tipo de impuesto fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("cargar-referencia")]
    public async Task<IActionResult> LoadReference(CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        ReferenceTaxTypesLoadResult result = await service.LoadReferenceTaxTypesAsync(
            actorId,
            cancellationToken);

        if (!result.Operation.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Operation.Message
                ?? "No fue posible cargar las tarifas de referencia.";
            return RedirectToAction(nameof(Index));
        }

        SetSuccessMessage(result.CreatedCount == 0
            ? "Las tarifas de referencia ya estaban registradas; no se creó ninguna."
            : $"Se crearon {result.CreatedCount} tarifas de referencia; {result.ExistingCount} ya existían. " +
              "Valide su uso con la asesoría contable.");
        return RedirectToAction(nameof(Index));
    }

    private void ValidatePercentageRate(TaxCalculationType calculationType, decimal rate, string key)
    {
        if (calculationType == TaxCalculationType.Percentage && rate > TaxType.MaxPercentageRate)
        {
            ModelState.AddModelError(key, "La tarifa porcentual no puede superar 100 %.");
        }
    }

    private static TaxTypeEditViewModel ToEditModel(TaxTypeModel taxType)
    {
        return new TaxTypeEditViewModel
        {
            Code = taxType.Code,
            Name = taxType.Name,
            Rate = taxType.Rate,
            Description = taxType.Description,
            CalculationType = taxType.CalculationType,
            Version = taxType.Version,
        };
    }
}
