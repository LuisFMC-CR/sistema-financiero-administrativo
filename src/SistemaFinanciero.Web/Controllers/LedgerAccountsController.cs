using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el catálogo contable jerárquico, tabular y editable.</summary>
/// <remarks>
/// Conexiones: usa <c>ILedgerAccountService</c> (Application). El usuario solo modifica cuentas de
/// movimiento (último nivel): las raíces y los grupos son acumuladores protegidos. <c>Create</c>
/// exige elegir una cuenta superior y nunca pide código ni tipo (el servicio los calcula). <c>Edit</c>
/// solo permite cambiar nombre, referencia y descripción (regla 12); código, tipo y cuenta superior se
/// muestran de solo lectura. <c>SetActive</c> bloquea la desactivación cuando la cuenta tiene
/// transacciones en el libro mayor o documentos asociados.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/cuentas-contables")]
public sealed class LedgerAccountsController(ILedgerAccountService service) : CatalogControllerBase
{
    private static readonly LedgerAccountType[] AllTypes = Enum.GetValues<LedgerAccountType>();

    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? code,
        string? name,
        LedgerAccountType? type,
        bool? isMovement,
        bool? isActive,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        LedgerAccountQuery query = new(code, name, type, isMovement, isActive, page);
        PagedResult<LedgerAccountModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new LedgerAccountIndexViewModel
        {
            Results = results,
            Code = query.Code,
            Name = query.Name,
            Type = query.Type,
            IsMovement = query.IsMovement,
            IsActive = query.IsActive,
        });
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
        return account is null ? NotFound() : View(ToEditModel(account));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        LedgerAccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        LedgerAccountModel? current = await service.GetAsync(id, cancellationToken);
        if (current is null)
        {
            return NotFound();
        }

        PopulateDisplayFields(model, current);

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
            new UpdateLedgerAccountCommand(model.Name, model.Reference, model.Description),
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

        PopulateDisplayFields(model, latest);

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
            return StateFailure(result, "LedgerAccounts");
        }

        SetSuccessMessage(model.IsActive ? "La cuenta contable fue reactivada." : "La cuenta contable fue desactivada.");
        return RedirectToAction(nameof(Index));
    }

    private static CreateLedgerAccountCommand ToCommand(LedgerAccountCreateViewModel model)
    {
        // El subtipo solo cuenta cuando la cuenta es de efectivo; si no, se descarta.
        return new CreateLedgerAccountCommand(
            model.Name,
            model.ParentId,
            model.IsCash ? model.CashKind : null,
            model.Reference,
            model.Description);
    }

    private static LedgerAccountEditViewModel ToEditModel(LedgerAccountModel account)
    {
        LedgerAccountEditViewModel model = new()
        {
            Name = account.Name,
            Reference = account.Reference,
            Description = account.Description,
            Version = account.Version,
        };

        PopulateDisplayFields(model, account);
        return model;
    }

    private static void PopulateDisplayFields(LedgerAccountEditViewModel model, LedgerAccountModel account)
    {
        model.Code = account.Code;
        model.Type = account.Type;
        model.ParentName = account.ParentName;
        model.CashKind = account.CashKind;
        model.IsProtected = account.IsProtected;
        model.SystemRole = account.SystemRole;
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
}
