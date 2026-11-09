using SistemaFinanciero.Application.Ledger;
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
        JournalEntrySourceType.VatSettlement => "Liquidación de IVA",
        _ => "Desconocido",
    };

    /// <summary>
    /// El controlador y la acción Details del documento al que enlaza "ver documento" desde un
    /// asiento (bloque 3 del paquete v8, trazabilidad en ambos sentidos).
    /// </summary>
    public static (string Controller, string Action) ToRoute(this JournalEntryDocumentKind kind) => kind switch
    {
        JournalEntryDocumentKind.Invoice => ("Invoices", "Details"),
        JournalEntryDocumentKind.Receivable => ("Receivables", "Details"),
        JournalEntryDocumentKind.Income => ("Incomes", "Details"),
        JournalEntryDocumentKind.Expense => ("Expenses", "Details"),
        JournalEntryDocumentKind.Payable => ("Payables", "Details"),
        JournalEntryDocumentKind.CreditNote => ("CreditNotes", "Details"),
        JournalEntryDocumentKind.CashTransfer => ("CashTransfers", "Details"),
        JournalEntryDocumentKind.VatSettlement => ("VatSettlements", "Details"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static string ToLinkLabel(this JournalEntryDocumentKind kind) => kind switch
    {
        JournalEntryDocumentKind.Invoice => "Ver factura",
        JournalEntryDocumentKind.Receivable => "Ver cuenta por cobrar",
        JournalEntryDocumentKind.Income => "Ver ingreso",
        JournalEntryDocumentKind.Expense => "Ver gasto",
        JournalEntryDocumentKind.Payable => "Ver cuenta por pagar",
        JournalEntryDocumentKind.CreditNote => "Ver nota de crédito",
        JournalEntryDocumentKind.CashTransfer => "Ver traspaso",
        JournalEntryDocumentKind.VatSettlement => "Ver liquidación de IVA",
        _ => "Ver documento",
    };
}
