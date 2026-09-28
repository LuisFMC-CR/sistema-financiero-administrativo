using SistemaFinanciero.Domain.Currencies;
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
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 10.05m, taxes: [PercentageTax(13m)]);

        Assert.Equal(1.31m, line.Taxes.Single().Amount);
        Assert.Equal(1.31m, line.TaxAmount);
        Assert.Equal(11.36m, line.TotalAmount);
    }

    [Fact]
    public void Line_RoundsAFixedAmountPerUnitTaxToTwoDecimals()
    {
        // 2,5 unidades por 0,3333 = 0,83325, que se redondea a 0,83.
        InvoiceTaxSpecification specification = new(
            Guid.NewGuid(),
            "ESP",
            "Impuesto específico",
            TaxCalculationType.FixedAmountPerUnit,
            0.3333m);

        InvoiceLine line = CreateLine(quantity: 2.5m, unitPrice: 100m, taxes: [specification]);

        Assert.Equal(0.83m, line.Taxes.Single().Amount);
    }

    [Theory]
    [InlineData(0.5, 0.5)]
    [InlineData(0.12345, 0.1235)]
    public void Line_KeepsFourDecimalsInTheTaxRate(decimal rate, decimal expectedRate)
    {
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 100m, taxes: [PercentageTax(rate)]);

        Assert.Equal(expectedRate, line.Taxes.Single().Rate);
    }

    [Fact]
    public void Invoice_SumsRoundedLinesIntoTwoDecimalTotals()
    {
        Invoice invoice = CreateInvoice(CurrencyCode.CRC);
        invoice.AddLine(
            CreateLine(quantity: 1m, unitPrice: 10.05m, taxes: [PercentageTax(13m)]),
            CreatedAtUtc,
            UserId);
        invoice.AddLine(
            CreateLine(quantity: 1m, unitPrice: 0.125m, taxes: [PercentageTax(13m)]),
            CreatedAtUtc,
            UserId);

        Assert.Equal(10.18m, invoice.GrossAmount);
        Assert.Equal(0m, invoice.DiscountAmount);
        Assert.Equal(10.18m, invoice.NetAmount);
        Assert.Equal(1.33m, invoice.TaxAmount);
        Assert.Equal(11.51m, invoice.TotalAmount);
    }

    [Fact]
    public void Confirm_KeepsSixDecimalsInTheUsdRate()
    {
        Invoice invoice = CreateInvoice(CurrencyCode.USD);
        invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 100m), CreatedAtUtc, UserId);

        invoice.Confirm(512.1234567m, CreatedAtUtc.AddMinutes(5), UserId);

        Assert.Equal(512.123457m, invoice.ConfirmedCrcPerUsd);
    }

    private static Invoice CreateInvoice(CurrencyCode currency) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            currency,
            PaymentTerm.Cash,
            dueDate: null,
            CreatedAtUtc,
            UserId);

    private static InvoiceLine CreateLine(
        decimal quantity,
        decimal unitPrice,
        decimal discountAmount = 0m,
        IEnumerable<InvoiceTaxSpecification>? taxes = null) =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            "Servicio de soporte",
            "hora",
            quantity,
            unitPrice,
            discountAmount,
            taxes);

    private static InvoiceTaxSpecification PercentageTax(decimal rate) =>
        new(
            Guid.NewGuid(),
            "IVA",
            "Impuesto al valor agregado",
            TaxCalculationType.Percentage,
            rate);
}
