using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;
using SistemaFinanciero.Web.Models.Users;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta la administración interna y no destructiva de cuentas de usuario.</summary>
[Authorize(Policy = SystemPolicies.ManageUsers)]
[Route("administracion/usuarios")]
public sealed class UsersController(IUserAdministrationService service) : Controller
{
    private const int SearchMaxLength = 100;

    /// <summary>Muestra usuarios filtrados y paginados del lado del servidor.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        UserStatusFilter status = UserStatusFilter.Active,
        string? role = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        UserQuery query = new(
            NormalizeSearch(search),
            NormalizeStatus(status),
            NormalizeRole(role),
            page);
        PagedResult<UserModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new UserIndexViewModel(
            results,
            query.Search,
            query.Status,
            query.Role,
            SystemRoles.All,
            actorId));
    }

    /// <summary>Muestra el formulario de alta de una cuenta.</summary>
    [HttpGet("crear")]
    public IActionResult Create()
    {
        return View(new UserCreateViewModel());
    }

    /// <summary>Crea una cuenta con un rol y una contraseña temporal.</summary>
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        UserCreateViewModel model,
        CancellationToken cancellationToken)
    {
        ValidateRole(model.Role);

        if (!ModelState.IsValid)
        {
            ClearPasswordValues(model);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        UserOperationResult result = await service.CreateAsync(
            new CreateUserCommand(
                model.FullName,
                model.Email,
                model.Role,
                model.TemporaryPassword),
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] =
                "El usuario fue creado. Deberá cambiar su contraseña temporal al iniciar sesión.";
            return RedirectToAction(nameof(Index));
        }

        ClearPasswordValues(model);
        AddOperationError(result, nameof(model.TemporaryPassword));
        return View(model);
    }

    /// <summary>Muestra los datos administrativos editables de una cuenta.</summary>
    [HttpGet("editar/{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        UserModel? user = await service.GetAsync(id, cancellationToken);
        return user is null ? NotFound() : View(ToEditModel(user, user.Id == actorId));
    }

    /// <summary>Actualiza nombre, correo y rol con concurrencia optimista.</summary>
    [HttpPost("editar/{id:guid}")]
    public async Task<IActionResult> Edit(
        Guid id,
        UserEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        model.IsCurrentUser = id == actorId;
        ValidateRole(model.Role);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        UserOperationResult result = await service.UpdateAsync(
            id,
            new UpdateUserCommand(model.FullName, model.Email, model.Role),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "El usuario fue actualizado correctamente.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == UserOperationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == UserOperationStatus.ProtectedAction && model.IsCurrentUser)
        {
            UserModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.Role = current.Role;
            model.Version = current.Version;
            ModelState.Remove(nameof(model.Role));
            ModelState.Remove(nameof(model.Version));
        }

        if (result.Status == UserOperationStatus.ConcurrencyConflict)
        {
            UserModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            model.Version = current.Version;
            ModelState.Remove(nameof(model.Version));
            Response.StatusCode = StatusCodes.Status409Conflict;
        }

        AddOperationError(result);
        return View(model);
    }

    /// <summary>Activa o desactiva una cuenta sin eliminar su historial.</summary>
    [HttpPost("estado/{id:guid}")]
    public async Task<IActionResult> SetActive(
        Guid id,
        UserStatusViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || model.IsActive is null)
        {
            TempData["ErrorMessage"] = "No fue posible validar el estado solicitado.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        UserOperationResult result = await service.SetActiveAsync(
            id,
            model.IsActive.Value,
            model.Version,
            actorId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return StateFailure(result);
        }

        TempData["SuccessMessage"] = model.IsActive.Value
            ? "El usuario fue reactivado."
            : "El usuario fue desactivado y sus sesiones anteriores fueron invalidadas.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Muestra el formulario aislado de restablecimiento de contraseña.</summary>
    [HttpGet("restablecer-contrasena/{id:guid}")]
    public async Task<IActionResult> ResetPassword(Guid id, CancellationToken cancellationToken)
    {
        UserModel? user = await service.GetAsync(id, cancellationToken);
        return user is null ? NotFound() : View(ToResetPasswordModel(user));
    }

    /// <summary>Asigna una contraseña temporal sin exponer tokens ni hashes.</summary>
    [HttpPost("restablecer-contrasena/{id:guid}")]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        UserResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        UserModel? target = await service.GetAsync(id, cancellationToken);
        if (target is null)
        {
            return NotFound();
        }

        PopulateTargetDisplay(model, target);

        if (!ModelState.IsValid)
        {
            ClearPasswordValues(model);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        UserOperationResult result = await service.ResetPasswordAsync(
            id,
            new ResetUserPasswordCommand(model.TemporaryPassword),
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] =
                "La contraseña temporal fue restablecida. El usuario deberá cambiarla al iniciar sesión.";
            return RedirectToAction(nameof(Index));
        }

        if (result.Status == UserOperationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == UserOperationStatus.ConcurrencyConflict)
        {
            UserModel? current = await service.GetAsync(id, cancellationToken);
            if (current is null)
            {
                return NotFound();
            }

            PopulateTargetDisplay(model, current);
            model.Version = current.Version;
            ModelState.Remove(nameof(model.Version));
            Response.StatusCode = StatusCodes.Status409Conflict;
        }

        string message = OperationMessage(result);
        ClearPasswordValues(model);
        ModelState.AddModelError(
            result.Status == UserOperationStatus.Invalid
                ? nameof(model.TemporaryPassword)
                : string.Empty,
            message);
        return View(model);
    }

    /// <summary>Elimina un bloqueo temporal conservando el resto de la cuenta.</summary>
    [HttpPost("desbloquear/{id:guid}")]
    public async Task<IActionResult> Unlock(
        Guid id,
        UserVersionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "No fue posible validar la cuenta que se desea desbloquear.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        UserOperationResult result = await service.UnlockAsync(
            id,
            model.Version,
            actorId,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return StateFailure(result);
        }

        TempData["SuccessMessage"] = "El bloqueo temporal del usuario fue eliminado.";
        return RedirectToAction(nameof(Index));
    }

    private static UserEditViewModel ToEditModel(UserModel user, bool isCurrentUser)
    {
        return new UserEditViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            Version = user.Version,
            IsCurrentUser = isCurrentUser,
        };
    }

    private static UserResetPasswordViewModel ToResetPasswordModel(UserModel user)
    {
        UserResetPasswordViewModel model = new() { Version = user.Version };
        PopulateTargetDisplay(model, user);
        return model;
    }

    private static void PopulateTargetDisplay(UserResetPasswordViewModel model, UserModel user)
    {
        model.TargetFullName = user.FullName;
        model.TargetEmail = user.Email;
    }

    private void AddOperationError(UserOperationResult result, string? invalidField = null)
    {
        if (result.Status == UserOperationStatus.Duplicate)
        {
            ModelState.AddModelError(nameof(UserInputViewModel.Email), OperationMessage(result));
            return;
        }

        ModelState.AddModelError(
            result.Status == UserOperationStatus.Invalid && invalidField is not null
                ? invalidField
                : string.Empty,
            OperationMessage(result));
    }

    private IActionResult StateFailure(UserOperationResult result)
    {
        if (result.Status == UserOperationStatus.NotFound)
        {
            return NotFound();
        }

        TempData["ErrorMessage"] = OperationMessage(result);
        return RedirectToAction(nameof(Index));
    }

    private void ValidateRole(string? role)
    {
        if (!string.IsNullOrWhiteSpace(role) && !SystemRoles.All.Contains(role, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(UserInputViewModel.Role), "Seleccione un rol permitido.");
        }
    }

    private bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        string normalized = search.Trim();
        return normalized.Length <= SearchMaxLength
            ? normalized
            : normalized[..SearchMaxLength];
    }

    private static UserStatusFilter NormalizeStatus(UserStatusFilter status)
    {
        return Enum.IsDefined(status) ? status : UserStatusFilter.Active;
    }

    private static string? NormalizeRole(string? role)
    {
        return SystemRoles.All.Contains(role ?? string.Empty, StringComparer.Ordinal)
            ? role
            : null;
    }

    private static string OperationMessage(UserOperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            return result.Message;
        }

        return result.Status switch
        {
            UserOperationStatus.Invalid => "Los datos no cumplen las reglas de seguridad.",
            UserOperationStatus.Duplicate => "Ya existe una cuenta con el mismo correo electrónico.",
            UserOperationStatus.ConcurrencyConflict =>
                "Otro administrador modificó esta cuenta. Revise los datos actuales e intente nuevamente.",
            UserOperationStatus.ProtectedAction => "La operación está protegida por una regla de seguridad.",
            _ => "No fue posible completar la operación.",
        };
    }

    private void ClearPasswordValues(UserResetPasswordViewModel model)
    {
        model.ClearPasswords();
        ClearModelStateValue(nameof(model.TemporaryPassword));
        ClearModelStateValue(nameof(model.ConfirmPassword));
    }

    private void ClearPasswordValues(UserCreateViewModel model)
    {
        model.TemporaryPassword = string.Empty;
        model.ConfirmPassword = string.Empty;
        ClearModelStateValue(nameof(model.TemporaryPassword));
        ClearModelStateValue(nameof(model.ConfirmPassword));
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
}
