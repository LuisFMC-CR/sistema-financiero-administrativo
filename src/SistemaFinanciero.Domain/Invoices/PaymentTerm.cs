namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Condición de pago acordada con el cliente para una factura administrativa.</summary>
public enum PaymentTerm
{
    /// <summary>Se cobra al confirmar la factura y genera su ingreso de inmediato.</summary>
    Cash = 1,

    /// <summary>Queda como cuenta por cobrar con una fecha de vencimiento.</summary>
    Credit = 2,
}
