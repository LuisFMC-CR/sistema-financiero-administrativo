using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>Administra cambios de seguridad iniciados por el propietario de la cuenta.</summary>
internal sealed class UserAccountService(
    FinancialDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IUserAccountService
{
    public Task<UserOperationResult> ChangePasswordAsync(
        Guid userId,
        ChangeOwnPasswordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return IdentityPersistence.ExecuteAsync(
            dbContext,
            async () =>
            {
                if (userId == Guid.Empty)
                {
                    return UserOperationResult.Missing();
                }

                ApplicationUser? user = await dbContext.Users
                    .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

                if (user is null || !user.IsActive)
                {
                    return UserOperationResult.Missing();
                }

                if (string.IsNullOrWhiteSpace(command.NewPassword) ||
                    command.NewPassword.Length > 128)
                {
                    return UserOperationResult.Invalid(
                        "La nueva contraseña es obligatoria y no puede superar 128 caracteres.");
                }

                IdentityResult result;
                if (user.MustChangePassword)
                {
                    string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                    result = await userManager.ResetPasswordAsync(
                        user,
                        resetToken,
                        command.NewPassword);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(command.CurrentPassword))
                    {
                        return UserOperationResult.Invalid("La contraseña actual es obligatoria.");
                    }

                    result = await userManager.ChangePasswordAsync(
                        user,
                        command.CurrentPassword,
                        command.NewPassword);
                }

                if (!result.Succeeded)
                {
                    string message = string.Join(
                        " ",
                        result.Errors
                            .Select(error => error.Description)
                            .Distinct(StringComparer.Ordinal));

                    return UserOperationResult.Invalid(
                        string.IsNullOrWhiteSpace(message)
                            ? "No fue posible cambiar la contraseña."
                            : message);
                }

                user.MustChangePassword = false;
                dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent(
                    Guid.NewGuid(),
                    SecurityAuditAction.PasswordChanged,
                    user.Id,
                    user.Id,
                    timeProvider.GetUtcNow()));
                await dbContext.SaveChangesAsync(cancellationToken);

                return UserOperationResult.Succeeded();
            },
            cancellationToken);
    }
}
