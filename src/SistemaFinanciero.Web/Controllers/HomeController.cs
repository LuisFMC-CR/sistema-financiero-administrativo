using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models;
using SistemaFinanciero.Web.Models.Overdue;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>
/// Presenta la página inicial, con el indicador de cuentas vencidas (regla 9), y el manejo general de
/// errores.
/// </summary>
/// <remarks>
/// Conexiones: es la ruta <c>Home/Index</c>, destino predeterminado del enrutador (<c>Program.cs</c>) y
/// de los enlaces «Inicio» en <c>_Layout.cshtml</c>. El indicador solo se calcula y muestra para
/// Gerencia y Finanzas (política <c>Reportes.Ver</c>): Asistente "no ve... saldos" (perfiles). Usa
/// <c>IReceivableService</c>/<c>IPayableService.GetOverdueSummaryAsync</c>, los mismos que alimentarán
/// el indicador de la pantalla de Consultas, mediante la vista parcial compartida
/// <c>_OverdueIndicator.cshtml</c>. <c>Error</c> es el destino de <c>app.UseExceptionHandler</c> en
/// producción.
/// </remarks>
public sealed class HomeController(
    IReceivableService receivableService,
    IPayableService payableService,
    ISystemParametersService systemParametersService,
    IAuthorizationService authorizationService) : Controller
{
    /// <summary>Días de alerta por defecto (regla 9) cuando Gerencia no ha configurado parámetros.</summary>
    private const int DefaultOverdueAlertDays = 7;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        bool canViewFinancialReports = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.ViewFinancialReports)).Succeeded;

        if (!canViewFinancialReports)
        {
            return View();
        }

        SystemParametersModel? parameters = await systemParametersService.GetAsync(cancellationToken);
        int alertDays = parameters?.OverdueAlertDays ?? DefaultOverdueAlertDays;

        OverdueSummary receivablesSummary = await receivableService.GetOverdueSummaryAsync(alertDays, cancellationToken);
        OverdueSummary payablesSummary = await payableService.GetOverdueSummaryAsync(alertDays, cancellationToken);

        return View(new OverdueIndicatorViewModel(receivablesSummary, payablesSummary, alertDays));
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
