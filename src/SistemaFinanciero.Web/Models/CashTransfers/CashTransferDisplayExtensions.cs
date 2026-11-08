using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Domain.CashTransfers;

namespace SistemaFinanciero.Web.Models.CashTransfers;

/// <summary>Nombres en español para los enums de traspasos (paquete v8, bloque 1).</summary>
public static class CashTransferDisplayExtensions
{
    public static string ToDisplayName(this CashTransferStatus status) => status switch
    {
        CashTransferStatus.Active => "Vigente",
        CashTransferStatus.Cancelled => "Anulado",
        _ => status.ToString(),
    };

    public static string ToDisplayName(this CashTransferStatusFilter status) => status switch
    {
        CashTransferStatusFilter.All => "Todos",
        CashTransferStatusFilter.Active => "Vigente",
        CashTransferStatusFilter.Cancelled => "Anulado",
        _ => status.ToString(),
    };
}
