using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Payables;

namespace SistemaFinanciero.Web.Models.Payables;

/// <summary>
/// Modelo de listado de cuentas por pagar. Conserva la consulta completa para repetir los mismos
/// filtros, uno por columna, al paginar.
/// </summary>
public sealed record PayableIndexViewModel(
    SistemaFinanciero.Application.Catalogs.Common.PagedResult<PayableModel> Results,
    PayableQuery Query);

/// <summary>
/// Modelo de la pantalla de detalle: la cuenta por pagar, su historial de pagos y si el usuario puede
/// registrar uno nuevo o anular alguno vigente.
/// </summary>
public sealed record PayableDetailsViewModel(
    PayableModel Payable,
    IReadOnlyList<SupplierPaymentModel> Payments,
    IReadOnlyList<BalanceAdjustmentModel> Adjustments,
    bool CanRegisterPayment,
    bool CanCancelPayment,
    bool CanAdjust);
