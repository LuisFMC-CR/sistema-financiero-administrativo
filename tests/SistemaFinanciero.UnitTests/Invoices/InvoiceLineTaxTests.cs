using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceLineTaxTests
{
    [Fact]
    public void Line_KeepsTheNetAmountAsTheTaxableBase()
    {
        // Bruto 2 x 100 = 200, descuento 50, neto 150; el 13 % se calcula sobre 150.
        InvoiceLine line = CreateLine(quantity: 2m, unitPrice: 100m, discountAmount: 50m, Vat(13m));

        Assert.Equal(150m, line.NetAmount);
        Assert.Equal(150m, line.Tax.TaxableAmount);
        Assert.Equal(19.50m, line.Tax.Amount);
        Assert.Equal(169.50m, line.TotalAmount);
    }

    [Fact]
    public void Line_KeepsTheTaxSnapshotWithNormalizedCode()
    {
        InvoiceTaxSpecification specification = new(" iva13 ", " IVA 13 % ", 13m);

        InvoiceLineTax tax = CreateLine(1m, 100m, 0m, specification).Tax;

        Assert.Equal("IVA13", tax.Code);
        Assert.Equal("IVA 13 %", tax.Name);
        Assert.Equal(13m, tax.Rate);
    }

    [Fact]
    public void Line_KeepsTheCatalogTaxTypeItCameFromAndNothingWhenNoneWasUsed()
    {
        Guid taxTypeId = Guid.NewGuid();
        InvoiceTaxSpecification fromCatalog = Vat(13m) with { TaxTypeId = taxTypeId };

        InvoiceLine withCatalog = CreateLine(1m, 100m, 0m, fromCatalog);
        InvoiceLine adHoc = CreateLine(1m, 100m, 0m, Vat(13m));

        Assert.Equal(taxTypeId, withCatalog.Tax.TaxTypeId);
        Assert.Null(adHoc.Tax.TaxTypeId);
    }

    [Fact]
    public void Line_RejectsAnEmptyCatalogTaxTypeId()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateLine(1m, 100m, 0m, Vat(13m) with { TaxTypeId = Guid.Empty }));
    }

    [Fact]
    public void Line_AllowsAZeroRateTaxAndKeepsItsBase()
    {
        InvoiceTaxSpecification exempt = new("EXE", "Exento", 0m);

        InvoiceLine line = CreateLine(1m, 80m, 0m, exempt);

        Assert.Equal(80m, line.Tax.TaxableAmount);
        Assert.Equal(0m, line.Tax.Amount);
    }

    [Fact]
    public void Line_RejectsANegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateLine(1m, 100m, 0m, Vat(-1m)));
    }

    [Fact]
    public void Line_RejectsAMissingTax()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CreateLine(1m, 100m, 0m, null!));
    }

    private static InvoiceLine CreateLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount,
        InvoiceTaxSpecification tax) =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            "Servicio de soporte",
            "hora",
            quantity,
            unitPrice,
            discountAmount,
            tax);

    private static InvoiceTaxSpecification Vat(decimal rate) =>
        new("IVA", "Impuesto al valor agregado", rate);
}
