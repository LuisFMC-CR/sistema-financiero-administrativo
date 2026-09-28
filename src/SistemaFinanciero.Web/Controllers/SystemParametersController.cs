using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Parameters;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta los parámetros únicos del sistema: el límite de autorización y los días de alerta de
/// vencimiento. Consultan Gerencia y Finanzas; solo Gerencia los define.
/// </summary>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("parametros")]
public sealed class SystemParametersController(ISystemParametersService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SystemParametersModel? current = await service.GetAsync(cancellationToken);
        return View(ToViewModel(current));
    }

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("")]
    public async Task<IActionResult> Save(SystemParametersViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        SaveSystemParametersCommand command = new(model.AuthorizationLimitCrc, model.OverdueAlertDays);

        CatalogOperationResult result = string.IsNullOrEmpty(model.Version)
            ? await service.CreateAsync(command, actorId, cancellationToken)
            : await service.UpdateAsync(command, model.Version, actorId, cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("Los parámetros del sistema se guardaron correctamente.");
            return RedirectToAction(nameof(Index));
        }

        SystemParametersModel? latest = await service.GetAsync(cancellationToken);
        SystemParametersViewModel latestModel = ToViewModel(latest);
        latestModel.AuthorizationLimitCrc = model.AuthorizationLimitCrc;
        latestModel.OverdueAlertDays = model.OverdueAlertDays;

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict && latest is not null)
        {
            latestModel.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, latestModel, nameof(Index));
    }

    private static SystemParametersViewModel ToViewModel(SystemParametersModel? current)
    {
        return current is null
            ? new SystemParametersViewModel()
            : new SystemParametersViewModel
            {
                AuthorizationLimitCrc = current.AuthorizationLimitCrc,
                OverdueAlertDays = current.OverdueAlertDays,
                Version = current.Version,
                UpdatedAtUtc = current.UpdatedAtUtc,
                UpdatedByUserName = current.UpdatedByUserName,
            };
    }
}
