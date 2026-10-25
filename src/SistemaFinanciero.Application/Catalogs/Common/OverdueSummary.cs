namespace SistemaFinanciero.Application.Catalogs.Common;

/// <summary>
/// Resumen de cuentas vencidas y próximas a vencer (regla de negocio 9), compartido por
/// <c>IReceivableService</c> e <c>IPayableService</c>: vencida es vencimiento anterior a hoy y saldo
/// mayor que cero; próxima es vencimiento dentro de los próximos días de alerta, también con saldo
/// mayor que cero.
/// </summary>
public sealed record OverdueSummary(
    int OverdueCount,
    decimal OverdueAmount,
    int UpcomingCount,
    decimal UpcomingAmount);
