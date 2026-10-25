using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Opción web para seleccionar una cuenta superior del mismo tipo.</summary>
public sealed record LedgerAccountParentOptionViewModel(
    Guid Id,
    string Label,
    LedgerAccountType Type);

/// <summary>
/// Entrada para crear una cuenta de detalle dentro de un grupo del catálogo base (regla 13). El
/// código y el tipo los calcula el servicio a partir de <see cref="ParentId"/> (regla 12); una cuenta
/// de efectivo indica su subtipo, que queda fijo después de crearla.
/// </summary>
public sealed class LedgerAccountCreateViewModel : IValidatableObject
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Cuenta superior")]
    public Guid ParentId { get; set; }

    [Display(Name = "Es una cuenta de efectivo (caja o banco)")]
    public bool IsCash { get; set; }

    [EnumDataType(typeof(CashAccountKind), ErrorMessage = "Seleccione un subtipo válido.")]
    [Display(Name = "Subtipo")]
    public CashAccountKind CashKind { get; set; } = CashAccountKind.Bank;

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
        // [Required] no detecta un Guid en su valor predeterminado: se valida a mano.
        if (ParentId == Guid.Empty)
        {
            yield return new ValidationResult(
                "La cuenta superior es obligatoria.",
                [nameof(ParentId)]);
        }
    }
}

/// <summary>
/// Entrada para editar una cuenta contable: solo nombre, referencia y descripción (regla 12). Código,
/// tipo, cuenta superior y subtipo se muestran de solo lectura.
/// </summary>
public sealed class LedgerAccountEditViewModel
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;

    [ValidateNever]
    public string Code { get; set; } = string.Empty;

    [ValidateNever]
    public LedgerAccountType Type { get; set; }

    [ValidateNever]
    public string? ParentName { get; set; }

    [ValidateNever]
    public CashAccountKind? CashKind { get; set; }

    [ValidateNever]
    public bool IsProtected { get; set; }
}
