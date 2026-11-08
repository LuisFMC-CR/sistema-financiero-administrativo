using SistemaFinanciero.Domain.Ledger;

namespace SistemaFinanciero.Web.Models.Ledger;

/// <summary>Etiquetas en español para los asientos contables automáticos (bloque 5 del paquete de mejoras finales).</summary>
public static class JournalEntryDisplayExtensions
{
    public static string ToDisplayName(this JournalEntrySourceType type) => type switch
    {
        JournalEntrySourceType.Invoice => "Factura",
        JournalEntrySourceType.ReceivablePayment => "Abono",
        JournalEntrySourceType.Income => "Ingreso manual",
        JournalEntrySourceType.Expense => "Gasto",
        JournalEntrySourceType.SupplierPayment => "Pago a proveedor",
        JournalEntrySourceType.BalanceAdjustment => "Ajuste de saldo",
        JournalEntrySourceType.CreditNote => "Nota de crédito",
        JournalEntrySourceType.CashTransfer => "Traspaso",
        _ => "Desconocido",
    };
}
