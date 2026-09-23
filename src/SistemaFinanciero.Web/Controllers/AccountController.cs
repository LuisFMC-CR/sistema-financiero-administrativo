using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Web.Models.Account;
using SistemaFinanciero.Web.Security;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Expone únicamente las operaciones de cuenta necesarias para usuarios internos.
/// </summary>
[Route("cuenta")]
public sealed partial class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IUserAccountService accountService,
    ILogger<AccountController> logger) : Controller
{
    private const string InvalidCredentialsMessage = "Usuario o contraseña incorrectos.";

    /// <summary>
    /// Muestra el formulario de inicio de sesión.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("iniciar-sesion")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    /// <summary>
    /// Autentica al usuario sin crear una cookie persistente.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("iniciar-sesion")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ClearLoginPassword(model);
            return View(model);
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(model.Email.Trim());

        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
            ClearLoginPassword(model);
            return View(model);
        }

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.PasswordSignInAsync(
            user,
            model.Password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            LogLoginRejected(logger, user.Id);
            ModelState.AddModelError(string.Empty, InvalidCredentialsMessage);
            ClearLoginPassword(model);
            return View(model);
        }

        LogLoginSucceeded(logger, user.Id);

        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(ChangePassword));
        }

        return Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Cierra la sesión actual mediante una solicitud protegida contra falsificación.
    /// </summary>
    [AllowBeforePasswordChange]
    [HttpPost("cerrar-sesion")]
    public async Task<IActionResult> Logout()
    {
        string? userId = userManager.GetUserId(User);
        await signInManager.SignOutAsync();
        LogLogout(logger, userId);

        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Muestra el formulario de cambio de contraseña.
    /// </summary>
    [AllowBeforePasswordChange]
    [HttpGet("cambiar-contrasena")]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    /// <summary>
    /// Cambia la contraseña mediante ASP.NET Core Identity.
    /// </summary>
    [AllowBeforePasswordChange]
    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordViewModel model,
        CancellationToken cancellationToken)
    {
        bool requiresPasswordChange = RequiresPasswordChange();
        if (!requiresPasswordChange && string.IsNullOrWhiteSpace(model.CurrentPassword))
        {
            ModelState.AddModelError(
                nameof(model.CurrentPassword),
                "La contraseña actual es obligatoria.");
        }

        if (!ModelState.IsValid)
        {
            ClearPasswordValues(model);
            return View(model);
        }

        if (!Guid.TryParse(userManager.GetUserId(User), out Guid userId))
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        UserOperationResult result = await accountService.ChangePasswordAsync(
            userId,
            new ChangeOwnPasswordCommand(model.CurrentPassword, model.NewPassword),
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.Status == UserOperationStatus.NotFound)
            {
                await signInManager.SignOutAsync();
                return RedirectToAction(nameof(Login));
            }

            ModelState.AddModelError(
                string.Empty,
                result.Message ?? "No fue posible cambiar la contraseña.");
            ClearPasswordValues(model);
            return View(model);
        }

        ApplicationUser? user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        await signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "La contraseña se actualizó correctamente.";
        LogPasswordChanged(logger, userId);

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// Informa que el usuario autenticado no posee el permiso solicitado.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("acceso-denegado")]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private void ClearPasswordValues(ChangePasswordViewModel model)
    {
        model.CurrentPassword = string.Empty;
        model.NewPassword = string.Empty;
        model.ConfirmPassword = string.Empty;
        ClearModelStateValue(nameof(model.CurrentPassword));
        ClearModelStateValue(nameof(model.NewPassword));
        ClearModelStateValue(nameof(model.ConfirmPassword));
    }

    private void ClearLoginPassword(LoginViewModel model)
    {
        model.Password = string.Empty;
        ClearModelStateValue(nameof(model.Password));
    }

    private bool RequiresPasswordChange()
    {
        return User.FindAll(SystemClaimTypes.MustChangePassword).Any(claim =>
            bool.TryParse(claim.Value, out bool required) && required);
    }

    private void ClearModelStateValue(string key)
    {
        string[] errors = ModelState[key]?.Errors
            .Select(error => error.ErrorMessage)
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .ToArray() ?? [];

        ModelState.Remove(key);

        foreach (string error in errors)
        {
            ModelState.AddModelError(key, error);
        }
    }

    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Warning,
        Message = "Intento de inicio de sesión rechazado para el usuario {UserId}.")]
    private static partial void LogLoginRejected(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "El usuario {UserId} inició sesión.")]
    private static partial void LogLoginSucceeded(ILogger logger, Guid userId);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Information,
        Message = "El usuario {UserId} cerró sesión.")]
    private static partial void LogLogout(ILogger logger, string? userId);

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "El usuario {UserId} cambió su contraseña.")]
    private static partial void LogPasswordChanged(ILogger logger, Guid userId);
}
