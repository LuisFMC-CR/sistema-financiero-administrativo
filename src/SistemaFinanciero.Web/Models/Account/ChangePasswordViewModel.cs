using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Account;

/// <summary>
/// Datos requeridos para cambiar la contraseña del usuario autenticado.
/// </summary>
public sealed class ChangePasswordViewModel
{
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña actual")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "La nueva contraseña es obligatoria.")]
    [StringLength(
        128,
        MinimumLength = 12,
        ErrorMessage = "La nueva contraseña debe contener al menos {2} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la nueva contraseña.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(NewPassword),
        ErrorMessage = "La confirmación no coincide con la nueva contraseña.")]
    [Display(Name = "Confirmar nueva contraseña")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
