using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceLineTaxTests
{
    [Fact]
    public void Line_KeepsTheNetAmountAsTheTaxableBase()
    {
        // Bruto 2 x 100 = 200, descuento 50, neto 150; el 13 % se calcula sobre 150.
        InvoiceLine line = CreateLine(quantity: 2m, unitPrice: 100m, discountAmount: 50m, [Vat(13m)]);

        InvoiceLineTax tax = line.Taxes.Single();

        Assert.Equal(150m, line.NetAmount);
        Assert.Equal(150m, tax.TaxableAmount);
        Assert.Equal(19.50m, tax.Amount);
        Assert.Equal(169.50m, line.TotalAmount);
    }

    [Fact]
    public void Line_KeepsTheNetAmountAsTheBaseOfAFixedAmountTaxToo()
    {
        InvoiceTaxSpecification fixedTax = new(
            Guid.NewGuid(),
            "ESP",
            "Impuesto específico",
            TaxCalculationType.FixedAmountPerUnit,
            2m);

        InvoiceLine line = CreateLine(quantity: 3m, unitPrice: 10m, discountAmount: 0m, [fixedTax]);

        InvoiceLineTax tax = line.Taxes.Single();

        Assert.Equal(30m, tax.TaxableAmount);
        Assert.Equal(6m, tax.Amount);
    }

    [Fact]
    public void Line_KeepsTheTaxSnapshotWithNormalizedCode()
    {
        Guid taxId = Guid.NewGuid();
        InvoiceTaxSpecification specification = new(
            taxId,
            " iva13 ",
            " IVA 13 % ",
            TaxCalculationType.Percentage,
            13m);

        InvoiceLineTax tax = CreateLine(1m, 100m, 0m, [specification]).Taxes.Single();

        Assert.Equal(taxId, tax.Id);
        Assert.Equal("IVA13", tax.Code);
        Assert.Equal("IVA 13 %", tax.Name);
        Assert.Equal(TaxCalculationType.Percentage, tax.CalculationType);
        Assert.Equal(13m, tax.Rate);
    }

    [Fact]
    public void Line_KeepsTheCatalogTaxTypeItCameFromAndNothingWhenNoneWasUsed()
    {
        Guid taxTypeId = Guid.NewGuid();
        InvoiceTaxSpecification fromCatalog = Vat(13m) with { TaxTypeId = taxTypeId };
        InvoiceTaxSpecification adHoc = new(
            Guid.NewGuid(),
            "SEL",
            "Selectivo",
            TaxCalculationType.Percentage,
            5m);

        InvoiceLine line = CreateLine(1m, 100m, 0m, [fromCatalog, adHoc]);

        Assert.Equal(taxTypeId, line.Taxes.Single(tax => tax.Code == "IVA").TaxTypeId);
        Assert.Null(line.Taxes.Single(tax => tax.Code == "SEL").TaxTypeId);
    }

    [Fact]
    public void Line_RejectsAnEmptyCatalogTaxTypeId()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateLine(1m, 100m, 0m, [Vat(13m) with { TaxTypeId = Guid.Empty }]));
    }

    [Fact]
    public void Line_AllowsAZeroRateTaxAndKeepsItsBase()
    {
        InvoiceTaxSpecification exempt = new(
            Guid.NewGuid(),
            "EXE",
            "Exento",
            TaxCalculationType.Percentage,
            0m);

        InvoiceLineTax tax = CreateLine(1m, 80m, 0m, [exempt]).Taxes.Single();

        Assert.Equal(80m, tax.TaxableAmount);
        Assert.Equal(0m, tax.Amount);
    }

    [Fact]
    public void Line_AppliesSeveralDifferentTaxesOverTheSameBase()
    {
        InvoiceTaxSpecification other = new(
            Guid.NewGuid(),
            "SEL",
            "Selectivo",
            TaxCalculationType.Percentage,
            5m);

        InvoiceLine line = CreateLine(1m, 200m, 0m, [Vat(13m), other]);

        Assert.All(line.Taxes, tax => Assert.Equal(200m, tax.TaxableAmount));
        Assert.Equal(36m, line.TaxAmount);
        Assert.Equal(236m, line.TotalAmount);
    }

    [Fact]
    public void Line_RejectsTheSameTaxTwice()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateLine(1m, 100m, 0m, [Vat(13m), Vat(13m)]));
    }

    [Fact]
    public void Line_RejectsANegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(1m, 100m, 0m, [Vat(-1m)]));
    }

    [Fact]
    public void Line_RejectsAnUndefinedCalculationType()
    {
        InvoiceTaxSpecification invalid = new(
            Guid.NewGuid(),
            "XXX",
            "Inválido",
            (TaxCalculationType)99,
            1m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(1m, 100m, 0m, [invalid]));
    }

    private static InvoiceLine CreateLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        IEnumerable<InvoiceTaxSpecification> taxes) =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            "Servicio de soporte",
            "hora",
            quantity,
            unitPrice,
            discountAmount,
            taxes);

    private static InvoiceTaxSpecification Vat(decimal rate) =>
        new(Guid.NewGuid(), "IVA", "Impuesto al valor agregado", TaxCalculationType.Percentage, rate);
}
