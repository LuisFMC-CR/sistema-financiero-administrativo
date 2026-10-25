namespace SistemaFinanciero.Domain.Receivables;

/// <summary>Estados admitidos durante el ciclo de vida de una cuenta por cobrar.</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="Receivable.Status"/>. <see cref="Receivable.ApplyPayment"/>
/// y <see cref="Receivable.RevertPayment"/> alternan entre <see cref="Pending"/> y
/// <see cref="PartiallyPaid"/> (o <see cref="Paid"/> cuando el saldo llega a cero);
/// <see cref="Receivable.Cancel"/> pasa a <see cref="Cancelled"/>.
/// </remarks>
public enum ReceivableStatus
{
    /// <summary>Sin abonos aplicados: el saldo es igual al monto original.</summary>
    Pending = 1,

    /// <summary>Con al menos un abono vigente, pero con saldo pendiente.</summary>
    PartiallyPaid = 2,

    /// <summary>El saldo llegó a cero.</summary>
    Paid = 3,

    /// <summary>Anulada junto con la factura que la originó.</summary>
    Cancelled = 4,
}
