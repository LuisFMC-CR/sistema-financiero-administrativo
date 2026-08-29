using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el mantenimiento administrativo de clientes.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/clientes")]
public sealed class CustomersController(ICustomerCatalogService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        CatalogStatusFilter status = CatalogStatusFilter.Active,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        CatalogQuery query = new(NormalizeSearch(search), NormalizeStatus(status), page);
        PagedResult<CustomerModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new CatalogIndexViewModel<CustomerModel>(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new CustomerInputViewModel());
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        CustomerInputViewModel model,
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
            SetSuccessMessage("El cliente fue creado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        CustomerModel? customer = await service.GetAsync(id, cancellationToken);
        return customer is null ? NotFound() : View(ToEditModel(customer));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessContacts)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        CustomerEditViewModel model,
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
            SetSuccessMessage("El cliente fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            CustomerModel? current = await service.GetAsync(id, cancellationToken);
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
            return StateFailure(result, nameof(CustomersController).Replace("Controller", string.Empty, StringComparison.Ordinal));
        }

        SetSuccessMessage(model.IsActive ? "El cliente fue reactivado." : "El cliente fue desactivado.");
        return RedirectToAction(nameof(Index));
    }

    private static SaveCustomerCommand ToCommand(BusinessContactInputViewModel model)
    {
        return new(
            model.Code,
            model.Name,
            model.Identification,
            model.Email,
            model.Phone,
            model.Address);
    }

    private static CustomerEditViewModel ToEditModel(CustomerModel customer)
    {
        return new CustomerEditViewModel
        {
            Code = customer.Code,
            Name = customer.Name,
            Identification = customer.Identification,
            Email = customer.Email,
            Phone = customer.Phone,
            Address = customer.Address,
            Version = customer.Version,
        };
    }
}
