using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Coordina los casos de uso administrativos de ASP.NET Core Identity sin exponer sus secretos.
/// </summary>
internal sealed class UserAdministrationService(
    FinancialDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : IUserAdministrationService
{
    private const int FullNameMaxLength = 150;
    private const int EmailMaxLength = 254;
    private const int PasswordMaxLength = 128;

    public async Task<PagedResult<UserModel>> SearchAsync(
        UserQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<ApplicationUser> users = dbContext.Users.AsNoTracking();

        users = Enum.IsDefined(query.Status)
            ? query.Status switch
            {
                UserStatusFilter.Active => users.Where(user => user.IsActive),
                UserStatusFilter.Inactive => users.Where(user => !user.IsActive),
                UserStatusFilter.All => users,
                _ => users.Where(user => user.IsActive),
            }
            : users.Where(user => user.IsActive);

        if (query.Search is not null)
        {
            string search = query.Search;
            users = users.Where(user =>
                user.FullName.Contains(search) ||
                user.Email!.Contains(search));
        }

        if (SystemRoles.IsDefined(query.Role))
        {
            string role = query.Role!;
            users = users.Where(user =>
                dbContext.UserRoles
                    .Join(
                        dbContext.Roles,
                        userRole => userRole.RoleId,
                        identityRole => identityRole.Id,
                        (userRole, identityRole) => new
                        {
                            userRole.UserId,
                            identityRole.Name,
                        })
                    .Any(assignment => assignment.UserId == user.Id && assignment.Name == role));
        }

        IQueryable<ApplicationUser> orderedUsers = users
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .ThenBy(user => user.Id);

        IQueryable<UserProjection> projected = orderedUsers.Select(user => new UserProjection(
            user.Id,
            user.FullName,
            user.Email!,
            dbContext.UserRoles
                .Where(userRole => userRole.UserId == user.Id)
                .Join(
                    dbContext.Roles,
                    userRole => userRole.RoleId,
                    identityRole => identityRole.Id,
                    (userRole, identityRole) => identityRole.Name!)
                .FirstOrDefault() ?? string.Empty,
            user.IsActive,
            user.MustChangePassword,
            user.LockoutEnd,
            user.ConcurrencyStamp ?? string.Empty));

        PagedResult<UserProjection> page = await CatalogPersistence.ToPageAsync(
            projected,
            query.Page,
            query.PageSize,
            cancellationToken);

        DateTimeOffset now = timeProvider.GetUtcNow();

        return new PagedResult<UserModel>(
            page.Items.Select(user => Map(user, now)).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<UserModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        UserProjection? user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == id)
            .Select(candidate => new UserProjection(
                candidate.Id,
                candidate.FullName,
                candidate.Email!,
                dbContext.UserRoles
                    .Where(userRole => userRole.UserId == candidate.Id)
                    .Join(
                        dbContext.Roles,
                        userRole => userRole.RoleId,
                        identityRole => identityRole.Id,
                        (userRole, identityRole) => identityRole.Name!)
                    .FirstOrDefault() ?? string.Empty,
                candidate.IsActive,
                candidate.MustChangePassword,
                candidate.LockoutEnd,
                candidate.ConcurrencyStamp ?? string.Empty))
            .SingleOrDefaultAsync(cancellationToken);

        return user is null ? null : Map(user, timeProvider.GetUtcNow());
    }

    public Task<UserOperationResult> CreateAsync(
        CreateUserCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return IdentityPersistence.ExecuteAsync(
            dbContext,
            async () =>
            {
                UserOperationResult? actorValidation = await ValidateActorAsync(
                    actorId,
                    cancellationToken);
                if (actorValidation is not null)
                {
                    return actorValidation;
                }

                if (!TryNormalizeProfile(
                        command.FullName,
                        command.Email,
                        command.Role,
                        out string fullName,
                        out string email,
                        out UserOperationResult? validation))
                {
                    return validation!;
                }

                if (string.IsNullOrWhiteSpace(command.TemporaryPassword) ||
                    command.TemporaryPassword.Length > PasswordMaxLength)
                {
                    return UserOperationResult.Invalid(
                        "La contraseña temporal es obligatoria y no puede superar 128 caracteres.");
                }

                ApplicationUser user = new()
                {
                    Id = Guid.NewGuid(),
                    FullName = fullName,
                    Email = email,
                    UserName = email,
                    EmailConfirmed = true,
                    IsActive = true,
                    MustChangePassword = true,
                };

                IdentityResult createResult = await userManager.CreateAsync(
                    user,
                    command.TemporaryPassword);
                UserOperationResult? createFailure = FromIdentityFailure(createResult);
                if (createFailure is not null)
                {
                    return createFailure;
                }

                IdentityResult roleResult = await userManager.AddToRoleAsync(user, command.Role);
                UserOperationResult? roleFailure = FromIdentityFailure(roleResult);
                if (roleFailure is not null)
                {
                    return roleFailure;
                }

                AddAudit(
                    SecurityAuditAction.UserCreated,
                    actorId,
                    user.Id,
                    command.Role);
                await dbContext.SaveChangesAsync(cancellationToken);

                return UserOperationResult.Succeeded();
            },
            cancellationToken);
    }

    public Task<UserOperationResult> UpdateAsync(
        Guid id,
        UpdateUserCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return IdentityPersistence.ExecuteAsync(
            dbContext,
            async () =>
            {
                UserOperationResult? actorValidation = await ValidateActorAsync(
                    actorId,
                    cancellationToken);
                if (actorValidation is not null)
                {
                    return actorValidation;
                }

                if (!TryNormalizeProfile(
                        command.FullName,
                        command.Email,
                        command.Role,
                        out string fullName,
                        out string email,
                        out UserOperationResult? validation))
                {
                    return validation!;
                }

                ApplicationUser? user = await dbContext.Users
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (user is null)
                {
                    return UserOperationResult.Missing();
                }

                if (!HasCurrentVersion(user, version))
                {
                    return UserOperationResult.Concurrent();
                }

                string? currentRole = await GetSingleRoleAsync(user.Id, cancellationToken);
                if (currentRole is null)
                {
                    return UserOperationResult.Invalid(
                        "La cuenta no posee exactamente un rol aprobado.");
                }

                bool roleChanged = !string.Equals(
                    currentRole,
                    command.Role,
                    StringComparison.Ordinal);

                if (roleChanged &&
                    string.Equals(currentRole, SystemRoles.Administrator, StringComparison.Ordinal) &&
                    user.IsActive &&
                    await CountActiveAdministratorsAsync(cancellationToken) <= 1)
                {
                    return LastAdministratorResult();
                }

                if (user.Id == actorId && roleChanged)
                {
                    return UserOperationResult.Protected(
                        "No puede cambiar el rol de su propia cuenta.");
                }

                bool emailChanged = !string.Equals(
                    user.Email,
                    email,
                    StringComparison.OrdinalIgnoreCase);
                bool profileChanged =
                    !string.Equals(user.FullName, fullName, StringComparison.Ordinal) ||
                    emailChanged;

                if (!profileChanged && !roleChanged)
                {
                    return UserOperationResult.Succeeded();
                }

                user.FullName = fullName;
                user.Email = email;
                user.UserName = email;

                IdentityResult updateResult = await userManager.UpdateAsync(user);
                UserOperationResult? updateFailure = FromIdentityFailure(updateResult);
                if (updateFailure is not null)
                {
                    return updateFailure;
                }

                if (roleChanged)
                {
                    IdentityResult removeResult = await userManager.RemoveFromRoleAsync(
                        user,
                        currentRole);
                    UserOperationResult? removeFailure = FromIdentityFailure(removeResult);
                    if (removeFailure is not null)
                    {
                        return removeFailure;
                    }

                    IdentityResult addResult = await userManager.AddToRoleAsync(user, command.Role);
                    UserOperationResult? addFailure = FromIdentityFailure(addResult);
                    if (addFailure is not null)
                    {
                        return addFailure;
                    }
                }

                if (roleChanged || emailChanged)
                {
                    IdentityResult stampResult = await userManager.UpdateSecurityStampAsync(user);
                    UserOperationResult? stampFailure = FromIdentityFailure(stampResult);
                    if (stampFailure is not null)
                    {
                        return stampFailure;
                    }
                }

                if (profileChanged)
                {
                    AddAudit(SecurityAuditAction.UserProfileUpdated, actorId, user.Id);
                }

                if (roleChanged)
                {
                    AddAudit(SecurityAuditAction.UserRoleChanged, actorId, user.Id, command.Role);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
                return UserOperationResult.Succeeded();
            },
            cancellationToken);
    }

    public Task<UserOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return IdentityPersistence.ExecuteAsync(
            dbContext,
            async () =>
            {
                UserOperationResult? actorValidation = await ValidateActorAsync(
                    actorId,
                    cancellationToken);
                if (actorValidation is not null)
                {
                    return actorValidation;
                }

                ApplicationUser? user = await dbContext.Users
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (user is null)
                {
                    return UserOperationResult.Missing();
                }

                if (!HasCurrentVersion(user, version))
                {
                    return UserOperationResult.Concurrent();
                }

                if (user.IsActive == isActive)
                {
                    return UserOperationResult.Succeeded();
                }

                string? role = await GetSingleRoleAsync(user.Id, cancellationToken);
                if (role is null)
                {
                    return UserOperationResult.Invalid(
                        "La cuenta no posee exactamente un rol aprobado.");
                }

                if (!isActive &&
                    string.Equals(role, SystemRoles.Administrator, StringComparison.Ordinal) &&
                    await CountActiveAdministratorsAsync(cancellationToken) <= 1)
                {
                    return LastAdministratorResult();
                }

                if (user.Id == actorId && !isActive)
                {
                    return UserOperationResult.Protected(
                        "No puede desactivar su propia cuenta.");
                }

                user.IsActive = isActive;
                IdentityResult updateResult = await userManager.UpdateAsync(user);
                UserOperationResult? updateFailure = FromIdentityFailure(updateResult);
                if (updateFailure is not null)
                {
                    return updateFailure;
                }

                IdentityResult stampResult = await userManager.UpdateSecurityStampAsync(user);
                UserOperationResult? stampFailure = FromIdentityFailure(stampResult);
                if (stampFailure is not null)
                {
                    return stampFailure;
                }

                AddAudit(
                    isActive
                        ? SecurityAuditAction.UserActivated
                        : SecurityAuditAction.UserDeactivated,
                    actorId,
                    user.Id);
                await dbContext.SaveChangesAsync(cancellationToken);

                return UserOperationResult.Succeeded();
            },
            cancellationToken);
    }

    public Task<UserOperationResult> ResetPasswordAsync(
        Guid id,
        ResetUserPasswordCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return IdentityPersistence.ExecuteAsync(
            dbContext,
            async () =>
            {
                UserOperationResult? actorValidation = await ValidateActorAsync(
                    actorId,
                    cancellationToken);
                if (actorValidation is not null)
                {
                    return actorValidation;
                }

                if (string.IsNullOrWhiteSpace(command.TemporaryPassword) ||
                    command.TemporaryPassword.Length > PasswordMaxLength)
                {
                    return UserOperationResult.Invalid(
                        "La contraseña temporal es obligatoria y no puede superar 128 caracteres.");
                }

                ApplicationUser? user = await dbContext.Users
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (user is null)
                {
                    return UserOperationResult.Missing();
                }

                if (!HasCurrentVersion(user, version))
                {
                    return UserOperationResult.Concurrent();
                }

                if (user.Id == actorId)
                {
                    return UserOperationResult.Protected(
                        "Utilice el cambio de contraseña personal para su propia cuenta.");
                }

                string token = await userManager.GeneratePasswordResetTokenAsync(user);
                user.MustChangePassword = true;

                IdentityResult resetResult = await userManager.ResetPasswordAsync(
                    user,
                    token,
                    command.TemporaryPassword);
                UserOperationResult? resetFailure = FromIdentityFailure(resetResult);
                if (resetFailure is not null)
                {
                    return resetFailure;
                }

                IdentityResult lockoutResult = await userManager.SetLockoutEndDateAsync(user, null);
                UserOperationResult? lockoutFailure = FromIdentityFailure(lockoutResult);
                if (lockoutFailure is not null)
                {
                    return lockoutFailure;
                }

                IdentityResult attemptsResult = await userManager.ResetAccessFailedCountAsync(user);
                UserOperationResult? attemptsFailure = FromIdentityFailure(attemptsResult);
                if (attemptsFailure is not null)
                {
                    return attemptsFailure;
                }

                AddAudit(SecurityAuditAction.PasswordReset, actorId, user.Id);
                await dbContext.SaveChangesAsync(cancellationToken);

                return UserOperationResult.Succeeded();
            },
            cancellationToken);
    }

    public Task<UserOperationResult> UnlockAsync(
        Guid id,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        return IdentityPersistence.ExecuteAsync(
            dbContext,
            async () =>
            {
                UserOperationResult? actorValidation = await ValidateActorAsync(
                    actorId,
                    cancellationToken);
                if (actorValidation is not null)
                {
                    return actorValidation;
                }

                ApplicationUser? user = await dbContext.Users
                    .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
                if (user is null)
                {
                    return UserOperationResult.Missing();
                }

                if (!HasCurrentVersion(user, version))
                {
                    return UserOperationResult.Concurrent();
                }

                if (user.LockoutEnd is null && user.AccessFailedCount == 0)
                {
                    return UserOperationResult.Succeeded();
                }

                IdentityResult lockoutResult = await userManager.SetLockoutEndDateAsync(user, null);
                UserOperationResult? lockoutFailure = FromIdentityFailure(lockoutResult);
                if (lockoutFailure is not null)
                {
                    return lockoutFailure;
                }

                IdentityResult attemptsResult = await userManager.ResetAccessFailedCountAsync(user);
                UserOperationResult? attemptsFailure = FromIdentityFailure(attemptsResult);
                if (attemptsFailure is not null)
                {
                    return attemptsFailure;
                }

                AddAudit(SecurityAuditAction.UserUnlocked, actorId, user.Id);
                await dbContext.SaveChangesAsync(cancellationToken);

                return UserOperationResult.Succeeded();
            },
            cancellationToken);
    }

    private async Task<UserOperationResult?> ValidateActorAsync(
        Guid actorId,
        CancellationToken cancellationToken)
    {
        if (actorId == Guid.Empty)
        {
            return UserOperationResult.Protected("El administrador autenticado no es válido.");
        }

        bool isActiveAdministrator = await dbContext.Users
            .AsNoTracking()
            .Where(user =>
                user.Id == actorId &&
                user.IsActive &&
                !user.MustChangePassword)
            .AnyAsync(user =>
                dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(
                        dbContext.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (userRole, role) => role.Name)
                    .Any(role => role == SystemRoles.Administrator),
                cancellationToken);

        return isActiveAdministrator
            ? null
            : UserOperationResult.Protected(
                "Solo un Administrador activo y sin cambio de contraseña pendiente puede realizar esta operación.");
    }

    private async Task<string?> GetSingleRoleAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        string[] roles = await dbContext.UserRoles
            .Where(userRole => userRole.UserId == userId)
            .Join(
                dbContext.Roles,
                userRole => userRole.RoleId,
                role => role.Id,
                (userRole, role) => role.Name!)
            .ToArrayAsync(cancellationToken);

        return roles.Length == 1 && SystemRoles.IsDefined(roles[0])
            ? roles[0]
            : null;
    }

    private Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken)
    {
        return dbContext.Users
            .Where(user => user.IsActive)
            .CountAsync(user =>
                dbContext.UserRoles
                    .Where(userRole => userRole.UserId == user.Id)
                    .Join(
                        dbContext.Roles,
                        userRole => userRole.RoleId,
                        role => role.Id,
                        (userRole, role) => role.Name)
                    .Any(role => role == SystemRoles.Administrator),
                cancellationToken);
    }

    private void AddAudit(
        SecurityAuditAction action,
        Guid actorId,
        Guid targetId,
        string? role = null)
    {
        dbContext.SecurityAuditEvents.Add(new SecurityAuditEvent(
            Guid.NewGuid(),
            action,
            actorId,
            targetId,
            timeProvider.GetUtcNow(),
            role));
    }

    private static bool TryNormalizeProfile(
        string fullNameValue,
        string emailValue,
        string roleValue,
        out string fullName,
        out string email,
        out UserOperationResult? validation)
    {
        fullName = fullNameValue?.Trim() ?? string.Empty;
        email = emailValue?.Trim() ?? string.Empty;

        if (fullName.Length == 0 || fullName.Length > FullNameMaxLength)
        {
            validation = UserOperationResult.Invalid(
                "El nombre completo es obligatorio y no puede superar 150 caracteres.");
            return false;
        }

        if (email.Length == 0 || email.Length > EmailMaxLength)
        {
            validation = UserOperationResult.Invalid(
                "El correo electrónico es obligatorio y no puede superar 254 caracteres.");
            return false;
        }

        if (!SystemRoles.IsDefined(roleValue))
        {
            validation = UserOperationResult.Invalid("Seleccione un rol permitido.");
            return false;
        }

        validation = null;
        return true;
    }

    private static bool HasCurrentVersion(ApplicationUser user, string version)
    {
        return !string.IsNullOrWhiteSpace(version) &&
            string.Equals(user.ConcurrencyStamp, version, StringComparison.Ordinal);
    }

    private static UserOperationResult LastAdministratorResult()
    {
        return UserOperationResult.Protected(
            "Debe permanecer al menos un Administrador activo.");
    }

    private static UserOperationResult? FromIdentityFailure(IdentityResult result)
    {
        if (result.Succeeded)
        {
            return null;
        }

        IdentityError[] errors = result.Errors.ToArray();

        if (errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
        {
            return UserOperationResult.Duplicated();
        }

        if (errors.Any(error => error.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            return UserOperationResult.Concurrent();
        }

        string message = string.Join(
            " ",
            errors.Select(error => error.Description).Distinct(StringComparer.Ordinal));

        return UserOperationResult.Invalid(
            string.IsNullOrWhiteSpace(message)
                ? "Identity rechazó la operación solicitada."
                : message);
    }

    private static UserModel Map(UserProjection user, DateTimeOffset now)
    {
        return new UserModel(
            user.Id,
            user.FullName,
            user.Email,
            user.Role,
            user.IsActive,
            user.MustChangePassword,
            user.LockoutEndUtc is not null && user.LockoutEndUtc > now,
            user.LockoutEndUtc,
            user.Version);
    }

    private sealed record UserProjection(
        Guid Id,
        string FullName,
        string Email,
        string Role,
        bool IsActive,
        bool MustChangePassword,
        DateTimeOffset? LockoutEndUtc,
        string Version);
}
