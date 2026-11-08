using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.CreditNotes;

/// <summary>Fotografía de la línea original acreditada por una nota de crédito total.</summary>
public sealed class CreditNoteLine
{
    private CreditNoteLine()
    {
    }

    public CreditNoteLine(
        Guid id,
        Guid invoiceLineId,
        decimal netAmount,
        decimal taxRate,
        decimal taxAmount)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        InvoiceLineId = DomainRules.RequiredId(invoiceLineId, nameof(invoiceLineId));
        NetAmount = RequireNonNegative(netAmount, DomainRules.MoneyDecimalPlaces, nameof(netAmount));
        TaxRate = RequireNonNegative(taxRate, DomainRules.QuantityDecimalPlaces, nameof(taxRate));
        TaxAmount = RequireNonNegative(taxAmount, DomainRules.MoneyDecimalPlaces, nameof(taxAmount));
        TotalAmount = DomainRules.RoundMoney(NetAmount + TaxAmount);

        if (TaxAmount != DomainRules.RoundMoney(NetAmount * TaxRate / 100m))
        {
            throw new ArgumentException("El IVA de la línea no coincide con su tarifa.", nameof(taxAmount));
        }
    }

    public Guid Id { get; private set; }

    public Guid CreditNoteId { get; private set; }

    public Guid InvoiceLineId { get; private set; }

    public decimal NetAmount { get; private set; }

    public decimal TaxRate { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    private static decimal RequireNonNegative(decimal value, int decimalPlaces, string parameterName)
    {
        decimal rounded = DomainRules.Round(value, decimalPlaces);
        return rounded >= 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto no puede ser negativo.");
    }
}
