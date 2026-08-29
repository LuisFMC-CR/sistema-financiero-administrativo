using Microsoft.AspNetCore.Identity;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Traduce los errores esperados de Identity a mensajes comprensibles para la interfaz en español.
/// </summary>
internal sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DefaultError() => Error(
        nameof(DefaultError),
        "No fue posible completar la operación de seguridad.");

    public override IdentityError ConcurrencyFailure() => Error(
        nameof(ConcurrencyFailure),
        "La cuenta fue modificada por otro usuario.");

    public override IdentityError PasswordMismatch() => Error(
        nameof(PasswordMismatch),
        "La contraseña actual es incorrecta.");

    public override IdentityError InvalidToken() => Error(
        nameof(InvalidToken),
        "El token de seguridad no es válido.");

    public override IdentityError InvalidUserName(string? userName) => Error(
        nameof(InvalidUserName),
        "El correo no puede utilizarse como nombre de usuario.");

    public override IdentityError InvalidEmail(string? email) => Error(
        nameof(InvalidEmail),
        "El correo electrónico no tiene un formato válido.");

    public override IdentityError DuplicateUserName(string userName) => Error(
        nameof(DuplicateUserName),
        "Ya existe una cuenta con el mismo correo electrónico.");

    public override IdentityError DuplicateEmail(string email) => Error(
        nameof(DuplicateEmail),
        "Ya existe una cuenta con el mismo correo electrónico.");

    public override IdentityError InvalidRoleName(string? role) => Error(
        nameof(InvalidRoleName),
        "El rol indicado no es válido.");

    public override IdentityError DuplicateRoleName(string role) => Error(
        nameof(DuplicateRoleName),
        "El rol indicado ya existe.");

    public override IdentityError UserAlreadyHasPassword() => Error(
        nameof(UserAlreadyHasPassword),
        "La cuenta ya posee una contraseña.");

    public override IdentityError UserLockoutNotEnabled() => Error(
        nameof(UserLockoutNotEnabled),
        "La cuenta no admite bloqueo temporal.");

    public override IdentityError UserAlreadyInRole(string role) => Error(
        nameof(UserAlreadyInRole),
        "La cuenta ya tiene asignado ese rol.");

    public override IdentityError UserNotInRole(string role) => Error(
        nameof(UserNotInRole),
        "La cuenta no tiene asignado ese rol.");

    public override IdentityError PasswordTooShort(int length) => Error(
        nameof(PasswordTooShort),
        $"La contraseña debe contener al menos {length} caracteres.");

    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(
        nameof(PasswordRequiresUniqueChars),
        $"La contraseña debe contener al menos {uniqueChars} caracteres diferentes.");

    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(
        nameof(PasswordRequiresNonAlphanumeric),
        "La contraseña debe contener al menos un carácter especial.");

    public override IdentityError PasswordRequiresDigit() => Error(
        nameof(PasswordRequiresDigit),
        "La contraseña debe contener al menos un número.");

    public override IdentityError PasswordRequiresLower() => Error(
        nameof(PasswordRequiresLower),
        "La contraseña debe contener al menos una letra minúscula.");

    public override IdentityError PasswordRequiresUpper() => Error(
        nameof(PasswordRequiresUpper),
        "La contraseña debe contener al menos una letra mayúscula.");

    private static IdentityError Error(string code, string description) => new()
    {
        Code = code,
        Description = description,
    };
}
