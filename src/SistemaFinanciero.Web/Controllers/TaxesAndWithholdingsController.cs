using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta la pantalla única "Impuestos y retenciones" (bloque 1 del paquete v7, módulo Gestionar
/// Impuestos y Retenciones de la Tabla 4), con dos secciones: tarifas de IVA y tipos de retención.
/// Reemplaza las pantallas separadas de <c>TaxTypesController</c> y <c>WithholdingTypesController</c>,
/// cuyas rutas anteriores ahora redirigen aquí.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>ITaxTypeService</c> e <c>IWithholdingTypeService</c> (Application), sin cambiar
/// ninguno de los dos. Finanzas y Gerencia editan el porcentaje, la base mínima, la descripción y el
/// estado; las tarifas y la retención que usan la lógica del sistema (<c>SystemRole</c>/
/// <c>IsSystemType</c>) no se pueden inactivar. Sin pantalla de alta ni filtros (paquete v8): el
/// catálogo es fijo y solo se agrega una tarifa o retención nueva a pedido explícito del usuario,
/// directamente en el código/una migración, nunca desde el navegador.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("impuestos-y-retenciones")]
public sealed class TaxesAndWithholdingsController(
    ITaxTypeService taxTypeService,
    IWithholdingTypeService withholdingTypeService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        // Sin filtros (bloque de simplificación del paquete v8): el catálogo de tarifas de IVA y
        // tipos de retención es fijo y pequeño; se muestra completo, activos e inactivos, para que
        // Gerencia pueda reactivar uno si lo necesita.
        CatalogQuery taxQuery = new(status: CatalogStatusFilter.All);
        CatalogQuery withholdingQuery = new(status: CatalogStatusFilter.All);

        PagedResult<TaxTypeModel> taxTypes = await taxTypeService.SearchAsync(taxQuery, cancellationToken);
        PagedResult<WithholdingTypeModel> withholdingTypes = await withholdingTypeService.SearchAsync(withholdingQuery, cancellationToken);

        bool canManage = (await authorizationService.AuthorizeAsync(User, SystemPolicies.ManageBusinessCatalogs)).Succeeded;

        return View(new TaxesAndWithholdingsIndexViewModel(
            taxTypes,
            withholdingTypes,
            canManage));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("tarifas/{id:guid}/editar")]
    public async Task<IActionResult> EditTaxType(Guid id, CancellationToken cancellationToken)
    {
        TaxTypeModel? taxType = await taxTypeService.GetAsync(id, cancellationToken);
        return taxType is null ? NotFound() : View(ToEditModel(taxType));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("tarifas/{id:guid}/editar")]
    public async Task<IActionResult> EditTaxType(Guid id, TaxTypeRateEditViewModel model, CancellationToken cancellationToken)
    {
        if (model.Rate > SistemaFinanciero.Domain.Catalogs.TaxType.MaxPercentageRate)
        {
            ModelState.AddModelError(nameof(model.Rate), "La tarifa porcentual no puede superar 100 %.");
        }

        TaxTypeModel? current = await taxTypeService.GetAsync(id, cancellationToken);

        if (current is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Code = current.Code;
            model.Name = current.Name;
            model.HaciendaRateCode = current.HaciendaRateCode;
            model.IsSystemRole = current.SystemRole is not null;
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        // Código, nombre y código de Hacienda quedan fijos en esta pantalla: se conservan tal como
        // están, nunca se toman del formulario reducido (bloque 1 del paquete v7, punto 6).
        CatalogOperationResult result = await taxTypeService.UpdateAsync(
            id,
            new UpdateTaxTypeCommand(current.Code, current.Name, model.Rate, current.HaciendaRateCode, model.Description),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El tipo de impuesto fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            TaxTypeModel? latest = await taxTypeService.GetAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        model.Code = current.Code;
        model.Name = current.Name;
        model.HaciendaRateCode = current.HaciendaRateCode;
        model.IsSystemRole = current.SystemRole is not null;
        return FormFailure(result, model, nameof(EditTaxType));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("tarifas/{id:guid}/estado")]
    public async Task<IActionResult> SetTaxTypeActive(Guid id, CatalogStatusViewModel model, CancellationToken cancellationToken)
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

        CatalogOperationResult result = await taxTypeService.SetActiveAsync(id, model.IsActive, model.Version, actorId, cancellationToken);

        if (!result.IsSuccess)
        {
            return StateFailure(result, nameof(TaxesAndWithholdingsController).Replace("Controller", string.Empty, StringComparison.Ordinal));
        }

        SetSuccessMessage(model.IsActive ? "El tipo de impuesto fue reactivado." : "El tipo de impuesto fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("retenciones/{id:guid}/editar")]
    public async Task<IActionResult> EditWithholdingType(Guid id, CancellationToken cancellationToken)
    {
        WithholdingTypeModel? withholdingType = await withholdingTypeService.GetAsync(id, cancellationToken);
        return withholdingType is null ? NotFound() : View(ToEditModel(withholdingType));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("retenciones/{id:guid}/editar")]
    public async Task<IActionResult> EditWithholdingType(Guid id, WithholdingTypeRateEditViewModel model, CancellationToken cancellationToken)
    {
        WithholdingTypeModel? current = await withholdingTypeService.GetAsync(id, cancellationToken);

        if (current is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Code = current.Code;
            model.Name = current.Name;
            model.IsSystemType = current.IsSystemType;
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await withholdingTypeService.UpdateAsync(
            id,
            new UpdateWithholdingTypeCommand(current.Code, current.Name, model.Rate, model.MinimumBase, model.Description),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El tipo de retención fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            WithholdingTypeModel? latest = await withholdingTypeService.GetAsync(id, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        model.Code = current.Code;
        model.Name = current.Name;
        model.IsSystemType = current.IsSystemType;
        return FormFailure(result, model, nameof(EditWithholdingType));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("retenciones/{id:guid}/estado")]
    public async Task<IActionResult> SetWithholdingTypeActive(Guid id, CatalogStatusViewModel model, CancellationToken cancellationToken)
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

        CatalogOperationResult result = await withholdingTypeService.SetActiveAsync(id, model.IsActive, model.Version, actorId, cancellationToken);

        if (!result.IsSuccess)
        {
            return StateFailure(result, nameof(TaxesAndWithholdingsController).Replace("Controller", string.Empty, StringComparison.Ordinal));
        }

        SetSuccessMessage(model.IsActive ? "El tipo de retención fue reactivado." : "El tipo de retención fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    private static TaxTypeRateEditViewModel ToEditModel(TaxTypeModel taxType)
    {
        return new TaxTypeRateEditViewModel
        {
            Code = taxType.Code,
            Name = taxType.Name,
            HaciendaRateCode = taxType.HaciendaRateCode,
            IsSystemRole = taxType.SystemRole is not null,
            Rate = taxType.Rate,
            Description = taxType.Description,
            Version = taxType.Version,
        };
    }

    private static WithholdingTypeRateEditViewModel ToEditModel(WithholdingTypeModel withholdingType)
    {
        return new WithholdingTypeRateEditViewModel
        {
            Code = withholdingType.Code,
            Name = withholdingType.Name,
            IsSystemType = withholdingType.IsSystemType,
            Rate = withholdingType.Rate,
            MinimumBase = withholdingType.MinimumBase,
            Description = withholdingType.Description,
            Version = withholdingType.Version,
        };
    }
}
