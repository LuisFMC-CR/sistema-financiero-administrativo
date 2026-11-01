namespace SistemaFinanciero.Domain.Invoices;

/// <summary>
/// Condición de pago acordada con el cliente para una factura administrativa. Es la lista fija de la
/// regla de negocio 14: no tiene pantalla de mantenimiento ni catálogo propio.
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>Invoice.PaymentTerm</c>. <see cref="PaymentTermDays"/> traduce cada
/// valor a los días de crédito que <c>Invoice</c> usa para calcular <c>DueDate</c> como la fecha de
/// emisión más esos días; el vencimiento nunca se recibe del llamador. Junto con <c>Invoice.DueDate</c>
/// decidirá, cuando exista el servicio de facturación, si confirmar la factura genera directamente un
/// ingreso (<see cref="Cash"/>) o una cuenta por cobrar (cualquier término de crédito). Esa generación
/// todavía no está implementada.
/// </remarks>
public enum PaymentTerm
{
    /// <summary>Se cobra al confirmar la factura y genera su ingreso de inmediato. Sin vencimiento.</summary>
    Cash = 1,

    /// <summary>Crédito a 30 días: el vencimiento es la fecha de emisión más 30 días.</summary>
    Credit30 = 2,

    /// <summary>Crédito a 60 días: el vencimiento es la fecha de emisión más 60 días.</summary>
    Credit60 = 3,

    /// <summary>Crédito a 90 días: el vencimiento es la fecha de emisión más 90 días.</summary>
    Credit90 = 4,
}

/// <summary>
/// Equivalencia de cada <see cref="PaymentTerm"/> con el código oficial de condición de venta de
/// Hacienda y sus días de crédito (bloque 3 del paquete de mejoras finales, catálogo "Condición de
/// venta" del Anexo 4.4); solo para preparar la exportación de datos de la factura (RF-88), sin
/// columna nueva.
/// </summary>
public static class PaymentTermHaciendaExtensions
{
    public static string ToSaleConditionCode(this PaymentTerm paymentTerm) => paymentTerm switch
    {
        PaymentTerm.Cash => "01",
        PaymentTerm.Credit30 or PaymentTerm.Credit60 or PaymentTerm.Credit90 => "02",
        _ => throw new ArgumentOutOfRangeException(nameof(paymentTerm), paymentTerm, "Plazo de pago desconocido."),
    };

    /// <summary>Días de crédito del plazo (0 de contado); ya definidos en <see cref="PaymentTermDays"/>.</summary>
    public static int ToCreditDays(this PaymentTerm paymentTerm) => PaymentTermDays.GetDays(paymentTerm);
}
