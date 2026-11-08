using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Budgets;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta y edita el presupuesto mensual por categoría de gasto de último nivel (regla 6).</summary>
/// <remarks>
/// Conexiones: usa <c>IBudgetService</c>. Gerencia y Finanzas ven y editan el presupuesto, misma
/// política que las demás pantallas de solo consulta financiera: Asistente "no ve... presupuesto"
/// (perfiles). La alerta no bloqueante al confirmar un gasto la muestra
/// <c>ExpensesController.Confirm</c>, no esta pantalla.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("presupuesto")]
public sealed class BudgetsController(IBudgetService service, TimeProvider timeProvider) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        int? year,
        int? month,
        BudgetListFilter filter = BudgetListFilter.OnlyWithBudget,
        CancellationToken cancellationToken = default)
    {
        DateOnly today = GetCurrentDate();
        int periodYear = year is > 0 ? year.Value : today.Year;
        int periodMonth = month is >= 1 and <= 12 ? month.Value : today.Month;
        BudgetListFilter normalizedFilter = Enum.IsDefined(filter) ? filter : BudgetListFilter.OnlyWithBudget;

        IReadOnlyList<BudgetModel> results = await service.SearchByPeriodAsync(periodYear, periodMonth, cancellationToken);

        if (normalizedFilter == BudgetListFilter.OnlyWithBudget)
        {
            results = results.Where(budget => budget.BudgetId is not null).ToArray();
        }

        return View(new BudgetIndexViewModel(
            results,
            periodYear,
            periodMonth,
            normalizedFilter,
            IsEditablePeriod(periodYear, periodMonth)));
    }

    [HttpGet("agregar")]
    public async Task<IActionResult> Create(int year, int month, CancellationToken cancellationToken)
    {
        if (!IsEditablePeriod(year, month))
        {
            return RejectPeriod(year, month);
        }

        BudgetCreateViewModel model = new() { PeriodYear = year, PeriodMonth = month };
        await PopulateCreateOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost("agregar")]
    public async Task<IActionResult> Create(BudgetCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!IsEditablePeriod(model.PeriodYear, model.PeriodMonth))
        {
            return RejectPeriod(model.PeriodYear, model.PeriodMonth);
        }

        if (!ModelState.IsValid)
        {
            await PopulateCreateOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.SetAmountAsync(
            new SetBudgetAmountCommand(model.AccountId!.Value, model.PeriodYear, model.PeriodMonth, model.Amount),
            version: null,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La línea de presupuesto fue agregada.");
            return RedirectToAction(nameof(Index), new { year = model.PeriodYear, month = model.PeriodMonth });
        }

        await PopulateCreateOptionsAsync(model, cancellationToken);
        return FormFailure(result, model, nameof(Create));
    }

    [HttpGet("{accountId:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid accountId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (!IsEditablePeriod(year, month))
        {
            return RejectPeriod(year, month);
        }

        BudgetModel? model = await service.GetAsync(accountId, year, month, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(new BudgetEditViewModel
        {
            AccountId = accountId,
            CategoryLabel = $"{model.AccountCode} - {model.AccountName}",
            PeriodYear = year,
            PeriodMonth = month,
            Amount = model.BudgetedAmount ?? 0m,
            Version = model.Version ?? string.Empty,
        });
    }

    [HttpPost("{accountId:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid accountId,
        BudgetEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!IsEditablePeriod(model.PeriodYear, model.PeriodMonth))
        {
            return RejectPeriod(model.PeriodYear, model.PeriodMonth);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.SetAmountAsync(
            new SetBudgetAmountCommand(accountId, model.PeriodYear, model.PeriodMonth, model.Amount),
            string.IsNullOrEmpty(model.Version) ? null : model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El presupuesto fue actualizado.");
            return RedirectToAction(nameof(Index), new { year = model.PeriodYear, month = model.PeriodMonth });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            BudgetModel? latest = await service.GetAsync(accountId, model.PeriodYear, model.PeriodMonth, cancellationToken);
            if (latest is not null)
            {
                model.Version = latest.Version ?? string.Empty;
                ModelState.Remove(nameof(model.Version));
            }
        }

        return FormFailure(result, model, nameof(Edit));
    }

    private async Task PopulateCreateOptionsAsync(BudgetCreateViewModel model, CancellationToken cancellationToken)
    {
        IReadOnlyList<BudgetModel> results = await service.SearchByPeriodAsync(
            model.PeriodYear,
            model.PeriodMonth,
            cancellationToken);

        model.CategoryOptions = results
            .Where(budget => budget.BudgetId is null)
            .Select(budget => new SelectListItem(
                $"{budget.AccountCode} - {budget.AccountName}",
                budget.AccountId.ToString(),
                budget.AccountId == model.AccountId))
            .ToList();
    }

    private DateOnly GetCurrentDate() =>
        DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);

    private bool IsEditablePeriod(int year, int month)
    {
        if (year is < 2000 or > 2200 || month is < 1 or > 12)
        {
            return false;
        }

        DateOnly periodStart = new(year, month, 1);
        DateOnly today = GetCurrentDate();
        return periodStart >= new DateOnly(today.Year, today.Month, 1);
    }

    private RedirectToActionResult RejectPeriod(int year, int month)
    {
        TempData["ErrorMessage"] =
            "El presupuesto solo se puede crear o modificar para el mes actual o uno futuro.";

        DateOnly today = GetCurrentDate();
        bool validPeriod = year is >= 2000 and <= 2200 && month is >= 1 and <= 12;
        return RedirectToAction(nameof(Index), new
        {
            year = validPeriod ? year : today.Year,
            month = validPeriod ? month : today.Month,
        });
    }
}
