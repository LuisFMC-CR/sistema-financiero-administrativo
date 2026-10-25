using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Domain.Payables;

namespace SistemaFinanciero.Web.Models.Payables;

/// <summary>Centraliza las etiquetas en español utilizadas en las pantallas de cuentas por pagar y pagos.</summary>
public static class PayableDisplayExtensions
{
    public static string ToDisplayName(this PayableStatus status) => status switch
    {
        PayableStatus.Pending => "Pendiente",
        PayableStatus.PartiallyPaid => "Parcialmente pagada",
        PayableStatus.Paid => "Pagada",
        PayableStatus.Cancelled => "Anulada",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this PayableStatusFilter status) => status switch
    {
        PayableStatusFilter.All => "Todas",
        PayableStatusFilter.Pending => "Pendientes",
        PayableStatusFilter.PartiallyPaid => "Parcialmente pagadas",
        PayableStatusFilter.Paid => "Pagadas",
        PayableStatusFilter.Cancelled => "Anuladas",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this SupplierPaymentStatus status) => status switch
    {
        SupplierPaymentStatus.Active => "Vigente",
        SupplierPaymentStatus.Cancelled => "Anulado",
        _ => "Desconocido",
    };
}
