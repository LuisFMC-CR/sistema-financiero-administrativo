using SistemaFinanciero.Application.VatSettlements;
using SistemaFinanciero.Domain.VatSettlements;

namespace SistemaFinanciero.Web.Models.VatSettlements;

/// <summary>Nombres en español para los enums de liquidaciones de IVA (paquete v8, bloque 2).</summary>
public static class VatSettlementDisplayExtensions
{
    public static string ToDisplayName(this VatSettlementStatus status) => status switch
    {
        VatSettlementStatus.Active => "Vigente",
        VatSettlementStatus.Cancelled => "Anulada",
        _ => status.ToString(),
    };

    public static string ToDisplayName(this VatSettlementStatusFilter status) => status switch
    {
        VatSettlementStatusFilter.All => "Todos",
        VatSettlementStatusFilter.Active => "Vigente",
        VatSettlementStatusFilter.Cancelled => "Anulada",
        _ => status.ToString(),
    };
}
