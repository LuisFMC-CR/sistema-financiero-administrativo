using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Línea inmutable de una factura, con su descripción y su IVA histórico.</summary>
/// <remarks>
/// Conexiones: vive dentro de un <see cref="Invoice"/> (<c>Invoice.AddLine</c>/<c>ReplaceLine</c>);
/// <see cref="CatalogItemId"/> apunta opcionalmente a <c>CatalogItem</c>. Se guarda en
/// <c>finanzas.FacturaLineas</c> vía <c>InvoiceLineConfiguration</c>. Recibe una
/// <c>InvoiceTaxSpecification</c> obligatoria para construir su <see cref="Tax"/>.
/// </remarks>
public sealed class InvoiceLine
{
    /// <summary>Máximo de caracteres para la descripción facturada.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Máximo de caracteres para la unidad visible.</summary>
    public const int UnitOfMeasureMaxLength = 30;

    private InvoiceLine()
    {
    }

    /// <summary>
    /// Crea una línea. Cantidad y precio unitario usan cuatro decimales; los importes, dos. Toda línea
    /// lleva una sola tarifa de IVA, incluso cuando es la tarifa Exento (0 %).
    /// </summary>
    public InvoiceLine(
        Guid id,
        Guid? catalogItemId,
        string description,
        string unitOfMeasure,
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        InvoiceTaxSpecification tax)
    {
        ArgumentNullException.ThrowIfNull(tax);

        Id = DomainRules.RequiredId(id, nameof(id));
        CatalogItemId = DomainRules.OptionalId(catalogItemId, nameof(catalogItemId));
        Description = DomainRules.RequiredText(description, DescriptionMaxLength, nameof(description));
        UnitOfMeasure = DomainRules.RequiredText(unitOfMeasure, UnitOfMeasureMaxLength, nameof(unitOfMeasure));
        Quantity = RequirePositive(quantity, nameof(quantity));
        UnitPrice = RequireNonNegative(unitPrice, DomainRules.QuantityDecimalPlaces, nameof(unitPrice));
        GrossAmount = DomainRules.RoundMoney(Quantity * UnitPrice);
        DiscountAmount = RequireNonNegative(discountAmount, DomainRules.MoneyDecimalPlaces, nameof(discountAmount));

        if (DiscountAmount > GrossAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountAmount),
                "El descuento no puede superar el importe bruto de la línea.");
        }

        NetAmount = DomainRules.RoundMoney(GrossAmount - DiscountAmount);
        // La base gravable es siempre el neto de esta línea, nunca un valor recibido de fuera.
        Tax = new InvoiceLineTax(tax, NetAmount);
        TaxAmount = Tax.Amount;
        TotalAmount = DomainRules.RoundMoney(NetAmount + TaxAmount);
    }

    /// <summary>Identificador técnico estable de la línea.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Posición de la línea dentro de su factura, desde 1. La asigna la factura al agregarla, por lo que
    /// vale 0 mientras la línea no pertenece a ninguna.
    /// </summary>
    public int Position { get; private set; }

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

    /// <summary>IVA aplicado a la línea.</summary>
    public InvoiceLineTax Tax { get; private set; } = null!;

    /// <summary>Fija la posición de la línea. Solo la factura que la contiene la administra.</summary>
    internal void MoveTo(int position)
    {
        Position = position > 0
            ? position
            : throw new ArgumentOutOfRangeException(nameof(position), position, "La posición debe ser mayor que cero.");
    }

    private static decimal RequirePositive(decimal value, string parameterName)
    {
        decimal roundedValue = DomainRules.Round(value, DomainRules.QuantityDecimalPlaces);
        return roundedValue > 0
            ? roundedValue
            : throw new ArgumentOutOfRangeException(parameterName, value, "El valor debe ser mayor que cero.");
    }

    private static decimal RequireNonNegative(decimal value, int decimalPlaces, string parameterName)
    {
        decimal roundedValue = DomainRules.Round(value, decimalPlaces);
        return roundedValue >= 0
            ? roundedValue
            : throw new ArgumentOutOfRangeException(parameterName, value, "El valor no puede ser negativo.");
    }
}
