using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Crea los roles fijos y el primer administrador mediante una operación explícita.
/// </summary>
public sealed partial class IdentityBootstrapper(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    FinancialDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<IdentityBootstrapper> logger)
{
    /// <summary>
    /// Inicializa la seguridad sin incluir credenciales predeterminadas en el código.
    /// </summary>
    /// <remarks>
    /// Los roles, el usuario y su asignación se confirman en una sola transacción. Cualquier error
    /// revierte el bootstrap completo y permite repetirlo de forma segura.
    /// </remarks>
    /// <param name="configuration">
    /// Configuración que debe contener nombre, correo y contraseña bajo <c>BootstrapAdmin</c>.
    /// </param>
    /// <param name="cancellationToken">Token para cancelar la operación.</param>
    /// <exception cref="InvalidOperationException">
    /// Se produce cuando falta configuración o Identity rechaza la creación requerida.
    /// </exception>
    public async Task ExecuteAsync(
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        await using IDbContextTransaction transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);

        foreach (string roleName in SystemRoles.All)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            IdentityResult createRoleResult = await roleManager.CreateAsync(
                new IdentityRole<Guid>(roleName));

            EnsureSucceeded(createRoleResult, $"crear el rol {roleName}");
        }

        IList<ApplicationUser> administrators = await userManager.GetUsersInRoleAsync(
            SystemRoles.Administrator);

        if (administrators.Any(user => user.IsActive))
        {
            await transaction.CommitAsync(cancellationToken);
            LogInitializationSkipped(logger);
            return;
        }

        string fullName = RequiredValue(configuration, "BootstrapAdmin:FullName");
        string email = RequiredValue(configuration, "BootstrapAdmin:Email");
        string password = RequiredValue(
            configuration,
            "BootstrapAdmin:Password",
            trimValue: false);

        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        if (user is not null)
        {
            throw new InvalidOperationException(
                "El correo de bootstrap ya pertenece a una cuenta. No se modificaron sus privilegios.");
        }

        user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            FullName = fullName,
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = false,
        };

        IdentityResult createResult = await userManager.CreateAsync(user, password);
        EnsureSucceeded(createResult, "crear el administrador inicial");

        IdentityResult assignRoleResult = await userManager.AddToRoleAsync(
            user,
            SystemRoles.Administrator);
        EnsureSucceeded(assignRoleResult, "asignar el rol Administrador");

        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent(
            Guid.NewGuid(),
            SecurityAuditAction.BootstrapAdministratorCreated,
            actorUserId: null,
            user.Id,
            timeProvider.GetUtcNow(),
            SystemRoles.Administrator));
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        LogInitializationCompleted(logger);
    }

    private static string RequiredValue(
        IConfiguration configuration,
        string key,
        bool trimValue = true)
    {
        string? value = configuration[key];

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Falta la configuración protegida requerida: {key}.");
        }

        return trimValue ? value.Trim() : value;
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        string errors = string.Join(
            "; ",
            result.Errors.Select(error => $"{error.Code}: {error.Description}"));

        throw new InvalidOperationException(
            $"Identity no pudo {operation}. {errors}");
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "La inicialización explícita de identidad finalizó correctamente. Retire los secretos de bootstrap.")]
    private static partial void LogInitializationCompleted(ILogger logger);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "La inicialización de identidad no realizó cambios porque ya existe un administrador activo.")]
    private static partial void LogInitializationSkipped(ILogger logger);
}
