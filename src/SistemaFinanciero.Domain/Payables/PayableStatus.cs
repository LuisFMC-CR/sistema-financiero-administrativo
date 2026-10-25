namespace SistemaFinanciero.Domain.Payables;

/// <summary>Estados admitidos durante el ciclo de vida de una cuenta por pagar.</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="Payable.Status"/>. <see cref="Payable.ApplyPayment"/> y
/// <see cref="Payable.RevertPayment"/> alternan entre <see cref="Pending"/> y
/// <see cref="PartiallyPaid"/> (o <see cref="Paid"/> cuando el saldo llega a cero);
/// <see cref="Payable.Cancel"/> pasa a <see cref="Cancelled"/>. Mismo criterio que
/// <c>ReceivableStatus</c>, del lado de ingresos.
/// </remarks>
public enum PayableStatus
{
    /// <summary>Sin pagos aplicados: el saldo es igual al monto original.</summary>
    Pending = 1,

    /// <summary>Con al menos un pago vigente, pero con saldo pendiente.</summary>
    PartiallyPaid = 2,

    /// <summary>El saldo llegó a cero.</summary>
    Paid = 3,

    /// <summary>Anulada junto con el gasto que la originó.</summary>
    Cancelled = 4,
}
