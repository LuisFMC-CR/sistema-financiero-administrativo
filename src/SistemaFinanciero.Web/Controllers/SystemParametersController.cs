using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Parameters;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta los parámetros únicos del sistema: el límite de autorización, los días de alerta de
/// vencimiento, el período fiscal y los datos de la empresa. El período fiscal y las reaperturas
/// mensuales son exclusivos de Gerencia.
/// </summary>
/// <remarks>
/// El período fiscal, las reaperturas mensuales y los datos de la empresa tienen formularios separados
/// de <see cref="Save"/> (límite y días). Configurar el período y reabrir/cerrar meses exige Gerencia.
/// </remarks>
[Authorize(Policy = SystemPolicies.ManageSystemParameters)]
[Route("parametros")]
public sealed class SystemParametersController(ISystemParametersService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        SystemParametersModel? current = await service.GetAsync(cancellationToken);
        return View(await ToPageViewModelAsync(current, cancellationToken));
    }

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("")]
    public async Task<IActionResult> Save(
        [Bind(Prefix = "Parameters")] SystemParametersViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersModel? current = await service.GetAsync(cancellationToken);
            SystemParametersPageViewModel page = await ToPageViewModelAsync(current, cancellationToken);
            page.Parameters = model;
            return View(nameof(Index), page);
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

        SystemParametersPageViewModel errorPage = await ToPageViewModelAsync(latest, cancellationToken);
        errorPage.Parameters = latestModel;
        return FormFailure(result, errorPage, nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageAccountingPeriods)]
    [HttpPost("periodo-fiscal")]
    public async Task<IActionResult> UpdateFiscalPeriod(
        [Bind(Prefix = "FiscalPeriodForm")] FiscalPeriodViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersModel? current = await service.GetAsync(cancellationToken);
            SystemParametersPageViewModel page = await ToPageViewModelAsync(current, cancellationToken);
            page.FiscalPeriodForm = model;
            return View(nameof(Index), page);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateFiscalPeriodAsync(
            model.StartDate!.Value,
            model.EndDate!.Value,
            model.Reason,
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El período fiscal se actualizó correctamente.");
            return RedirectToAction(nameof(Index));
        }

        SystemParametersModel? latest = await service.GetAsync(cancellationToken);

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict && latest is not null)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        SystemParametersPageViewModel errorPage = await ToPageViewModelAsync(latest, cancellationToken);
        errorPage.FiscalPeriodForm = model;
        return FormFailure(result, errorPage, nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageAccountingPeriods)]
    [HttpPost("periodos/reabrir")]
    public async Task<IActionResult> ReopenAccountingPeriod(
        [Bind(Prefix = "ReopenAccountingPeriodForm")] ReopenAccountingPeriodViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersPageViewModel page = await ToPageViewModelAsync(
                await service.GetAsync(cancellationToken), cancellationToken);
            page.ReopenAccountingPeriodForm = model;
            return View(nameof(Index), page);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.ReopenAccountingPeriodAsync(
            model.Year, model.Month, model.Reason, actorId, cancellationToken);
        if (result.IsSuccess)
        {
            SetSuccessMessage("El mes se reabrió correctamente y permanecerá abierto hasta que Gerencia lo cierre.");
            return RedirectToAction(nameof(Index));
        }

        SystemParametersPageViewModel errorPage = await ToPageViewModelAsync(
            await service.GetAsync(cancellationToken), cancellationToken);
        errorPage.ReopenAccountingPeriodForm = model;
        return FormFailure(result, errorPage, nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageAccountingPeriods)]
    [HttpPost("periodos/cerrar")]
    public async Task<IActionResult> CloseAccountingPeriod(
        CloseAccountingPeriodViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersPageViewModel page = await ToPageViewModelAsync(
                await service.GetAsync(cancellationToken), cancellationToken);
            return View(nameof(Index), page);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.CloseAccountingPeriodAsync(
            model.Year, model.Month, model.Version, actorId, cancellationToken);
        if (result.IsSuccess)
        {
            SetSuccessMessage("El mes se cerró nuevamente.");
            return RedirectToAction(nameof(Index));
        }

        SystemParametersPageViewModel errorPage = await ToPageViewModelAsync(
            await service.GetAsync(cancellationToken), cancellationToken);
        return FormFailure(result, errorPage, nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("datos-empresa")]
    public async Task<IActionResult> UpdateCompany(
        [Bind(Prefix = "CompanyDataForm")] CompanyDataViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SystemParametersModel? current = await service.GetAsync(cancellationToken);
            SystemParametersPageViewModel page = await ToPageViewModelAsync(current, cancellationToken);
            page.CompanyDataForm = model;
            return View(nameof(Index), page);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateCompanyAsync(
            new SaveCompanyDataCommand(
                model.Name,
                model.LegalId,
                model.Address,
                model.Phone,
                model.Email,
                model.ActivityCode,
                model.TradeName,
                model.ProvinceCode,
                model.CantonCode,
                model.DistrictCode,
                model.BranchCode,
                model.TerminalCode),
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

        SystemParametersPageViewModel errorPage = await ToPageViewModelAsync(latest, cancellationToken);
        errorPage.CompanyDataForm = model;
        return FormFailure(result, errorPage, nameof(Index));
    }

    private async Task<SystemParametersPageViewModel> ToPageViewModelAsync(
        SystemParametersModel? current,
        CancellationToken cancellationToken) => new()
    {
        CurrentConfiguration = current,
        Parameters = ToViewModel(current),
        FiscalPeriodForm = ToFiscalPeriodViewModel(current),
        ReopenedAccountingPeriods = await service.GetReopenedAccountingPeriodsAsync(cancellationToken),
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
                Version = current.Version,
                UpdatedAtUtc = current.UpdatedAtUtc,
                UpdatedByUserName = current.UpdatedByUserName,
            };
    }

    private static FiscalPeriodViewModel ToFiscalPeriodViewModel(SystemParametersModel? current)
    {
        return current is null
            ? new FiscalPeriodViewModel()
            : new FiscalPeriodViewModel
            {
                StartDate = current.FiscalPeriodStartDate,
                EndDate = current.FiscalPeriodEndDate,
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
                ActivityCode = current.CompanyActivityCode,
                TradeName = current.CompanyTradeName,
                ProvinceCode = current.CompanyProvinceCode,
                CantonCode = current.CompanyCantonCode,
                DistrictCode = current.CompanyDistrictCode,
                BranchCode = current.CompanyBranchCode ?? "001",
                TerminalCode = current.CompanyTerminalCode ?? "00001",
                Version = current.Version,
            };
    }
}
