using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
public sealed class BudgetsController(IBudgetService service) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken cancellationToken)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        int periodYear = year is > 0 ? year.Value : today.Year;
        int periodMonth = month is >= 1 and <= 12 ? month.Value : today.Month;

        IReadOnlyList<BudgetModel> results = await service.SearchByPeriodAsync(periodYear, periodMonth, cancellationToken);

        return View(new BudgetIndexViewModel(results, periodYear, periodMonth));
    }

    [HttpGet("{categoryId:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid categoryId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        BudgetModel? model = await service.GetAsync(categoryId, year, month, cancellationToken);

        if (model is null)
        {
            return NotFound();
        }

        return View(new BudgetEditViewModel
        {
            CategoryId = categoryId,
            CategoryLabel = $"{model.CategoryCode} - {model.CategoryName}",
            PeriodYear = year,
            PeriodMonth = month,
            Amount = model.BudgetedAmount ?? 0m,
            Version = model.Version ?? string.Empty,
        });
    }

    [HttpPost("{categoryId:guid}/editar")]
    public async Task<IActionResult> Edit(
        Guid categoryId,
        BudgetEditViewModel model,
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

        CatalogOperationResult result = await service.SetAmountAsync(
            new SetBudgetAmountCommand(categoryId, model.PeriodYear, model.PeriodMonth, model.Amount),
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
            BudgetModel? latest = await service.GetAsync(categoryId, model.PeriodYear, model.PeriodMonth, cancellationToken);
            if (latest is not null)
            {
                model.Version = latest.Version ?? string.Empty;
                ModelState.Remove(nameof(model.Version));
            }
        }

        return FormFailure(result, model, nameof(Edit));
    }
}
