using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Entrada para crear un tipo de impuesto de método de cálculo fijo.</summary>
public sealed class TaxTypeCreateViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [EnumDataType(typeof(TaxCalculationType), ErrorMessage = "Seleccione un método de cálculo válido.")]
    [Display(Name = "Método de cálculo")]
    public TaxCalculationType CalculationType { get; set; } = TaxCalculationType.Percentage;

    [Range(
        typeof(decimal),
        "0",
        "999999999999.9999",
        ErrorMessage = "La tarifa no puede ser negativa.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Tarifa")]
    public decimal Rate { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }
}

/// <summary>
/// Entrada para editar un tipo de impuesto; el método de cálculo solo se muestra porque es inmutable.
/// </summary>
public sealed class TaxTypeEditViewModel
{
    [Required(ErrorMessage = "El código es obligatorio.")]
    [StringLength(30, ErrorMessage = "El código no puede superar 30 caracteres.")]
    [Display(Name = "Código")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(120, ErrorMessage = "El nombre no puede superar 120 caracteres.")]
    [Display(Name = "Nombre")]
    public string Name { get; set; } = string.Empty;

    [Range(
        typeof(decimal),
        "0",
        "999999999999.9999",
        ErrorMessage = "La tarifa no puede ser negativa.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Tarifa")]
    public decimal Rate { get; set; }

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    public TaxCalculationType CalculationType { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión del tipo de impuesto.")]
    public string Version { get; set; } = string.Empty;
}
