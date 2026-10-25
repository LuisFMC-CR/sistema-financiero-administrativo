using SistemaFinanciero.Domain.Adjustments;

namespace SistemaFinanciero.Web.Models.Adjustments;

/// <summary>Centraliza las etiquetas en español de los ajustes de saldo.</summary>
public static class BalanceAdjustmentDisplayExtensions
{
    public static string ToDisplayName(this BalanceAdjustmentDirection direction) => direction switch
    {
        BalanceAdjustmentDirection.Decrease => "Disminución",
        BalanceAdjustmentDirection.Increase => "Aumento",
        _ => "Desconocido",
    };
}
