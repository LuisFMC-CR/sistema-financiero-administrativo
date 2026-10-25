namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Estados admitidos durante el ciclo de vida de una factura administrativa.</summary>
/// <remarks>
/// Conexiones: es el valor de <c>Invoice.Status</c>. <c>Invoice.EnsureDraft</c> solo permite editar en
/// <see cref="Draft"/>; <c>Confirm</c> pasa de <see cref="Draft"/> a <see cref="Confirmed"/>; <c>Cancel</c>
/// pasa de <see cref="Confirmed"/> a <see cref="Cancelled"/>. Todavía no hay pantalla que muestre o
/// cambie este estado, porque no existe un controlador de facturas.
/// </remarks>
public enum InvoiceStatus
{
    /// <summary>Documento editable que aún no afecta las cuentas por cobrar.</summary>
    Draft = 1,

    /// <summary>Documento definitivo que conserva sus valores históricos.</summary>
    Confirmed = 2,

    /// <summary>Documento confirmado reversado sin eliminar su trazabilidad.</summary>
    Cancelled = 3,
}
