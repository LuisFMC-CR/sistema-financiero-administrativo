using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceRoundingTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Line_KeepsFourDecimalsInQuantityAndUnitPrice_AndRoundsTheGrossAmountToTwo()
    {
        InvoiceLine line = CreateLine(quantity: 3m, unitPrice: 1.33333m);

        Assert.Equal(1.3333m, line.UnitPrice);
        Assert.Equal(4.00m, line.GrossAmount);
    }

    [Theory]
    [InlineData(0.125, 0.13)]
    [InlineData(0.135, 0.14)]
    [InlineData(0.124, 0.12)]
    public void Line_RoundsMidpointsAwayFromZero(decimal unitPrice, decimal expectedGross)
    {
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: unitPrice);

        Assert.Equal(expectedGross, line.GrossAmount);
    }

    [Fact]
    public void Line_RoundsTheDiscountToTwoDecimals()
    {
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 10m, discountAmount: 0.005m);

        Assert.Equal(0.01m, line.DiscountAmount);
        Assert.Equal(9.99m, line.NetAmount);
    }

    [Fact]
    public void Line_RoundsAPercentageTaxToTwoDecimals()
    {
        // 13 % de 10,05 = 1,3065, que se redondea a 1,31.
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 10.05m, tax: PercentageTax(13m));

        Assert.Equal(1.31m, line.Tax.Amount);
        Assert.Equal(1.31m, line.TaxAmount);
        Assert.Equal(11.36m, line.TotalAmount);
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0.12345, 0.1235)]
    public void Line_KeepsFourDecimalsInTheTaxRate(decimal rate, decimal expectedRate)
    {
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 100m, tax: PercentageTax(rate));

        Assert.Equal(expectedRate, line.Tax.Rate);
    }

    [Fact]
    public void Invoice_SumsRoundedLinesIntoTwoDecimalTotals()
    {
        Invoice invoice = CreateInvoice();
        invoice.AddLine(
            CreateLine(quantity: 1m, unitPrice: 10.05m, tax: PercentageTax(13m)),
            CreatedAtUtc,
            UserId);
        invoice.AddLine(
            CreateLine(quantity: 1m, unitPrice: 0.125m, tax: PercentageTax(13m)),
            CreatedAtUtc,
            UserId);

        Assert.Equal(10.18m, invoice.GrossAmount);
        Assert.Equal(0m, invoice.DiscountAmount);
        Assert.Equal(10.18m, invoice.NetAmount);
        Assert.Equal(1.33m, invoice.TaxAmount);
        Assert.Equal(11.51m, invoice.TotalAmount);
    }

    private static Invoice CreateInvoice() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            PaymentTerm.Cash,
            CreatedAtUtc,
            UserId);

    private static InvoiceLine CreateLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount = 0m,
        InvoiceTaxSpecification? tax = null) =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            "Servicio de soporte",
            "hora",
            quantity,
            unitPrice,
            discountAmount,
            tax ?? ExemptTax());

    private static InvoiceTaxSpecification PercentageTax(decimal rate) =>
        new("IVA", "Impuesto al valor agregado", rate);

    private static InvoiceTaxSpecification ExemptTax() =>
        new("IVA-EXENTO", "IVA exento", 0m);
}
