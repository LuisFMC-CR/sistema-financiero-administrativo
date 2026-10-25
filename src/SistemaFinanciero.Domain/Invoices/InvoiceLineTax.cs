using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Invoices;

/// <summary>Fotografía del IVA aplicado a una línea de factura.</summary>
/// <remarks>
/// Conexiones: solo <see cref="InvoiceLine"/> la crea (constructor <c>internal</c>), a partir de una
/// <c>InvoiceTaxSpecification</c>. Se guarda como columnas de <c>finanzas.FacturaLineas</c> (tipo
/// propietario de EF Core), vía <c>InvoiceLineConfiguration</c>. <see cref="TaxTypeId"/> apunta
/// opcionalmente a <c>TaxType</c>, con clave foránea restrictiva: no se puede borrar un tipo de
/// impuesto que una línea ya usó.
/// </remarks>
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
    /// Crea la fotografía del IVA y calcula su importe. Solo la línea de factura lo invoca, para que la
    /// base gravable siempre sea el neto de esa línea.
    /// </summary>
    internal InvoiceLineTax(
        InvoiceTaxSpecification specification,
        decimal taxableAmount)
    {
        ArgumentNullException.ThrowIfNull(specification);

        TaxTypeId = DomainRules.OptionalId(specification.TaxTypeId, nameof(specification.TaxTypeId));
        Code = DomainRules.NormalizedCode(specification.Code, CodeMaxLength, nameof(specification.Code));
        Name = DomainRules.RequiredText(specification.Name, NameMaxLength, nameof(specification.Name));
        Rate = NormalizeRate(specification.Rate, nameof(specification.Rate));
        TaxableAmount = taxableAmount;
        Amount = CalculateAmount(Rate, taxableAmount);
    }

    /// <summary>
    /// Tipo de impuesto del catálogo del que se tomó la fotografía; nulo si no se usó un tipo del
    /// catálogo. Los valores de esta fila no dependen de él después de creada.
    /// </summary>
    public Guid? TaxTypeId { get; private set; }

    /// <summary>Código de impuesto vigente al preparar la factura.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Nombre visible del impuesto vigente al preparar la factura.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Porcentaje de IVA utilizado.</summary>
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

    private static decimal CalculateAmount(decimal rate, decimal taxableAmount)
    {
        if (taxableAmount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(taxableAmount),
                "La base gravable debe ser válida.");
        }

        return DomainRules.RoundMoney(taxableAmount * rate / 100m);
    }
}
