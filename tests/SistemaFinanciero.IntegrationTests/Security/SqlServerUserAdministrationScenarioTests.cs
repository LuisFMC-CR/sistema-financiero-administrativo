using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Security;

/// <summary>
/// Escenarios reales de Identity que solo se habilitan explícitamente y siempre revierten sus datos.
/// </summary>
public sealed class SqlServerUserAdministrationScenarioTests
    : IClassFixture<FinancialWebApplicationFactory>
{
    private const string EnableVariable = "SISTEMA_FINANCIERO_RUN_SQL_TESTS";
    private const string InitialAdministratorPassword = "Admin!Test1234";
    private readonly FinancialWebApplicationFactory factory;

    public SqlServerUserAdministrationScenarioTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task CreateSearchAndEdit_KeepOneRoleAndRejectStaleOrDuplicateData()
    {
        if (!SqlTestsEnabled())
        {
            return;
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FinancialDbContext dbContext = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            TestSecurityContext setup = await CreateSecurityContextAsync(scope.ServiceProvider);
            IUserAdministrationService service = scope.ServiceProvider
                .GetRequiredService<IUserAdministrationService>();
            string suffix = Guid.NewGuid().ToString("N")[..8];
            string email = $"finance-{suffix}@example.test";
            const string temporaryPassword = "Temporal!123Aa";

            UserOperationResult createResult = await service.CreateAsync(
                new CreateUserCommand(
                    "Persona de Finanzas",
                    email,
                    SystemRoles.Finance,
                    temporaryPassword),
                setup.Actor.Id);

            Assert.True(createResult.IsSuccess, createResult.Message);

            dbContext.ChangeTracker.Clear();
            ApplicationUser created = await dbContext.Users.SingleAsync(user => user.Email == email);
            Assert.True(created.IsActive);
            Assert.True(created.MustChangePassword);
            Assert.NotEqual(temporaryPassword, created.PasswordHash);
            Assert.True(await setup.UserManager.CheckPasswordAsync(created, temporaryPassword));
            Assert.Equal(
                [SystemRoles.Finance],
                await setup.UserManager.GetRolesAsync(created));

            SecurityAuditEvent createdEvent = await dbContext.SecurityAuditEvents
                .AsNoTracking()
                .SingleAsync(auditEvent =>
                    auditEvent.TargetUserId == created.Id &&
                    auditEvent.Action == SecurityAuditAction.UserCreated);
            Assert.Equal(setup.Actor.Id, createdEvent.ActorUserId);
            Assert.Equal(SystemRoles.Finance, createdEvent.Role);

            UserOperationResult unauthorizedResult = await service.CreateAsync(
                new CreateUserCommand(
                    "Alta no autorizada",
                    $"unauthorized-{suffix}@example.test",
                    SystemRoles.Assistant,
                    temporaryPassword),
                created.Id);
            Assert.Equal(UserOperationStatus.ProtectedAction, unauthorizedResult.Status);

            UserOperationResult duplicateResult = await service.CreateAsync(
                new CreateUserCommand(
                    "Correo repetido",
                    email.ToUpperInvariant(),
                    SystemRoles.Assistant,
                    temporaryPassword),
                setup.Actor.Id);
            Assert.Equal(UserOperationStatus.Duplicate, duplicateResult.Status);

            await transaction.CreateSavepointAsync("BeforeSecondRole");
            dbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = created.Id,
                RoleId = setup.AssistantRoleId,
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            await transaction.RollbackToSavepointAsync("BeforeSecondRole");
            dbContext.ChangeTracker.Clear();

            await SeedPagedFinanceUsersAsync(dbContext, setup.FinanceRoleId, suffix);

            PagedResult<UserModel> secondPage = await service.SearchAsync(new UserQuery(
                search: suffix,
                status: UserStatusFilter.Active,
                role: SystemRoles.Finance,
                page: 2,
                pageSize: 20));

            Assert.Equal(22, secondPage.TotalCount);
            Assert.Equal(2, secondPage.Items.Count);
            Assert.All(secondPage.Items, user => Assert.Equal(SystemRoles.Finance, user.Role));

            UserModel versionBeforeEdit = (await service.GetAsync(created.Id))!;
            string securityStampBeforeEdit = created.SecurityStamp!;
            string editedEmail = $"assistant-{suffix}@example.test";

            UserOperationResult editResult = await service.UpdateAsync(
                created.Id,
                new UpdateUserCommand(
                    "Persona Asistente",
                    editedEmail,
                    SystemRoles.Assistant),
                versionBeforeEdit.Version,
                setup.Actor.Id);

            Assert.True(editResult.IsSuccess, editResult.Message);

            dbContext.ChangeTracker.Clear();
            ApplicationUser edited = await dbContext.Users.SingleAsync(user => user.Id == created.Id);
            Assert.Equal("Persona Asistente", edited.FullName);
            Assert.Equal(editedEmail, edited.Email);
            Assert.NotEqual(securityStampBeforeEdit, edited.SecurityStamp);
            Assert.Equal(
                [SystemRoles.Assistant],
                await setup.UserManager.GetRolesAsync(edited));

            SecurityAuditAction[] editActions = await dbContext.SecurityAuditEvents
                .AsNoTracking()
                .Where(auditEvent => auditEvent.TargetUserId == edited.Id)
                .Select(auditEvent => auditEvent.Action)
                .ToArrayAsync();
            Assert.Contains(SecurityAuditAction.UserProfileUpdated, editActions);
            Assert.Contains(SecurityAuditAction.UserRoleChanged, editActions);

            UserOperationResult staleResult = await service.UpdateAsync(
                edited.Id,
                new UpdateUserCommand(
                    "Cambio obsoleto",
                    editedEmail,
                    SystemRoles.Assistant),
                versionBeforeEdit.Version,
                setup.Actor.Id);
            Assert.Equal(UserOperationStatus.ConcurrencyConflict, staleResult.Status);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task StateAndUnlock_ProtectSelfInvalidateSessionsAndKeepLockoutSeparate()
    {
        if (!SqlTestsEnabled())
        {
            return;
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FinancialDbContext dbContext = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            await DeactivateExistingAdministratorsAsync(dbContext);
            TestSecurityContext setup = await CreateSecurityContextAsync(scope.ServiceProvider);
            IUserAdministrationService service = scope.ServiceProvider
                .GetRequiredService<IUserAdministrationService>();
            IUserAccountService account = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
            string suffix = Guid.NewGuid().ToString("N")[..8];
            string email = $"state-{suffix}@example.test";

            Assert.True((await service.CreateAsync(
                new CreateUserCommand(
                    "Usuario de Estado",
                    email,
                    SystemRoles.Management,
                    "Temporal!123Aa"),
                setup.Actor.Id)).IsSuccess);

            string secondAdminEmail = $"second-admin-{suffix}@example.test";
            const string secondAdminTemporaryPassword = "TemporalAdmin!456Bb";
            Assert.True((await service.CreateAsync(
                new CreateUserCommand(
                    "Segundo Administrador",
                    secondAdminEmail,
                    SystemRoles.Administrator,
                    secondAdminTemporaryPassword),
                setup.Actor.Id)).IsSuccess);

            dbContext.ChangeTracker.Clear();
            ApplicationUser secondAdministrator = await dbContext.Users
                .SingleAsync(user => user.Email == secondAdminEmail);
            Assert.True((await account.ChangePasswordAsync(
                secondAdministrator.Id,
                new ChangeOwnPasswordCommand(
                    secondAdminTemporaryPassword,
                    "AdministradorFinal!789Cc"))).IsSuccess);

            UserModel actorModel = (await service.GetAsync(setup.Actor.Id))!;
            UserOperationResult selfDeactivate = await service.SetActiveAsync(
                setup.Actor.Id,
                false,
                actorModel.Version,
                setup.Actor.Id);
            Assert.Equal(UserOperationStatus.ProtectedAction, selfDeactivate.Status);
            Assert.Contains("propia", selfDeactivate.Message, StringComparison.OrdinalIgnoreCase);

            UserModel actorBeforeExternalDeactivation = (await service.GetAsync(setup.Actor.Id))!;
            UserOperationResult externalDeactivation = await service.SetActiveAsync(
                setup.Actor.Id,
                false,
                actorBeforeExternalDeactivation.Version,
                secondAdministrator.Id);
            Assert.True(externalDeactivation.IsSuccess, externalDeactivation.Message);

            UserModel lastAdministrator = (await service.GetAsync(secondAdministrator.Id))!;
            UserOperationResult lastAdministratorAttempt = await service.SetActiveAsync(
                secondAdministrator.Id,
                false,
                lastAdministrator.Version,
                secondAdministrator.Id);
            Assert.Equal(UserOperationStatus.ProtectedAction, lastAdministratorAttempt.Status);
            Assert.Contains(
                "al menos un Administrador activo",
                lastAdministratorAttempt.Message,
                StringComparison.OrdinalIgnoreCase);

            UserModel inactiveActor = (await service.GetAsync(setup.Actor.Id))!;
            Assert.True((await service.SetActiveAsync(
                setup.Actor.Id,
                true,
                inactiveActor.Version,
                secondAdministrator.Id)).IsSuccess);

            actorModel = (await service.GetAsync(setup.Actor.Id))!;

            UserOperationResult selfRoleChange = await service.UpdateAsync(
                setup.Actor.Id,
                new UpdateUserCommand(
                    setup.Actor.FullName,
                    setup.Actor.Email!,
                    SystemRoles.Finance),
                actorModel.Version,
                setup.Actor.Id);
            Assert.Equal(UserOperationStatus.ProtectedAction, selfRoleChange.Status);

            dbContext.ChangeTracker.Clear();
            ApplicationUser target = await dbContext.Users.SingleAsync(user => user.Email == email);
            target.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
            target.AccessFailedCount = 5;
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();

            UserModel lockedModel = (await service.GetAsync(target.Id))!;
            Assert.True(lockedModel.IsLockedOut);
            string stampBeforeDeactivation = target.SecurityStamp!;

            UserOperationResult deactivateResult = await service.SetActiveAsync(
                target.Id,
                false,
                lockedModel.Version,
                setup.Actor.Id);
            Assert.True(deactivateResult.IsSuccess, deactivateResult.Message);

            UserModel inactiveModel = (await service.GetAsync(target.Id))!;
            UserOperationResult reactivateResult = await service.SetActiveAsync(
                target.Id,
                true,
                inactiveModel.Version,
                setup.Actor.Id);
            Assert.True(reactivateResult.IsSuccess, reactivateResult.Message);

            dbContext.ChangeTracker.Clear();
            ApplicationUser reactivated = await dbContext.Users.SingleAsync(user => user.Id == target.Id);
            Assert.True(reactivated.IsActive);
            Assert.NotEqual(stampBeforeDeactivation, reactivated.SecurityStamp);
            Assert.NotNull(reactivated.LockoutEnd);
            Assert.Equal(5, reactivated.AccessFailedCount);

            UserModel versionBeforeUnlock = (await service.GetAsync(target.Id))!;
            UserOperationResult unlockResult = await service.UnlockAsync(
                target.Id,
                versionBeforeUnlock.Version,
                setup.Actor.Id);
            Assert.True(unlockResult.IsSuccess, unlockResult.Message);

            dbContext.ChangeTracker.Clear();
            ApplicationUser unlocked = await dbContext.Users.SingleAsync(user => user.Id == target.Id);
            Assert.Null(unlocked.LockoutEnd);
            Assert.Equal(0, unlocked.AccessFailedCount);
            Assert.Contains(
                await dbContext.SecurityAuditEvents
                    .AsNoTracking()
                    .Where(auditEvent => auditEvent.TargetUserId == target.Id)
                    .Select(auditEvent => auditEvent.Action)
                    .ToArrayAsync(),
                action => action == SecurityAuditAction.UserUnlocked);

            UserOperationResult selfReset = await service.ResetPasswordAsync(
                setup.Actor.Id,
                new ResetUserPasswordCommand("OtraClave!789Cc"),
                actorModel.Version,
                setup.Actor.Id);
            Assert.Equal(UserOperationStatus.ProtectedAction, selfReset.Status);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    [Fact]
    [Trait("Category", "SqlServer")]
    public async Task ResetAndOwnChange_ReplaceHashesClearLockoutAndRemoveTemporaryRequirement()
    {
        if (!SqlTestsEnabled())
        {
            return;
        }

        await using AsyncServiceScope scope = factory.Services.CreateAsyncScope();
        FinancialDbContext dbContext = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            TestSecurityContext setup = await CreateSecurityContextAsync(scope.ServiceProvider);
            IUserAdministrationService administration = scope.ServiceProvider
                .GetRequiredService<IUserAdministrationService>();
            IUserAccountService account = scope.ServiceProvider.GetRequiredService<IUserAccountService>();
            IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory = scope.ServiceProvider
                .GetRequiredService<IUserClaimsPrincipalFactory<ApplicationUser>>();
            string suffix = Guid.NewGuid().ToString("N")[..8];
            string email = $"password-{suffix}@example.test";
            const string firstTemporaryPassword = "Temporal!123Aa";
            const string secondTemporaryPassword = "Reinicio!456Bb";
            const string finalPassword = "Definitiva!789Cc";

            Assert.True((await administration.CreateAsync(
                new CreateUserCommand(
                    "Usuario de Contraseña",
                    email,
                    SystemRoles.Assistant,
                    firstTemporaryPassword),
                setup.Actor.Id)).IsSuccess);

            dbContext.ChangeTracker.Clear();
            ApplicationUser target = await dbContext.Users.SingleAsync(user => user.Email == email);
            target.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);
            target.AccessFailedCount = 5;
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();

            UserModel beforeReset = (await administration.GetAsync(target.Id))!;
            string oldSecurityStamp = target.SecurityStamp!;

            UserOperationResult resetResult = await administration.ResetPasswordAsync(
                target.Id,
                new ResetUserPasswordCommand(secondTemporaryPassword),
                beforeReset.Version,
                setup.Actor.Id);
            Assert.True(resetResult.IsSuccess, resetResult.Message);

            dbContext.ChangeTracker.Clear();
            ApplicationUser resetUser = await dbContext.Users.SingleAsync(user => user.Id == target.Id);
            Assert.False(await setup.UserManager.CheckPasswordAsync(resetUser, firstTemporaryPassword));
            Assert.True(await setup.UserManager.CheckPasswordAsync(resetUser, secondTemporaryPassword));
            Assert.True(resetUser.MustChangePassword);
            Assert.Null(resetUser.LockoutEnd);
            Assert.Equal(0, resetUser.AccessFailedCount);
            Assert.NotEqual(oldSecurityStamp, resetUser.SecurityStamp);

            ClaimsPrincipal pendingPrincipal = await claimsFactory.CreateAsync(resetUser);
            Assert.True(pendingPrincipal.HasClaim(
                SystemClaimTypes.MustChangePassword,
                bool.TrueString));

            UserOperationResult staleReset = await administration.ResetPasswordAsync(
                target.Id,
                new ResetUserPasswordCommand("Obsoleta!012Dd"),
                beforeReset.Version,
                setup.Actor.Id);
            Assert.Equal(UserOperationStatus.ConcurrencyConflict, staleReset.Status);

            UserOperationResult ownChange = await account.ChangePasswordAsync(
                target.Id,
                new ChangeOwnPasswordCommand(null, finalPassword));
            Assert.True(ownChange.IsSuccess, ownChange.Message);

            dbContext.ChangeTracker.Clear();
            ApplicationUser finalUser = await dbContext.Users.SingleAsync(user => user.Id == target.Id);
            Assert.False(finalUser.MustChangePassword);
            Assert.False(await setup.UserManager.CheckPasswordAsync(finalUser, secondTemporaryPassword));
            Assert.True(await setup.UserManager.CheckPasswordAsync(finalUser, finalPassword));

            ClaimsPrincipal finalPrincipal = await claimsFactory.CreateAsync(finalUser);
            Assert.False(finalPrincipal.HasClaim(
                SystemClaimTypes.MustChangePassword,
                bool.TrueString));

            SecurityAuditAction[] actions = await dbContext.SecurityAuditEvents
                .AsNoTracking()
                .Where(auditEvent => auditEvent.TargetUserId == target.Id)
                .Select(auditEvent => auditEvent.Action)
                .ToArrayAsync();
            Assert.Contains(SecurityAuditAction.PasswordReset, actions);
            Assert.Contains(SecurityAuditAction.PasswordChanged, actions);
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }

    private static bool SqlTestsEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(EnableVariable),
            "1",
            StringComparison.Ordinal);
    }

    private static async Task<TestSecurityContext> CreateSecurityContextAsync(
        IServiceProvider services)
    {
        FinancialDbContext dbContext = services.GetRequiredService<FinancialDbContext>();
        UserManager<ApplicationUser> userManager = services
            .GetRequiredService<UserManager<ApplicationUser>>();
        RoleManager<IdentityRole<Guid>> roleManager = services
            .GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        Dictionary<string, Guid> roleIds = new(StringComparer.Ordinal);
        foreach (string roleName in SystemRoles.All)
        {
            IdentityRole<Guid>? role = await roleManager.FindByNameAsync(roleName);
            if (role is null)
            {
                role = new IdentityRole<Guid>(roleName);
                AssertSucceeded(await roleManager.CreateAsync(role));
            }

            roleIds.Add(roleName, role.Id);
        }

        Guid actorId = Guid.NewGuid();
        string email = $"sql-admin-{actorId:N}@example.test";
        ApplicationUser actor = new()
        {
            Id = actorId,
            FullName = "Administrador SQL de Prueba",
            Email = email,
            UserName = email,
            EmailConfirmed = true,
            IsActive = true,
            MustChangePassword = false,
        };

        AssertSucceeded(await userManager.CreateAsync(actor, InitialAdministratorPassword));
        AssertSucceeded(await userManager.AddToRoleAsync(actor, SystemRoles.Administrator));
        dbContext.ChangeTracker.Clear();

        actor = await dbContext.Users.SingleAsync(user => user.Id == actorId);
        return new TestSecurityContext(
            actor,
            userManager,
            roleIds[SystemRoles.Finance],
            roleIds[SystemRoles.Assistant]);
    }

    private static async Task SeedPagedFinanceUsersAsync(
        FinancialDbContext dbContext,
        Guid financeRoleId,
        string suffix)
    {
        for (int index = 1; index <= 21; index++)
        {
            Guid id = Guid.NewGuid();
            string email = $"page-{suffix}-{index:D2}@example.test";
            string normalizedEmail = email.ToUpperInvariant();
            dbContext.Users.Add(new ApplicationUser
            {
                Id = id,
                FullName = $"Usuario Paginado {index:D2}",
                Email = email,
                NormalizedEmail = normalizedEmail,
                UserName = email,
                NormalizedUserName = normalizedEmail,
                EmailConfirmed = true,
                IsActive = true,
                MustChangePassword = true,
                LockoutEnabled = true,
                SecurityStamp = Guid.NewGuid().ToString("N"),
                ConcurrencyStamp = Guid.NewGuid().ToString("N"),
            });
            dbContext.UserRoles.Add(new IdentityUserRole<Guid>
            {
                UserId = id,
                RoleId = financeRoleId,
            });
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static Task<int> DeactivateExistingAdministratorsAsync(FinancialDbContext dbContext)
    {
        return dbContext.Users
            .Where(user => dbContext.UserRoles
                .Join(
                    dbContext.Roles,
                    userRole => userRole.RoleId,
                    role => role.Id,
                    (userRole, role) => new { userRole.UserId, role.Name })
                .Any(assignment =>
                    assignment.UserId == user.Id &&
                    assignment.Name == SystemRoles.Administrator))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(user => user.IsActive, false));
    }

    private static void AssertSucceeded(IdentityResult result)
    {
        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private sealed record TestSecurityContext(
        ApplicationUser Actor,
        UserManager<ApplicationUser> UserManager,
        Guid FinanceRoleId,
        Guid AssistantRoleId);
}
