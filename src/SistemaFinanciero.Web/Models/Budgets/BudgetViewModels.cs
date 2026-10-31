using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Budgets;

namespace SistemaFinanciero.Web.Models.Budgets;

/// <summary>Modelo de listado del presupuesto de un mes: una fila por categoría de gasto de último nivel.</summary>
public sealed record BudgetIndexViewModel(
    IReadOnlyList<BudgetModel> Results,
    int PeriodYear,
    int PeriodMonth);

/// <summary>Entrada para crear o corregir el monto presupuestado de una categoría y un mes (regla 6).</summary>
public sealed class BudgetEditViewModel
{
    [ValidateNever]
    public Guid CategoryId { get; set; }

    [ValidateNever]
    public string CategoryLabel { get; set; } = string.Empty;

    [ValidateNever]
    public int PeriodYear { get; set; }

    [ValidateNever]
    public int PeriodMonth { get; set; }

    [Range(0, 999_999_999_999.99, ErrorMessage = "El monto no puede ser negativo.")]
    [Display(Name = "Monto presupuestado")]
    public decimal Amount { get; set; }

    /// <summary>Vacío cuando todavía no existe presupuesto para esta categoría y este mes.</summary>
    [ValidateNever]
    public string Version { get; set; } = string.Empty;
}

/// <summary>
/// Entrada para agregar una línea de presupuesto nueva: se elige la categoría de una lista (solo las
/// que todavía no tienen presupuesto ese mes) y se indica el monto de una vez, en vez de ubicarla en
/// la tabla del mes y editarla ahí.
/// </summary>
public sealed class BudgetCreateViewModel
{
    [Required(ErrorMessage = "La categoría es obligatoria.")]
    [Display(Name = "Categoría de gasto")]
    public Guid? CategoryId { get; set; }

    [ValidateNever]
    public IReadOnlyList<SelectListItem> CategoryOptions { get; set; } = [];

    [ValidateNever]
    public int PeriodYear { get; set; }

    [ValidateNever]
    public int PeriodMonth { get; set; }

    [Range(0, 999_999_999_999.99, ErrorMessage = "El monto no puede ser negativo.")]
    [Display(Name = "Monto presupuestado")]
    public decimal Amount { get; set; }
}
