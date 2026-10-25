using SistemaFinanciero.Application.Incomes;
using SistemaFinanciero.Domain.Incomes;

namespace SistemaFinanciero.Web.Models.Incomes;

/// <summary>Centraliza las etiquetas en español utilizadas en las pantallas de ingresos.</summary>
public static class IncomeDisplayExtensions
{
    public static string ToDisplayName(this IncomeOrigin origin) => origin switch
    {
        IncomeOrigin.InvoiceCash => "Factura de contado",
        IncomeOrigin.ReceivablePayment => "Abono",
        IncomeOrigin.Manual => "Manual",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this IncomeOriginFilter origin) => origin switch
    {
        IncomeOriginFilter.All => "Todos",
        IncomeOriginFilter.InvoiceCash => "Factura de contado",
        IncomeOriginFilter.ReceivablePayment => "Abono",
        IncomeOriginFilter.Manual => "Manual",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this IncomeStatus status) => status switch
    {
        IncomeStatus.Active => "Vigente",
        IncomeStatus.Cancelled => "Anulado",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this IncomeStatusFilter status) => status switch
    {
        IncomeStatusFilter.All => "Todos",
        IncomeStatusFilter.Active => "Vigentes",
        IncomeStatusFilter.Cancelled => "Anulados",
        _ => "Desconocido",
    };
}
