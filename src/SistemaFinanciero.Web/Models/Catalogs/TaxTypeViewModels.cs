using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Entrada para crear una tarifa de IVA.</summary>
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

    [Range(
        typeof(decimal),
        "0",
        "999999999999.9999",
        ErrorMessage = "La tarifa no puede ser negativa.",
        ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    [Display(Name = "Tarifa")]
    public decimal Rate { get; set; }

    [Required(ErrorMessage = "El código de tarifa de Hacienda es obligatorio.")]
    [StringLength(2, MinimumLength = 2, ErrorMessage = "El código de Hacienda debe tener exactamente 2 dígitos.")]
    [RegularExpression(@"^\d{2}$", ErrorMessage = "El código de Hacienda debe tener exactamente 2 dígitos.")]
    [Display(Name = "Código de tarifa de Hacienda")]
    public string HaciendaRateCode { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "La descripción no puede superar 300 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }
}
