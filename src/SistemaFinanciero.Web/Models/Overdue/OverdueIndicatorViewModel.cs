using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Web.Models.Overdue;

/// <summary>
/// Modelo del indicador de cuentas vencidas (regla 9), compartido por la página de inicio y la
/// pantalla de Consultas.
/// </summary>
public sealed record OverdueIndicatorViewModel(
    OverdueSummary Receivables,
    OverdueSummary Payables,
    int AlertDays);
