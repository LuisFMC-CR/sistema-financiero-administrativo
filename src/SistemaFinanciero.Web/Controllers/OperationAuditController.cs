using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Audit;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Audit;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta, de solo consulta, la bitácora de operaciones (confirmar, anular y ajustar documentos
/// financieros; regla de negocio 4 y 16).
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IOperationAuditService</c>. Exclusivo de Gerencia
/// (<c>SystemPolicies.ViewAuditTrail</c>): es información financiera, y el perfil Administrador "no
/// tiene acceso a información financiera", así que no entra aquí aunque administre cuentas. Es la
/// mitad "temas de operación" de la bitácora; la mitad "temas de sistema" la ve Administrador en
/// <c>SecurityAuditController</c>. Vive en <c>auditoria/...</c> y no en <c>consultas/...</c> porque
/// <c>QueriesAuthorizationTests</c> exige que toda ruta bajo "consultas" use
/// <c>SystemPolicies.ViewFinancialReports</c> (Gerencia y Finanzas); esta pantalla es más restrictiva.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewAuditTrail)]
[Route("auditoria/bitacora-operaciones")]
public sealed class OperationAuditController(IOperationAuditService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        PagedResult<OperationAuditEntryModel> results = await service.SearchAsync(
            new OperationAuditQuery(page),
            cancellationToken);
        return View(new OperationAuditIndexViewModel(results));
    }
}
