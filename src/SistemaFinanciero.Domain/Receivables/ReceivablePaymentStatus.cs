namespace SistemaFinanciero.Domain.Receivables;

/// <summary>Estados admitidos de un abono.</summary>
/// <remarks>Conexiones: es el valor de <see cref="ReceivablePayment.Status"/>.</remarks>
public enum ReceivablePaymentStatus
{
    /// <summary>Vigente: rebaja el saldo de su cuenta por cobrar y mantiene su ingreso.</summary>
    Active = 1,

    /// <summary>Anulado: su saldo fue restituido y su ingreso anulado (regla 4).</summary>
    Cancelled = 2,
}
