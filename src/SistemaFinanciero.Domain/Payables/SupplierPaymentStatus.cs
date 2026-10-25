namespace SistemaFinanciero.Domain.Payables;

/// <summary>Estados admitidos durante el ciclo de vida de un pago a proveedor.</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="SupplierPayment.Status"/>. <see cref="SupplierPayment.Cancel"/>
/// pasa de <see cref="Active"/> a <see cref="Cancelled"/>. Mismo criterio que
/// <c>ReceivablePaymentStatus</c>, del lado de ingresos.
/// </remarks>
public enum SupplierPaymentStatus
{
    /// <summary>Pago vigente.</summary>
    Active = 1,

    /// <summary>Pago anulado; restituyó el saldo de su cuenta por pagar.</summary>
    Cancelled = 2,
}
