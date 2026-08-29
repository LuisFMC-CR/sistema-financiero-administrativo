using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Users;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class UsersControllerInputTests
{
    [Fact]
    public async Task Create_WithUnknownRole_ReturnsValidationErrorWithoutCallingService()
    {
        RecordingUserAdministrationService service = new();
        UsersController controller = CreateController(service);
        UserCreateViewModel model = new()
        {
            FullName = "Usuario de prueba",
            Email = "usuario@empresa.example",
            Role = "Superusuario",
            TemporaryPassword = "Temporal-2026!",
            ConfirmPassword = "Temporal-2026!",
        };

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Role)));
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task Index_NormalizesUntrustedFiltersBeforeCallingService()
    {
        RecordingUserAdministrationService service = new();
        UsersController controller = CreateController(service);
        string longSearch = new('x', 150);

        IActionResult action = await controller.Index(
            longSearch,
            (UserStatusFilter)999,
            "Superusuario",
            page: -10,
            CancellationToken.None);

        ViewResult result = Assert.IsType<ViewResult>(action);
        Assert.IsType<UserIndexViewModel>(result.Model);
        Assert.NotNull(service.LastQuery);
        Assert.Equal(new string('x', 100), service.LastQuery.Search);
        Assert.Equal(UserStatusFilter.Active, service.LastQuery.Status);
        Assert.Null(service.LastQuery.Role);
        Assert.Equal(1, service.LastQuery.Page);
    }

    [Fact]
    public async Task ResetPassword_WhenServiceRejectsOperation_DoesNotRedisplayPasswords()
    {
        Guid targetId = Guid.NewGuid();
        RecordingUserAdministrationService service = new()
        {
            UserToReturn = new UserModel(
                targetId,
                "Cuenta objetivo",
                "objetivo@empresa.example",
                SystemRoles.Finance,
                IsActive: true,
                MustChangePassword: false,
                IsLockedOut: false,
                LockoutEndUtc: null,
                Version: "version-vigente"),
            ResetResult = UserOperationResult.Invalid("La contraseña temporal no es válida."),
        };
        UsersController controller = CreateController(service);
        UserResetPasswordViewModel model = new()
        {
            TemporaryPassword = "Temporal-2026!",
            ConfirmPassword = "Temporal-2026!",
            Version = "version-vigente",
        };

        IActionResult action = await controller.ResetPassword(
            targetId,
            model,
            CancellationToken.None);

        Assert.IsType<ViewResult>(action);
        Assert.Equal(1, service.ResetCalls);
        Assert.Empty(model.TemporaryPassword);
        Assert.Empty(model.ConfirmPassword);
        Assert.Equal("version-vigente", model.Version);
        Assert.False(controller.ModelState.IsValid);
    }

    private static UsersController CreateController(RecordingUserAdministrationService service)
    {
        Guid actorId = Guid.NewGuid();
        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.NameIdentifier, actorId.ToString())],
            authenticationType: "Test");

        return new UsersController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                },
            },
        };
    }

    private sealed class RecordingUserAdministrationService : IUserAdministrationService
    {
        public int CreateCalls { get; private set; }

        public UserQuery? LastQuery { get; private set; }

        public int ResetCalls { get; private set; }

        public UserOperationResult ResetResult { get; init; } = UserOperationResult.Succeeded();

        public UserModel? UserToReturn { get; init; }

        public Task<PagedResult<UserModel>> SearchAsync(
            UserQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<UserModel>([], query.Page, query.PageSize, 0));
        }

        public Task<UserModel?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(UserToReturn);
        }

        public Task<UserOperationResult> CreateAsync(
            CreateUserCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(UserOperationResult.Succeeded());
        }

        public Task<UserOperationResult> UpdateAsync(
            Guid id,
            UpdateUserCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UserOperationResult> SetActiveAsync(
            Guid id,
            bool isActive,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<UserOperationResult> ResetPasswordAsync(
            Guid id,
            ResetUserPasswordCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            ResetCalls++;
            return Task.FromResult(ResetResult);
        }

        public Task<UserOperationResult> UnlockAsync(
            Guid id,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
