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

    /// <summary>Crea un impuesto histórico y calcula su importe sobre la línea indicada.</summary>
    public InvoiceLineTax(
        Guid id,
        string code,
        string name,
        TaxCalculationType calculationType,
        decimal rate,
        decimal taxableAmount,
        decimal quantity)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        CalculationType = DomainRules.DefinedEnum(calculationType, nameof(calculationType));
        Rate = NormalizeRate(rate, nameof(rate));
        Amount = CalculateAmount(CalculationType, Rate, taxableAmount, quantity);
    }

    /// <summary>Identificador técnico de la fotografía tributaria.</summary>
    public Guid Id { get; private set; }

    /// <summary>Código de impuesto vigente al preparar la factura.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Nombre visible del impuesto vigente al preparar la factura.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Método de cálculo aplicado.</summary>
    public TaxCalculationType CalculationType { get; private set; }

    /// <summary>Porcentaje o monto fijo por unidad utilizado.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Importe tributario resultante, conservado con cuatro decimales.</summary>
    public decimal Amount { get; private set; }

    private static decimal NormalizeRate(decimal value, string parameterName)
    {
        decimal roundedValue = decimal.Round(value, 4, MidpointRounding.AwayFromZero);

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

        return decimal.Round(amount, 4, MidpointRounding.AwayFromZero);
    }
}
