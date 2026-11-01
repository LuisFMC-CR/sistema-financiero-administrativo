namespace SistemaFinanciero.Domain.Ledger;

/// <summary>
/// Origen de un asiento contable automático (RF-78, bloque 5 del paquete de mejoras finales).
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>JournalEntry.SourceType</c>; junto con <c>JournalEntry.SourceId</c>
/// identifica el documento que generó el asiento, para "Ver asiento" y para la reversión (RF-79). Los
/// ingresos de factura de contado y de abono no tienen tipo propio: su efecto ya está en el asiento de
/// la factura o del abono.
/// </remarks>
public enum JournalEntrySourceType
{
    /// <summary>Factura confirmada, de contado o a crédito.</summary>
    Invoice = 1,

    /// <summary>Abono a una cuenta por cobrar.</summary>
    ReceivablePayment = 2,

    /// <summary>Ingreso manual.</summary>
    Income = 3,

    /// <summary>Gasto confirmado, de contado o a crédito.</summary>
    Expense = 4,

    /// <summary>Pago a proveedor.</summary>
    SupplierPayment = 5,

    /// <summary>Ajuste manual de saldo de una cuenta por cobrar o por pagar.</summary>
    BalanceAdjustment = 6,
}
