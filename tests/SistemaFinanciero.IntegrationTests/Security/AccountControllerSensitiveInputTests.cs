using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Account;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;
using StaticOptions = Microsoft.Extensions.Options.Options;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class AccountControllerSensitiveInputTests
{
    [Fact]
    public async Task Login_WithInvalidModel_ClearsPasswordAndPreservesValidationError()
    {
        AccountController controller = CreateController();
        LoginViewModel model = new()
        {
            Email = "usuario@empresa.example",
            Password = "Valor-sensible-2026!",
        };
        SetSensitiveValue(controller.ModelState, nameof(model.Password), model.Password);
        controller.ModelState.AddModelError(
            nameof(model.Password),
            "La contraseña es obligatoria.");

        IActionResult action = await controller.Login(model);

        Assert.IsType<ViewResult>(action);
        Assert.Empty(model.Password);
        AssertSensitiveValueWasRemoved(controller.ModelState, nameof(model.Password));
        Assert.Contains(
            controller.ModelState[nameof(model.Password)]!.Errors,
            error => error.ErrorMessage == "La contraseña es obligatoria.");
    }

    [Fact]
    public async Task Login_WhenAuthenticationIsRejected_ClearsPasswordAndKeepsGenericError()
    {
        ApplicationUser user = new()
        {
            Id = Guid.NewGuid(),
            Email = "usuario@empresa.example",
            IsActive = true,
        };
        AccountController controller = CreateController(user, IdentitySignInResult.Failed);
        LoginViewModel model = new()
        {
            Email = user.Email,
            Password = "Valor-sensible-2026!",
        };
        SetSensitiveValue(controller.ModelState, nameof(model.Password), model.Password);

        IActionResult action = await controller.Login(model);

        Assert.IsType<ViewResult>(action);
        Assert.Empty(model.Password);
        AssertSensitiveValueWasRemoved(controller.ModelState, nameof(model.Password));
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "Usuario o contraseña incorrectos.");
    }

    [Fact]
    public async Task ChangePassword_WithInvalidConfirmation_ClearsEveryPasswordAndPreservesError()
    {
        AccountController controller = CreateController();
        ChangePasswordViewModel model = CreateChangePasswordModel();
        SetAllPasswordValues(controller.ModelState, model);
        controller.ModelState.AddModelError(
            nameof(model.ConfirmPassword),
            "La confirmación no coincide con la nueva contraseña.");

        IActionResult action = await controller.ChangePassword(model, CancellationToken.None);

        Assert.IsType<ViewResult>(action);
        AssertAllPasswordsWereCleared(controller.ModelState, model);
        Assert.Contains(
            controller.ModelState[nameof(model.ConfirmPassword)]!.Errors,
            error => error.ErrorMessage ==
                "La confirmación no coincide con la nueva contraseña.");
    }

    [Fact]
    public async Task ChangePassword_WhenServiceRejectsOperation_ClearsEveryPasswordAndKeepsError()
    {
        RecordingAccountService accountService = new()
        {
            Result = UserOperationResult.Invalid("La contraseña actual es incorrecta."),
        };
        Guid userId = Guid.NewGuid();
        AccountController controller = CreateController(accountService: accountService);
        SetAuthenticatedUser(controller, userId);
        ChangePasswordViewModel model = CreateChangePasswordModel();
        SetAllPasswordValues(controller.ModelState, model);

        IActionResult action = await controller.ChangePassword(model, CancellationToken.None);

        Assert.IsType<ViewResult>(action);
        Assert.Equal(1, accountService.Calls);
        AssertAllPasswordsWereCleared(controller.ModelState, model);
        Assert.Contains(
            controller.ModelState[string.Empty]!.Errors,
            error => error.ErrorMessage == "La contraseña actual es incorrecta.");
    }

    [Fact]
    public async Task ChangePassword_WithTemporaryRequirement_OmitsCurrentPasswordAndRedirectsHome()
    {
        Guid userId = Guid.NewGuid();
        ApplicationUser user = new()
        {
            Id = userId,
            Email = "temporal@empresa.example",
            IsActive = true,
        };
        RecordingAccountService accountService = new();
        AccountController controller = CreateController(user, accountService: accountService);
        SetAuthenticatedUser(controller, userId, mustChangePassword: true);
        ChangePasswordViewModel model = new()
        {
            NewPassword = "Nueva-segura-2026!",
            ConfirmPassword = "Nueva-segura-2026!",
        };

        IActionResult action = await controller.ChangePassword(model, CancellationToken.None);

        RedirectToActionResult result = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal("Index", result.ActionName);
        Assert.Equal("Home", result.ControllerName);
        Assert.Equal(1, accountService.Calls);
        Assert.NotNull(accountService.LastCommand);
        Assert.True(string.IsNullOrEmpty(accountService.LastCommand.CurrentPassword));
    }

    private static AccountController CreateController(
        ApplicationUser? user = null,
        IdentitySignInResult? signInResult = null,
        RecordingAccountService? accountService = null)
    {
        StubUserManager userManager = new(user);
        StubSignInManager signInManager = new(
            userManager,
            signInResult ?? IdentitySignInResult.Failed);

        DefaultHttpContext httpContext = new();
        AccountController controller = new(
            userManager,
            signInManager,
            accountService ?? new RecordingAccountService(),
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext,
            },
        };
        controller.TempData = new TempDataDictionary(httpContext, new StubTempDataProvider());

        return controller;
    }

    private static ChangePasswordViewModel CreateChangePasswordModel()
    {
        return new ChangePasswordViewModel
        {
            CurrentPassword = "Actual-segura-2026!",
            NewPassword = "Nueva-segura-2026!",
            ConfirmPassword = "Nueva-segura-2026!",
        };
    }

    private static void SetAuthenticatedUser(
        AccountController controller,
        Guid userId,
        bool mustChangePassword = false)
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, userId.ToString())];
        if (mustChangePassword)
        {
            claims.Add(new Claim(SystemClaimTypes.MustChangePassword, bool.TrueString));
        }

        ClaimsIdentity identity = new(
            claims,
            authenticationType: "Test");
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(identity);
    }

    private static void SetAllPasswordValues(
        ModelStateDictionary modelState,
        ChangePasswordViewModel model)
    {
        SetSensitiveValue(modelState, nameof(model.CurrentPassword), model.CurrentPassword);
        SetSensitiveValue(modelState, nameof(model.NewPassword), model.NewPassword);
        SetSensitiveValue(modelState, nameof(model.ConfirmPassword), model.ConfirmPassword);
    }

    private static void SetSensitiveValue(
        ModelStateDictionary modelState,
        string key,
        string value)
    {
        modelState.SetModelValue(key, value, value);
        modelState.MarkFieldValid(key);
    }

    private static void AssertAllPasswordsWereCleared(
        ModelStateDictionary modelState,
        ChangePasswordViewModel model)
    {
        Assert.Empty(model.CurrentPassword);
        Assert.Empty(model.NewPassword);
        Assert.Empty(model.ConfirmPassword);
        AssertSensitiveValueWasRemoved(modelState, nameof(model.CurrentPassword));
        AssertSensitiveValueWasRemoved(modelState, nameof(model.NewPassword));
        AssertSensitiveValueWasRemoved(modelState, nameof(model.ConfirmPassword));
    }

    private static void AssertSensitiveValueWasRemoved(
        ModelStateDictionary modelState,
        string key)
    {
        ModelStateEntry? entry = modelState[key];
        Assert.True(entry is null || entry.RawValue is null);
        Assert.True(entry is null || entry.AttemptedValue is null);
    }

    private sealed class RecordingAccountService : IUserAccountService
    {
        public int Calls { get; private set; }

        public ChangeOwnPasswordCommand? LastCommand { get; private set; }

        public UserOperationResult Result { get; init; } = UserOperationResult.Succeeded();

        public Task<UserOperationResult> ChangePasswordAsync(
            Guid userId,
            ChangeOwnPasswordCommand command,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            LastCommand = command;
            return Task.FromResult(Result);
        }
    }

    private sealed class StubUserManager(ApplicationUser? user)
        : UserManager<ApplicationUser>(
            new StubUserStore(),
            StaticOptions.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance)
    {
        public override Task<ApplicationUser?> FindByEmailAsync(string email)
        {
            return Task.FromResult(user);
        }

        public override Task<ApplicationUser?> FindByIdAsync(string userId)
        {
            return Task.FromResult(user);
        }
    }

    private sealed class StubSignInManager(
        UserManager<ApplicationUser> userManager,
        IdentitySignInResult result)
        : SignInManager<ApplicationUser>(
            userManager,
            new HttpContextAccessor(),
            new UserClaimsPrincipalFactory<ApplicationUser>(
                userManager,
                StaticOptions.Create(new IdentityOptions())),
            StaticOptions.Create(new IdentityOptions()),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(StaticOptions.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<ApplicationUser>())
    {
        public override Task<IdentitySignInResult> PasswordSignInAsync(
            ApplicationUser user,
            string password,
            bool isPersistent,
            bool lockoutOnFailure)
        {
            return Task.FromResult(result);
        }

        public override Task RefreshSignInAsync(ApplicationUser user)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class StubUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(user.Id.ToString());
        }

        public Task<string?> GetUserNameAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(user.UserName);
        }

        public Task SetUserNameAsync(
            ApplicationUser user,
            string? userName,
            CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(user.NormalizedUserName);
        }

        public Task SetNormalizedUserNameAsync(
            ApplicationUser user,
            string? normalizedName,
            CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> UpdateAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<IdentityResult> DeleteAsync(
            ApplicationUser user,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(IdentityResult.Success);
        }

        public Task<ApplicationUser?> FindByIdAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ApplicationUser?>(null);
        }

        public Task<ApplicationUser?> FindByNameAsync(
            string normalizedUserName,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<ApplicationUser?>(null);
        }
    }

    private sealed class StubTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context)
        {
            return new Dictionary<string, object>();
        }

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }
}
