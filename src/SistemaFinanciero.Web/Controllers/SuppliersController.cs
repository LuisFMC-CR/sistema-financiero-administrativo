using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el mantenimiento administrativo de proveedores.</summary>
/// <remarks>
/// Conexiones: usa <c>ISupplierCatalogService</c> (Application), implementado por
/// <c>SupplierCatalogService</c> (Infrastructure). Mismas políticas que <c>CustomersController</c>.
/// Nadie más lo consume todavía: el módulo de Gastos, que elegiría un proveedor, no existe.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/proveedores")]
public sealed class SuppliersController(ISupplierCatalogService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<SupplierModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<SupplierModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new SupplierInputViewModel());
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        SupplierInputViewModel model,
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
            ToCommand(model),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El proveedor fue creado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        SupplierModel? supplier = await service.GetAsync(id, cancellationToken);
        return supplier is null ? NotFound() : View(ToEditModel(supplier));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        SupplierEditViewModel model,
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
            ToCommand(model),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El proveedor fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            SupplierModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.Version = current.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Edit));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
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
            return StateFailure(result, nameof(SuppliersController).Replace("Controller", string.Empty, StringComparison.Ordinal));
        }

        SetSuccessMessage(model.IsActive ? "El proveedor fue reactivado." : "El proveedor fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    private static SaveSupplierCommand ToCommand(SupplierInputViewModel model)
    {
        return new(
            model.Name,
            model.Identification,
            model.Email,
            model.Phone,
            model.Address,
            model.EconomicActivity);
    }

    private static SupplierEditViewModel ToEditModel(SupplierModel supplier)
    {
        return new SupplierEditViewModel
        {
            Code = supplier.Code,
            Name = supplier.Name,
            Identification = supplier.Identification,
            Email = supplier.Email,
            Phone = supplier.Phone,
            Address = supplier.Address,
            EconomicActivity = supplier.EconomicActivity,
            Version = supplier.Version,
        };
    }
}
