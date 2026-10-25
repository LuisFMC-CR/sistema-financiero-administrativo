using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Account;

/// <summary>
/// Datos requeridos para cambiar la contraseña del usuario autenticado.
/// </summary>
/// <remarks>
/// Conexiones: la recibe <c>AccountController.ChangePassword</c>, que la convierte en
/// <c>ChangeOwnPasswordCommand</c> para <c>IUserAccountService.ChangePasswordAsync</c> (Application).
/// <c>CurrentPassword</c> no es obligatorio aquí porque una cuenta con contraseña temporal no la
/// necesita; <c>UserAccountService</c> es quien decide si exigirla.
/// </remarks>
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
