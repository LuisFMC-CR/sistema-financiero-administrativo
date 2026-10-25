using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Receivables;

namespace SistemaFinanciero.Web.Models.Receivables;

/// <summary>
/// Modelo de listado de cuentas por cobrar. Conserva la consulta completa para repetir los mismos
/// filtros, uno por columna, al paginar.
/// </summary>
public sealed record ReceivableIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<ReceivableModel> Results,
    ReceivableQuery Query);

/// <summary>
/// Modelo de la pantalla de detalle: la cuenta por cobrar, su historial de abonos y si el usuario
/// puede registrar uno nuevo o anular alguno vigente.
/// </summary>
public sealed record ReceivableDetailsViewModel(
    ReceivableModel Receivable,
    IReadOnlyList<ReceivablePaymentModel> Payments,
    IReadOnlyList<BalanceAdjustmentModel> Adjustments,
    bool CanRegisterPayment,
    bool CanCancelPayment,
    bool CanAdjust);
