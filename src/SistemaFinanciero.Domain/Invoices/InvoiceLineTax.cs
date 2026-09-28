using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Fotografía de un impuesto aplicado a una línea de factura.</summary>
public sealed class InvoiceLineTax
{
    /// <summary>Máximo de caracteres para el código de impuesto.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Máximo de caracteres para el nombre descriptivo del impuesto.</summary>
    public const int NameMaxLength = 120;

    private InvoiceLineTax()
    {
    }

    /// <summary>
    /// Crea la fotografía de un impuesto y calcula su importe. Solo la línea de factura lo invoca,
    /// para que la base gravable siempre sea el neto de esa línea.
    /// </summary>
    internal InvoiceLineTax(
        InvoiceTaxSpecification specification,
        decimal taxableAmount,
        decimal quantity)
    {
        ArgumentNullException.ThrowIfNull(specification);

        Id = DomainRules.RequiredId(specification.Id, nameof(specification.Id));
        TaxTypeId = DomainRules.OptionalId(specification.TaxTypeId, nameof(specification.TaxTypeId));
        Code = DomainRules.NormalizedCode(specification.Code, CodeMaxLength, nameof(specification.Code));
        Name = DomainRules.RequiredText(specification.Name, NameMaxLength, nameof(specification.Name));
        CalculationType = DomainRules.DefinedEnum(specification.CalculationType, nameof(specification.CalculationType));
        Rate = NormalizeRate(specification.Rate, nameof(specification.Rate));
        TaxableAmount = taxableAmount;
        Amount = CalculateAmount(CalculationType, Rate, taxableAmount, quantity);
    }

    /// <summary>Identificador técnico de la fotografía tributaria.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Tipo de impuesto del catálogo del que se tomó la fotografía; nulo si no se usó un tipo del
    /// catálogo. Los valores de esta fila no dependen de él después de creada.
    /// </summary>
    public Guid? TaxTypeId { get; private set; }

    /// <summary>Código de impuesto vigente al preparar la factura.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Nombre visible del impuesto vigente al preparar la factura.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Método de cálculo aplicado.</summary>
    public TaxCalculationType CalculationType { get; private set; }

    /// <summary>Porcentaje o monto fijo por unidad utilizado.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Base gravable conservada: el neto de la línea (bruto menos descuento).</summary>
    public decimal TaxableAmount { get; private set; }

    /// <summary>Importe tributario resultante, redondeado a dos decimales.</summary>
    public decimal Amount { get; private set; }

    private static decimal NormalizeRate(decimal value, string parameterName)
    {
        decimal roundedValue = DomainRules.Round(value, DomainRules.QuantityDecimalPlaces);

        if (roundedValue < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "La tarifa de impuesto no puede ser negativa.");
        }

        return roundedValue;
    }

    private static decimal CalculateAmount(
        TaxCalculationType calculationType,
        decimal rate,
        decimal taxableAmount,
        decimal quantity)
    {
        if (taxableAmount < 0 || quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taxableAmount),
                "La base gravable y la cantidad deben ser válidas.");
        }

        decimal amount = calculationType switch
        {
            TaxCalculationType.Percentage => taxableAmount * rate / 100m,
            TaxCalculationType.FixedAmountPerUnit => quantity * rate,
            _ => throw new ArgumentOutOfRangeException(nameof(calculationType)),
        };

        return DomainRules.RoundMoney(amount);
    }
}
