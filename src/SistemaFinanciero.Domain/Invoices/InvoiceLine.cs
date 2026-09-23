using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Línea inmutable de una factura, con su descripción e impuestos históricos.</summary>
public sealed class InvoiceLine
{
    /// <summary>Máximo de caracteres para la descripción facturada.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Máximo de caracteres para la unidad visible.</summary>
    public const int UnitOfMeasureMaxLength = 30;

    private readonly List<InvoiceLineTax> taxes = [];

    private InvoiceLine()
    {
    }

    /// <summary>Crea una línea con importes calculados a cuatro decimales.</summary>
    public InvoiceLine(
        Guid id,
        Guid? catalogItemId,
        string description,
        string unitOfMeasure,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        IEnumerable<InvoiceLineTax>? taxes)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        CatalogItemId = DomainRules.OptionalId(catalogItemId, nameof(catalogItemId));
        Description = DomainRules.RequiredText(description, DescriptionMaxLength, nameof(description));
        UnitOfMeasure = DomainRules.RequiredText(unitOfMeasure, UnitOfMeasureMaxLength, nameof(unitOfMeasure));
        Quantity = RequirePositive(quantity, nameof(quantity));
        UnitPrice = RequireNonNegative(unitPrice, nameof(unitPrice));
        GrossAmount = Round(Quantity * UnitPrice);
        DiscountAmount = RequireNonNegative(discountAmount, nameof(discountAmount));

        if (DiscountAmount > GrossAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                "El descuento no puede superar el importe bruto de la línea.");
        }

        NetAmount = Round(GrossAmount - DiscountAmount);
        AddTaxes(taxes);
        TaxAmount = Round(this.taxes.Sum(tax => tax.Amount));
        TotalAmount = Round(NetAmount + TaxAmount);
    }

    /// <summary>Identificador técnico estable de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>Artículo de catálogo de origen, cuando se utilizó uno.</summary>
    public Guid? CatalogItemId { get; private set; }

    /// <summary>Descripción conservada aunque el catálogo cambie.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Unidad de medida conservada aunque el catálogo cambie.</summary>
    public string UnitOfMeasure { get; private set; } = string.Empty;

    /// <summary>Cantidad facturada.</summary>
    public decimal Quantity { get; private set; }

    /// <summary>Precio unitario en la moneda de la factura.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>Importe antes de descuentos e impuestos.</summary>
    public decimal GrossAmount { get; private set; }

    /// <summary>Descuento monetario aplicado a la línea.</summary>
    public decimal DiscountAmount { get; private set; }

    /// <summary>Base gravable de la línea.</summary>
    public decimal NetAmount { get; private set; }

    /// <summary>Total de impuestos de la línea.</summary>
    public decimal TaxAmount { get; private set; }

    /// <summary>Total final de la línea.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Impuestos aplicados a la línea.</summary>
    public IReadOnlyCollection<InvoiceLineTax> Taxes => taxes.AsReadOnly();

    private void AddTaxes(IEnumerable<InvoiceLineTax>? lineTaxes)
    {
        if (lineTaxes is null)
        {
            return;
        }

        foreach (InvoiceLineTax tax in lineTaxes)
        {
            ArgumentNullException.ThrowIfNull(tax);

            if (taxes.Any(existing => existing.Code == tax.Code))
            {
                throw new ArgumentException("No se puede repetir un impuesto en la misma línea.", nameof(lineTaxes));
            }

            taxes.Add(tax);
        }
    }

    private static decimal RequirePositive(decimal value, string parameterName)
    {
        decimal roundedValue = Round(value);
        return roundedValue > 0
            ? roundedValue
            : throw new ArgumentOutOfRangeException(parameterName, value, "El valor debe ser mayor que cero.");
    }

    private static decimal RequireNonNegative(decimal value, string parameterName)
    {
        decimal roundedValue = Round(value);
        return roundedValue >= 0
            ? roundedValue
            : throw new ArgumentOutOfRangeException(parameterName, value, "El valor no puede ser negativo.");
    }

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
