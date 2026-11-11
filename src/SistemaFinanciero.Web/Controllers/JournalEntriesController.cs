using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Web.Models.Ledger;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Consulta general de los asientos contables automáticos y trazabilidad "Ver asiento"
/// (RF-78/RF-79).
/// </summary>
/// <remarks>
/// Conexiones: usa <c>IJournalEntryService</c>. <see cref="Index"/> es la consulta del período, con
/// filtros de mes, estado y número de asiento; <see cref="Trace"/> es la pantalla a la que enlaza el botón "Ver asiento"
/// de cada documento (facturas, gastos, abonos, pagos, ingresos manuales y ajustes de saldo). Ambos
/// exigen <c>SystemPolicies.ViewFinancialReports</c> (Gerencia y Finanzas), porque muestran cuentas y
/// saldos contables, igual que los reportes. Los asientos nunca se generan desde aquí: cada operación
/// los crea en su propia transacción.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialReports)]
[Route("contabilidad/asientos")]
public sealed class JournalEntriesController(IJournalEntryService service, TimeProvider timeProvider) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        int? periodYear,
        int? periodMonth,
        JournalEntryStateFilter? state,
        long? entryNumber,
        CancellationToken cancellationToken)
    {
        (int year, int month) = NormalizeMonth(periodYear, periodMonth);
        JournalEntryStateFilter filter = state ?? JournalEntryStateFilter.All;

        JournalEntryListResult result = await service.SearchAsync(year, month, filter, entryNumber, cancellationToken);

        return View(new JournalEntriesIndexViewModel(year, month, filter, entryNumber, result));
    }

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

    private (int Year, int Month) NormalizeMonth(int? periodYear, int? periodMonth)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return (periodYear ?? today.Year, periodMonth ?? today.Month);
    }
}
