namespace SistemaFinanciero.Application.Security.Users;

/// <summary>Datos requeridos para cambiar la contraseña propia.</summary>
public sealed record ChangeOwnPasswordCommand(
    string CurrentPassword,
    string NewPassword);

/// <summary>Casos de uso de seguridad ejecutados por el propietario de una cuenta.</summary>
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
