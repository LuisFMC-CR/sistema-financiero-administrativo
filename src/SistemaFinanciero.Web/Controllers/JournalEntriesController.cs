using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Ledger;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Trazabilidad de los asientos contables automáticos ("Ver asiento", RF-78/RF-79) y comando explícito
/// de generación para los documentos que ya existían antes del bloque 5 del paquete de mejoras
/// finales.
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IJournalEntryService</c>. <see cref="Trace"/> es la pantalla a la que enlaza el
/// botón "Ver asiento" de cada documento (facturas, gastos, abonos, pagos, ingresos manuales y
/// ajustes de saldo); su política es <c>SystemPolicies.ViewFinancialReports</c> porque muestra
/// cuentas y saldos contables, igual que los reportes. <see cref="Index"/> y
/// <see cref="Backfill"/> son exclusivos de Gerencia (<c>SystemPolicies.ManageSystemParameters</c>): el
/// comando nunca se ejecuta al arrancar, solo cuando Gerencia lo pide expresamente aquí.
/// </remarks>
[Route("contabilidad/asientos")]
public sealed class JournalEntriesController(IJournalEntryService service) : Controller
{
    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpGet("")]
    public IActionResult Index() => View();

    [Authorize(Policy = SystemPolicies.ManageSystemParameters)]
    [HttpPost("generar-pendientes")]
    public async Task<IActionResult> Backfill(CancellationToken cancellationToken)
    {
        JournalEntryBackfillResult result = await service.BackfillAsync(cancellationToken);

        TempData["SuccessMessage"] = result.EntriesCreated == 0 && result.ReversalsCreated == 0
            ? "No había asientos pendientes de generar."
            : $"Se generaron {result.EntriesCreated} asiento(s) y {result.ReversalsCreated} reversión(es).";

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = SystemPolicies.ViewFinancialReports)]
    [HttpGet("por-origen/{sourceType:int}/{sourceId:guid}")]
    public async Task<IActionResult> Trace(int sourceType, Guid sourceId, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(typeof(JournalEntrySourceType), sourceType))
        {
            return NotFound();
        }

        JournalEntryTraceModel? trace = await service.GetTraceAsync(
            (JournalEntrySourceType)sourceType, sourceId, cancellationToken);

        if (trace is null)
        {
            return NotFound();
        }

        return View(trace);
    }
}
