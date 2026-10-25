using System.ComponentModel.DataAnnotations;

namespace SistemaFinanciero.Web.Models.Account;

/// <summary>
/// Datos necesarios para autenticar a un usuario interno.
/// </summary>
/// <remarks>
/// Conexiones: la recibe <c>AccountController.Login</c>, que usa <c>SignInManager&lt;ApplicationUser&gt;</c>
/// de Identity directamente (no pasa por <c>IUserAccountService</c>). La vista es
/// <c>Views/Account/Login.cshtml</c>.
/// </remarks>
public sealed class LoginViewModel
{
    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; init; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña")]
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Ruta interna solicitada antes de iniciar sesión.
    /// </summary>
    public string? ReturnUrl { get; init; }
}
