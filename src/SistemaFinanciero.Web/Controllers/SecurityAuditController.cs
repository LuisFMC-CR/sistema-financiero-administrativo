using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Audit;
using SistemaFinanciero.Web.Models.Audit;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta, de solo consulta, la bitácora de seguridad (altas, bajas, bloqueos, contraseñas).</summary>
/// <remarks>
/// Conexiones: usa <c>ISecurityAuditService</c>. Exclusivo de Administrador
/// (<c>SystemPolicies.ManageUsers</c>), igual que <c>UsersController</c>: es la mitad "temas de
/// sistema" de la bitácora que corresponde a su perfil, sin ningún dato financiero. La mitad "temas de
/// operación" la ve Gerencia en <c>OperationAuditController</c>.
/// </remarks>
[Authorize(Policy = SystemPolicies.ManageUsers)]
[Route("administracion/bitacora-seguridad")]
public sealed class SecurityAuditController(ISecurityAuditService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        PagedResult<SecurityAuditEntryModel> results = await service.SearchAsync(
            new SecurityAuditQuery(page),
            cancellationToken);
        return View(new SecurityAuditIndexViewModel(results));
    }
}
