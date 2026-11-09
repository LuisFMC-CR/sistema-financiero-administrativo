namespace SistemaFinanciero.Domain.VatSettlements;

/// <summary>Estados admitidos durante el ciclo de vida de una liquidación mensual del IVA.</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="VatSettlement.Status"/>. <see cref="VatSettlement.Cancel"/>
/// pasa de <see cref="Active"/> a <see cref="Cancelled"/>. Mismo criterio que
/// <c>CashTransferStatus</c>.
/// </remarks>
public enum VatSettlementStatus
{
    /// <summary>Liquidación vigente.</summary>
    Active = 1,

    /// <summary>Liquidación anulada; su reversión ya se contabilizó.</summary>
    Cancelled = 2,
}
