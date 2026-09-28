using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Opción web para seleccionar una categoría padre de la misma naturaleza.</summary>
public sealed record FinancialCategoryParentOptionViewModel(
    Guid Id,
    string Label,
    FinancialCategoryKind Kind);

/// <summary>Opción web para seleccionar la cuenta contable de una categoría, según su naturaleza.</summary>
public sealed record FinancialCategoryLedgerAccountOptionViewModel(
    Guid Id,
    string Label,
    FinancialCategoryKind Kind);

/// <summary>Entrada para crear o editar una categoría financiera.</summary>
public class FinancialCategoryInputViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(FinancialCategoryKind), ErrorMessage = "Seleccione una naturaleza válida.")]
    [Display(Name = "Naturaleza")]
    public FinancialCategoryKind Kind { get; set; } = FinancialCategoryKind.Income;

    [Display(Name = "Categoría padre")]
    public Guid? ParentId { get; set; }

    [Required(ErrorMessage = "Seleccione una cuenta contable.")]
    [Display(Name = "Cuenta contable")]
    public Guid? LedgerAccountId { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [ValidateNever]
    public IReadOnlyList<FinancialCategoryParentOptionViewModel> ParentOptions { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<FinancialCategoryLedgerAccountOptionViewModel> LedgerAccountOptions { get; set; } = [];
}

/// <summary>Entrada para editar una categoría con concurrencia optimista.</summary>
public sealed class FinancialCategoryEditViewModel : FinancialCategoryInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión de la categoría.")]
    public string Version { get; set; } = string.Empty;
}

