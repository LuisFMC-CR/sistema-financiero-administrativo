using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Proporciona comportamiento de presentación común a los catálogos.</summary>
public abstract class CatalogControllerBase : Controller
{
    private const int SearchMaxLength = 100;

    protected static string? NormalizeSearch(string? search)
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

    protected static CatalogStatusFilter NormalizeStatus(CatalogStatusFilter status)
    {
        return Enum.IsDefined(status) ? status : CatalogStatusFilter.Active;
    }

    protected bool TryGetActorId(out Guid actorId)
    {
        return Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);
    }

    protected IActionResult FormFailure<TModel>(
        CatalogOperationResult result,
        TModel model,
        string viewName)
    {
        if (result.Status == CatalogOperationStatus.NotFound)
        {
            return NotFound();
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
        }

        ModelState.AddModelError(string.Empty, OperationMessage(result));
        return View(viewName, model);
    }

    protected IActionResult StateFailure(CatalogOperationResult result, string controllerName)
    {
        if (result.Status == CatalogOperationStatus.NotFound)
        {
            return NotFound();
        }

        TempData["ErrorMessage"] = OperationMessage(result);
        return RedirectToAction("Index", controllerName);
    }

    protected void SetSuccessMessage(string message)
    {
        TempData["SuccessMessage"] = message;
    }

    private static string OperationMessage(CatalogOperationResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            return result.Message;
        }

        return result.Status switch
        {
            CatalogOperationStatus.Invalid => "Los datos no cumplen las reglas del catálogo.",
            CatalogOperationStatus.Duplicate => "Ya existe un registro con los mismos datos únicos.",
            CatalogOperationStatus.ConcurrencyConflict => "Otro usuario modificó el registro. Revise los datos e intente nuevamente.",
            CatalogOperationStatus.DependencyConflict => "El cambio no puede realizarse porque existen registros relacionados.",
            _ => "No fue posible completar la operación.",
        };
    }
}
