using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security.Users;

namespace SistemaFinanciero.Web.Models.Users;

/// <summary>Datos requeridos para presentar el listado administrativo de usuarios.</summary>
public sealed record UserIndexViewModel(
    PagedResult<UserModel> Results,
    string? Search,
    UserStatusFilter Status,
    string? Role,
    IReadOnlyCollection<string> Roles,
    Guid CurrentUserId);

/// <summary>Datos del formulario de filtros del listado de usuarios.</summary>
public sealed record UserFilterViewModel(
    string? Search,
    UserStatusFilter Status,
    string? Role,
    IReadOnlyCollection<string> Roles);

/// <summary>Datos para conservar los filtros al cambiar de página.</summary>
public sealed record UserPaginationViewModel(
    int CurrentPage,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage,
    string? Search,
    UserStatusFilter Status,
    string? Role);

/// <summary>Campos administrativos comunes para crear o editar una cuenta interna.</summary>
public abstract class UserInputViewModel
{
    [Required(ErrorMessage = "El nombre completo es obligatorio.")]
    [StringLength(150, ErrorMessage = "El nombre completo no puede superar 150 caracteres.")]
    [Display(Name = "Nombre completo")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo electrónico es obligatorio.")]
    [StringLength(254, ErrorMessage = "El correo electrónico no puede superar 254 caracteres.")]
    [EmailAddress(ErrorMessage = "Ingrese un correo electrónico válido.")]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "El rol es obligatorio.")]
    [StringLength(50, ErrorMessage = "El rol no puede superar 50 caracteres.")]
    [Display(Name = "Rol")]
    public string Role { get; set; } = string.Empty;
}

/// <summary>Entrada cerrada para crear una cuenta con contraseña temporal.</summary>
public sealed class UserCreateViewModel : UserInputViewModel
{
    [Required(ErrorMessage = "La contraseña temporal es obligatoria.")]
    [StringLength(
        128,
        MinimumLength = 12,
        ErrorMessage = "La contraseña temporal debe contener entre {2} y {1} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Contraseña temporal")]
    public string TemporaryPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña temporal.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(TemporaryPassword),
        ErrorMessage = "La confirmación no coincide con la contraseña temporal.")]
    [Display(Name = "Confirmar contraseña temporal")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

/// <summary>Entrada cerrada para editar los datos permitidos de una cuenta.</summary>
public sealed class UserEditViewModel : UserInputViewModel
{
    /// <summary>Permite que la vista evite ofrecer un cambio de rol sobre la cuenta propia.</summary>
    [BindNever]
    public bool IsCurrentUser { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Entrada para activar o desactivar una cuenta con concurrencia optimista.</summary>
public sealed class UserStatusViewModel
{
    [Required(ErrorMessage = "No fue posible validar el estado solicitado.")]
    public bool? IsActive { get; set; }

    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Entrada para una operación que solo requiere la versión vigente.</summary>
public sealed class UserVersionViewModel
{
    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;
}

/// <summary>Entrada cerrada para asignar una contraseña temporal a otra cuenta.</summary>
public sealed class UserResetPasswordViewModel
{
    /// <summary>Nombre informativo obtenido nuevamente desde el servidor.</summary>
    [BindNever]
    public string TargetFullName { get; set; } = string.Empty;

    /// <summary>Correo informativo obtenido nuevamente desde el servidor.</summary>
    [BindNever]
    public string TargetEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña temporal es obligatoria.")]
    [StringLength(
        128,
        MinimumLength = 12,
        ErrorMessage = "La contraseña temporal debe contener entre {2} y {1} caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nueva contraseña temporal")]
    public string TemporaryPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme la contraseña temporal.")]
    [DataType(DataType.Password)]
    [Compare(
        nameof(TemporaryPassword),
        ErrorMessage = "La confirmación no coincide con la contraseña temporal.")]
    [Display(Name = "Confirmar contraseña temporal")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "No fue posible validar la versión de la cuenta.")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Elimina valores sensibles antes de volver a presentar el formulario.</summary>
    public void ClearPasswords()
    {
        TemporaryPassword = string.Empty;
        ConfirmPassword = string.Empty;
    }
}

/// <summary>Traduce estados técnicos de usuario a etiquetas de interfaz.</summary>
public static class UserDisplayExtensions
{
    /// <summary>Obtiene el nombre visible de un filtro de estado.</summary>
    public static string ToDisplayName(this UserStatusFilter status)
    {
        return status switch
        {
            UserStatusFilter.Active => "Activos",
            UserStatusFilter.Inactive => "Inactivos",
            UserStatusFilter.All => "Todos",
            _ => status.ToString(),
        };
    }
}
