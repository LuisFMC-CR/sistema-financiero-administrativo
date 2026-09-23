namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Estados admitidos durante el ciclo de vida de una factura administrativa.</summary>
public enum InvoiceStatus
{
    /// <summary>Documento editable que aún no afecta las cuentas por cobrar.</summary>
    Draft = 1,

    /// <summary>Documento definitivo que conserva sus valores históricos.</summary>
    Confirmed = 2,

    /// <summary>Documento confirmado reversado sin eliminar su trazabilidad.</summary>
    Cancelled = 3,
}
