using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Opción web para seleccionar una cuenta superior del mismo tipo.</summary>
public sealed record LedgerAccountParentOptionViewModel(
    Guid Id,
    string Label,
    LedgerAccountType Type);

/// <summary>
/// Entrada para crear una cuenta contable. Una cuenta de efectivo indica subtipo y moneda, que quedan
/// fijos después de crearla.
/// </summary>
public sealed class LedgerAccountCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(LedgerAccountType), ErrorMessage = "Seleccione un tipo válido.")]
    [Display(Name = "Tipo")]
    public LedgerAccountType Type { get; set; } = LedgerAccountType.Asset;

    [Display(Name = "Cuenta superior")]
    public Guid? ParentId { get; set; }

    [Display(Name = "Es una cuenta de efectivo (caja o banco)")]
    public bool IsCash { get; set; }

    [EnumDataType(typeof(CashAccountKind), ErrorMessage = "Seleccione un subtipo válido.")]
    [Display(Name = "Subtipo")]
    public CashAccountKind CashKind { get; set; } = CashAccountKind.Bank;

    [EnumDataType(typeof(CurrencyCode), ErrorMessage = "Seleccione una moneda válida.")]
    [Display(Name = "Moneda")]
    public CurrencyCode Currency { get; set; } = CurrencyCode.CRC;

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [ValidateNever]
    public IReadOnlyList<LedgerAccountParentOptionViewModel> ParentOptions { get; set; } = [];

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsCash && Type != LedgerAccountType.Asset)
        {
            yield return new ValidationResult(
                "Solo una cuenta de tipo Activo puede ser de efectivo.",
                [nameof(IsCash)]);
        }
    }
}

/// <summary>
/// Entrada para editar una cuenta contable; tipo, subtipo y moneda solo se muestran porque son
/// inmutables.
/// </summary>
public sealed class LedgerAccountEditViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Cuenta superior")]
    public Guid? ParentId { get; set; }

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    public LedgerAccountType Type { get; set; }

    public CashAccountKind? CashKind { get; set; }

    public CurrencyCode? Currency { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;

    [ValidateNever]
    public IReadOnlyList<LedgerAccountParentOptionViewModel> ParentOptions { get; set; } = [];
}
