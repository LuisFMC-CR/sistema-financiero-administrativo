namespace SistemaFinanciero.Application.Security.Users;

/// <summary>Datos requeridos para cambiar la contraseña propia.</summary>
public sealed record ChangeOwnPasswordCommand(
    string? CurrentPassword,
    string NewPassword);

/// <summary>Casos de uso de seguridad ejecutados por el propietario de una cuenta.</summary>
/// <remarks>
/// Conexiones: lo implementa <c>UserAccountService</c> (Infrastructure/Identity); lo consume
/// <c>AccountController.ChangePassword</c> (Web, ruta <c>/cuenta/cambiar-contraseña</c>). Es la vía que
/// usa cualquier usuario para cambiar su propia contraseña, a diferencia de
/// <see cref="IUserAdministrationService.ResetPasswordAsync"/>, que un Administrador usa sobre otra
/// cuenta.
/// </remarks>
public interface IUserAccountService
{
    /// <summary>
    /// Cambia la contraseña, elimina la obligación temporal y registra el evento de seguridad.
    /// </summary>
    public Task<UserOperationResult> ChangePasswordAsync(
        Guid userId,
        ChangeOwnPasswordCommand command,
        CancellationToken cancellationToken = default);
}
