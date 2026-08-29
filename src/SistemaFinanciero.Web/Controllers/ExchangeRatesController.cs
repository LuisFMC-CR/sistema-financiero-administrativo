using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.ExchangeRates;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta los tipos de cambio diarios CRC por USD capturados manualmente.</summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/tipos-cambio")]
public sealed class ExchangeRatesController(IDailyExchangeRateService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        PageQuery query = new(NormalizeSearch(search), page);
        PagedResult<DailyExchangeRateModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new PagedIndexViewModel<DailyExchangeRateModel>(results, query.Search));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new DailyExchangeRateInputViewModel());
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        DailyExchangeRateInputViewModel model,
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
            SetSuccessMessage("El tipo de cambio fue registrado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        return FormFailure(result, model, nameof(Create));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        DailyExchangeRateModel? exchangeRate = await service.GetAsync(id, cancellationToken);
        return exchangeRate is null ? NotFound() : View(ToEditModel(exchangeRate));
    }

    [Authorize(Policy = SystemPolicies.ManageBusinessCatalogs)]
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        DailyExchangeRateEditViewModel model,
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
            SetSuccessMessage("El tipo de cambio fue actualizado correctamente.");
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            DailyExchangeRateModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.Version = current.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Edit));
    }

    private static SaveDailyExchangeRateCommand ToCommand(DailyExchangeRateInputViewModel model)
    {
        return new(model.EffectiveDate, model.CrcPerUsd, model.Source, model.Notes);
    }

    private static DailyExchangeRateEditViewModel ToEditModel(DailyExchangeRateModel exchangeRate)
    {
        return new DailyExchangeRateEditViewModel
        {
            EffectiveDate = exchangeRate.EffectiveDate,
            CrcPerUsd = exchangeRate.CrcPerUsd,
            Source = exchangeRate.Source,
            Notes = exchangeRate.Notes,
            Version = exchangeRate.Version,
        };
    }
}
