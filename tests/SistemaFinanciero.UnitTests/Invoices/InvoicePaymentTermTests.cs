using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoicePaymentTermTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly IssueDate = new(2026, 9, 10);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_LeavesTheDueDateEmptyForCash()
    {
        Invoice invoice = CreateInvoice(PaymentTerm.Cash);

        Assert.Equal(PaymentTerm.Cash, invoice.PaymentTerm);
        Assert.Null(invoice.DueDate);
    }

    [Theory]
    [InlineData(PaymentTerm.Credit30, 30)]
    [InlineData(PaymentTerm.Credit60, 60)]
    [InlineData(PaymentTerm.Credit90, 90)]
    public void Constructor_CalculatesTheDueDateAsTheIssueDatePlusTheDaysOfTheTerm(
        PaymentTerm paymentTerm,
        int days)
    {
        Invoice invoice = CreateInvoice(paymentTerm);

        Assert.Equal(paymentTerm, invoice.PaymentTerm);
        Assert.Equal(IssueDate.AddDays(days), invoice.DueDate);
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedPaymentTerm()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInvoice((PaymentTerm)99));
    }

    private static Invoice CreateInvoice(PaymentTerm paymentTerm) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            IssueDate,
            paymentTerm,
            CreatedAtUtc,
            UserId);
}
