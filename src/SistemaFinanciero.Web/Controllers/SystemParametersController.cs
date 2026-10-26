using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Parameters;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta los parámetros únicos del sistema: el límite de autorización, los días de alerta de
/// vencimiento, la fecha de cierre y los datos de la empresa. Consultan Gerencia y Finanzas; solo
/// Gerencia los define.
/// </summary>
/// <remarks>
/// La fecha de cierre (regla 16) y los datos de la empresa (RF-55) tienen cada uno su propio
/// formulario y su propia acción (<see cref="ChangeClosingDate"/>, <see cref="UpdateCompany"/>),
/// separados de <see cref="Save"/> (límite y días): adelantar la fecha de cierre no exige motivo,
/// retrocederla (reapertura) sí, y ese motivo no tiene sentido en los otros dos formularios; los
/// datos de la empresa no comparten nada con el límite, los días ni la fecha de cierre.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("parametros")]
public sealed class SystemParametersController(ISystemParametersService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SystemParametersModel? current = await service.GetAsync(cancellationToken);
        return View(ToPageViewModel(current));
    }

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("")]
    public async Task<IActionResult> Save(SystemParametersViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersModel? current = await service.GetAsync(cancellationToken);
            return View(nameof(Index), new SystemParametersPageViewModel
            {
                Parameters = model,
                ClosingDateForm = ToClosingDateViewModel(current),
                CompanyDataForm = ToCompanyDataViewModel(current),
            });
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

        return FormFailure(
            result,
            new SystemParametersPageViewModel
            {
                Parameters = latestModel,
                ClosingDateForm = ToClosingDateViewModel(latest),
                CompanyDataForm = ToCompanyDataViewModel(latest),
            },
            nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("fecha-cierre")]
    public async Task<IActionResult> ChangeClosingDate(
        ChangeClosingDateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersModel? current = await service.GetAsync(cancellationToken);
            return View(nameof(Index), new SystemParametersPageViewModel
            {
                Parameters = ToViewModel(current),
                ClosingDateForm = model,
                CompanyDataForm = ToCompanyDataViewModel(current),
            });
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.ChangeClosingDateAsync(
            model.NewClosingDate, model.Reason, model.Version, actorId, cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La fecha de cierre se actualizó correctamente.");
            return RedirectToAction(nameof(Index));
        }

        SystemParametersModel? latest = await service.GetAsync(cancellationToken);

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict && latest is not null)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(
            result,
            new SystemParametersPageViewModel
            {
                Parameters = ToViewModel(latest),
                ClosingDateForm = model,
                CompanyDataForm = ToCompanyDataViewModel(latest),
            },
            nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("datos-empresa")]
    public async Task<IActionResult> UpdateCompany(
        CompanyDataViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersModel? current = await service.GetAsync(cancellationToken);
            return View(nameof(Index), new SystemParametersPageViewModel
            {
                Parameters = ToViewModel(current),
                ClosingDateForm = ToClosingDateViewModel(current),
                CompanyDataForm = model,
            });
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateCompanyAsync(
            new SaveCompanyDataCommand(model.Name, model.LegalId, model.Address, model.Phone, model.Email),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("Los datos de la empresa se actualizaron correctamente.");
            return RedirectToAction(nameof(Index));
        }

        SystemParametersModel? latest = await service.GetAsync(cancellationToken);

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict && latest is not null)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(
            result,
            new SystemParametersPageViewModel
            {
                Parameters = ToViewModel(latest),
                ClosingDateForm = ToClosingDateViewModel(latest),
                CompanyDataForm = model,
            },
            nameof(Index));
    }

    private static SystemParametersPageViewModel ToPageViewModel(SystemParametersModel? current) => new()
    {
        Parameters = ToViewModel(current),
        ClosingDateForm = ToClosingDateViewModel(current),
        CompanyDataForm = ToCompanyDataViewModel(current),
    };

    private static SystemParametersViewModel ToViewModel(SystemParametersModel? current)
    {
        return current is null
            ? new SystemParametersViewModel()
            : new SystemParametersViewModel
            {
                AuthorizationLimitCrc = current.AuthorizationLimitCrc,
                OverdueAlertDays = current.OverdueAlertDays,
                ClosingDate = current.ClosingDate,
                Version = current.Version,
                UpdatedAtUtc = current.UpdatedAtUtc,
                UpdatedByUserName = current.UpdatedByUserName,
            };
    }

    private static ChangeClosingDateViewModel ToClosingDateViewModel(SystemParametersModel? current)
    {
        return current is null
            ? new ChangeClosingDateViewModel()
            : new ChangeClosingDateViewModel
            {
                NewClosingDate = current.ClosingDate,
                Version = current.Version,
            };
    }

    private static CompanyDataViewModel ToCompanyDataViewModel(SystemParametersModel? current)
    {
        return current is null
            ? new CompanyDataViewModel()
            : new CompanyDataViewModel
            {
                Name = current.CompanyName ?? string.Empty,
                LegalId = current.CompanyLegalId ?? string.Empty,
                Address = current.CompanyAddress,
                Phone = current.CompanyPhone,
                Email = current.CompanyEmail,
                Version = current.Version,
            };
    }
}
