using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Web.Models.Invoices;

/// <summary>Centraliza las etiquetas en español utilizadas en las pantallas de facturación.</summary>
public static class InvoiceDisplayExtensions
{
    public static string ToDisplayName(this PaymentMethod paymentMethod) => paymentMethod switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.BankTransfer => "Transferencia bancaria",
        PaymentMethod.SinpeMobile => "SINPE Móvil",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.Check => "Cheque",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this PaymentTerm paymentTerm) => paymentTerm switch
    {
        PaymentTerm.Cash => "Contado",
        PaymentTerm.Credit30 => "Crédito 30 días",
        PaymentTerm.Credit60 => "Crédito 60 días",
        PaymentTerm.Credit90 => "Crédito 90 días",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this InvoiceStatus status) => status switch
    {
        InvoiceStatus.Draft => "Borrador",
        InvoiceStatus.Confirmed => "Confirmada",
        InvoiceStatus.Cancelled => "Anulada",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this InvoiceStatusFilter status) => status switch
    {
        InvoiceStatusFilter.All => "Todas",
        InvoiceStatusFilter.Draft => "Borradores",
        InvoiceStatusFilter.Confirmed => "Confirmadas",
        InvoiceStatusFilter.Cancelled => "Anuladas",
        _ => "Desconocido",
    };
}
