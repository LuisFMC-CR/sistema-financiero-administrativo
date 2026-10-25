namespace SistemaFinanciero.Domain.Invoices;

/// <summary>
/// Días de crédito fijos de cada <see cref="PaymentTerm"/> (regla de negocio 14), usados por
/// <c>Invoice</c> para calcular <c>DueDate</c> como la fecha de emisión más esos días.
/// </summary>
/// <remarks>
/// Conexiones: es <c>internal</c>, así que solo lo usa <c>Invoice</c> dentro de este mismo proyecto
/// (Domain). No es un catálogo: la lista de plazos es fija y no tiene pantalla de mantenimiento.
/// </remarks>
internal static class PaymentTermDays
{
    public static int GetDays(PaymentTerm paymentTerm) => paymentTerm switch
    {
        PaymentTerm.Cash => 0,
        PaymentTerm.Credit30 => 30,
        PaymentTerm.Credit60 => 60,
        PaymentTerm.Credit90 => 90,
        _ => throw new ArgumentOutOfRangeException(
            nameof(paymentTerm),
            paymentTerm,
            "El plazo de pago indicado no tiene días de crédito definidos."),
    };
}
