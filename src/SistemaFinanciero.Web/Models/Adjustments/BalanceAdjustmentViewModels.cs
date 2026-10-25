using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using SistemaFinanciero.Domain.Adjustments;

namespace SistemaFinanciero.Web.Models.Adjustments;

/// <summary>
/// Entrada para ajustar manualmente el saldo de una cuenta por cobrar o por pagar (regla de negocio
/// 4). El mismo modelo sirve para ambos lados; lo que cambia es la ruta a la que se publica.
/// </summary>
public sealed class BalanceAdjustmentFormViewModel
{
    [ValidateNever]
    public string AccountLabel { get; set; } = string.Empty;

    [ValidateNever]
    public decimal CurrentBalance { get; set; }

    [ValidateNever]
    public decimal OriginalAmount { get; set; }

    [Range(0.01, 999_999_999_999.99, ErrorMessage = "El monto debe ser mayor que cero.")]
    [Display(Name = "Monto del ajuste")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "El sentido del ajuste es obligatorio.")]
    [Display(Name = "Sentido")]
    public BalanceAdjustmentDirection Direction { get; set; } = BalanceAdjustmentDirection.Decrease;

    [Required(ErrorMessage = "El motivo del ajuste es obligatorio.")]
    [StringLength(300, ErrorMessage = "El motivo no puede superar 300 caracteres.")]
    [Display(Name = "Motivo del ajuste")]
    public string Reason { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión del registro.")]
    public string Version { get; set; } = string.Empty;
}
