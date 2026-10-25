namespace SistemaFinanciero.Domain.Common;

/// <summary>Medio por el que se recibe o se hace un pago.</summary>
/// <remarks>
/// Es un enum compartido (no pertenece a un catálogo editable): lo usa <c>Income</c> para el ingreso
/// de una factura de contado o manual, y lo usarán los pagos a proveedores del paso 3 del roadmap.
/// </remarks>
public enum PaymentMethod
{
    /// <summary>Efectivo.</summary>
    Cash = 1,

    /// <summary>Transferencia bancaria.</summary>
    BankTransfer = 2,

    /// <summary>SINPE Móvil.</summary>
    SinpeMobile = 3,

    /// <summary>Tarjeta.</summary>
    Card = 4,

    /// <summary>Cheque.</summary>
    Check = 5,
}
