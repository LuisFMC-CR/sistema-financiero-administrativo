using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Ruta anterior de las tarifas de IVA. La pantalla se trasladó a
/// <c>TaxesAndWithholdingsController</c> (bloque 1 del paquete v7, punto 6); esta ruta solo redirige
/// para no romper enlaces o marcadores existentes.
/// </summary>
[Authorize(Policy = SystemPolicies.ViewBusinessCatalogs)]
[Route("catalogos/tipos-impuesto")]
public sealed class TaxTypesController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        return RedirectToAction("Index", "TaxesAndWithholdings");
    }
}
