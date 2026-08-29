using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Entrada para crear una cuenta financiera de moneda fija.</summary>
public sealed class FinancialAccountCreateViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(FinancialAccountType), ErrorMessage = "Seleccione un tipo válido.")]
    [Display(Name = "Tipo")]
    public FinancialAccountType Type { get; set; } = FinancialAccountType.Bank;

    [EnumDataType(typeof(CurrencyCode), ErrorMessage = "Seleccione una moneda válida.")]
    [Display(Name = "Moneda")]
    public CurrencyCode Currency { get; set; } = CurrencyCode.CRC;

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }
}

/// <summary>
/// Entrada para editar una cuenta; tipo y moneda solo se muestran porque son inmutables.
/// </summary>
public sealed class FinancialAccountEditViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "La referencia no puede superar 100 caracteres.")]
    [Display(Name = "Referencia")]
    public string? Reference { get; set; }

    public FinancialAccountType Type { get; set; }

    public CurrencyCode Currency { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;
}

