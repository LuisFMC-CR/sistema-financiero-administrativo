using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Receivables;

namespace SistemaFinanciero.Web.Models.Receivables;

/// <summary>Centraliza las etiquetas en español utilizadas en las pantallas de cuentas por cobrar.</summary>
public static class ReceivableDisplayExtensions
{
    public static string ToDisplayName(this ReceivableStatus status) => status switch
    {
        ReceivableStatus.Pending => "Pendiente",
        ReceivableStatus.PartiallyPaid => "Parcialmente pagada",
        ReceivableStatus.Paid => "Pagada",
        ReceivableStatus.Cancelled => "Anulada",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this ReceivableStatusFilter status) => status switch
    {
        ReceivableStatusFilter.All => "Todas",
        ReceivableStatusFilter.Pending => "Pendientes",
        ReceivableStatusFilter.PartiallyPaid => "Parcialmente pagadas",
        ReceivableStatusFilter.Paid => "Pagadas",
        ReceivableStatusFilter.Cancelled => "Anuladas",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this ReceivablePaymentStatus status) => status switch
    {
        ReceivablePaymentStatus.Active => "Vigente",
        ReceivablePaymentStatus.Cancelled => "Anulado",
        _ => "Desconocido",
    };
}
