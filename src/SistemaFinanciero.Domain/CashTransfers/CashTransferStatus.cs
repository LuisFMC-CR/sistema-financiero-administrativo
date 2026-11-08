namespace SistemaFinanciero.Domain.CashTransfers;

/// <summary>Estados admitidos durante el ciclo de vida de un traspaso entre cuentas de efectivo.</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="CashTransfer.Status"/>. <see cref="CashTransfer.Cancel"/> pasa
/// de <see cref="Active"/> a <see cref="Cancelled"/>. Mismo criterio que <c>SupplierPaymentStatus</c>.
/// </remarks>
public enum CashTransferStatus
{
    /// <summary>Traspaso vigente.</summary>
    Active = 1,

    /// <summary>Traspaso anulado; su reversión ya se contabilizó.</summary>
    Cancelled = 2,
}
