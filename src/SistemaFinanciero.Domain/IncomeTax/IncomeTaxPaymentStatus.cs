namespace SistemaFinanciero.Domain.IncomeTax;

/// <summary>Estados de un pago de impuesto sobre la renta hecho a Hacienda.</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="IncomeTaxPayment.Status"/>. <see cref="IncomeTaxPayment.Cancel"/>
/// pasa de <see cref="Active"/> a <see cref="Cancelled"/>. Mismo criterio que <c>CashTransferStatus</c>.
/// </remarks>
public enum IncomeTaxPaymentStatus
{
    /// <summary>Pago vigente: se resta en la estimación de su período.</summary>
    Active = 1,

    /// <summary>Pago anulado; su reversión ya se contabilizó.</summary>
    Cancelled = 2,
}
