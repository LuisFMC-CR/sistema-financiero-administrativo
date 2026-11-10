namespace SistemaFinanciero.Domain.Audit;

/// <summary>Tipo de documento financiero registrado en la bitácora de operaciones (regla 4/16).</summary>
/// <remarks>
/// Se amplía a medida que se construyen los módulos siguientes del roadmap (gastos, cuentas por
/// pagar, pagos, presupuesto); los valores ya asignados no se reutilizan ni se renumeran.
/// </remarks>
public enum OperationLogEntityType
{
    /// <summary>Factura administrativa.</summary>
    Invoice = 1,

    /// <summary>Cuenta por cobrar.</summary>
    Receivable = 2,

    /// <summary>Abono a una cuenta por cobrar.</summary>
    ReceivablePayment = 3,

    /// <summary>Ingreso, automático o manual.</summary>
    Income = 4,

    /// <summary>Gasto.</summary>
    Expense = 5,

    /// <summary>Cuenta por pagar.</summary>
    Payable = 6,

    /// <summary>Pago a proveedor.</summary>
    SupplierPayment = 7,

    /// <summary>Nota de crédito interna que anula una factura.</summary>
    CreditNote = 8,

    /// <summary>Traspaso entre dos cuentas de efectivo propias.</summary>
    CashTransfer = 9,

    /// <summary>Liquidación mensual del IVA.</summary>
    VatSettlement = 10,

    /// <summary>Pago de impuesto sobre la renta a Hacienda (RF-99).</summary>
    IncomeTaxPayment = 11,
}
