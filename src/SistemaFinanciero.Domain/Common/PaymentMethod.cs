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

/// <summary>
/// Equivalencia de cada <see cref="PaymentMethod"/> con el código oficial de medio de pago de
/// Hacienda (bloque 3 del paquete de mejoras finales, catálogo "Medios de pago" del Anexo 4.4);
/// solo para preparar la exportación de datos de la factura (RF-88), sin columna nueva.
/// </summary>
/// <remarks>
/// No se confirmó contra el documento oficial, salvo "01" Efectivo; queda pendiente de validar antes
/// de usarse para un envío real.
/// </remarks>
public static class PaymentMethodHaciendaExtensions
{
    public static string ToHaciendaCode(this PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Cash => "01",
        PaymentMethod.Card => "02",
        PaymentMethod.Check => "03",
        PaymentMethod.BankTransfer => "04",
        PaymentMethod.SinpeMobile => "06",
        _ => throw new ArgumentOutOfRangeException(nameof(paymentMethod), paymentMethod, "Medio de pago desconocido."),
    };
}
