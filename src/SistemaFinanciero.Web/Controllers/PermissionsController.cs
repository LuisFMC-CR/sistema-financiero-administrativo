using Microsoft.AspNetCore.Mvc;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Pantalla de solo consulta de la matriz de perfiles y permisos (paso 7 del roadmap).
/// </summary>
/// <remarks>
/// Conexiones: sin servicio propio ni base de datos; muestra la matriz ya aprobada en
/// <c>CLAUDE.md</c>. No administra usuarios ni roles: eso lo hace <c>UsersController</c>
/// (ruta <c>/administracion/usuarios</c>, exclusivo de Administrador). Cualquier usuario autenticado
/// la ve, sin política adicional, porque es solo de consulta.
/// </remarks>
[Route("seguridad/perfiles-y-permisos")]
public sealed class PermissionsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
