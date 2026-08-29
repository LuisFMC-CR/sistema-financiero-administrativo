using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Catalogs;

/// <summary>Entrada para registrar o corregir un tipo de cambio diario.</summary>
public class DailyExchangeRateInputViewModel : IValidatableObject
{
    [DataType(DataType.Date)]
    [Display(Name = "Fecha efectiva")]
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Range(typeof(decimal), "0.000001", "999999999999.999999", ErrorMessage = "El tipo de cambio debe ser mayor que cero.")]
    [Display(Name = "CRC por USD")]
    public decimal CrcPerUsd { get; set; }

    [Required(ErrorMessage = "La fuente es obligatoria.")]
    [StringLength(100, ErrorMessage = "La fuente no puede superar 100 caracteres.")]
    [Display(Name = "Fuente")]
    public string Source { get; set; } = string.Empty;

    [StringLength(300, ErrorMessage = "Las observaciones no pueden superar 300 caracteres.")]
    [Display(Name = "Observaciones")]
    public string? Notes { get; set; }

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EffectiveDate == default)
        {
            yield return new ValidationResult(
                "La fecha efectiva es obligatoria.",
                [nameof(EffectiveDate)]);
        }
    }
}

/// <summary>Entrada para corregir una tasa con concurrencia optimista.</summary>
public sealed class DailyExchangeRateEditViewModel : DailyExchangeRateInputViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión del tipo de cambio.")]
    public string Version { get; set; } = string.Empty;
}
