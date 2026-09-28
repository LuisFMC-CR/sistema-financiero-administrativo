using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoicePaymentTermTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateOnly IssueDate = new(2026, 9, 10);
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_AcceptsACashInvoiceWithoutDueDate()
    {
        Invoice invoice = CreateInvoice(PaymentTerm.Cash, dueDate: null);

        Assert.Equal(PaymentTerm.Cash, invoice.PaymentTerm);
        Assert.Null(invoice.DueDate);
    }

    [Fact]
    public void Constructor_RejectsADueDateOnACashInvoice()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateInvoice(PaymentTerm.Cash, IssueDate.AddDays(30)));
    }

    [Fact]
    public void Constructor_AcceptsACreditInvoiceWithADueDate()
    {
        DateOnly dueDate = IssueDate.AddDays(30);

        Invoice invoice = CreateInvoice(PaymentTerm.Credit, dueDate);

        Assert.Equal(PaymentTerm.Credit, invoice.PaymentTerm);
        Assert.Equal(dueDate, invoice.DueDate);
    }

    [Fact]
    public void Constructor_AcceptsACreditInvoiceDueOnTheIssueDate()
    {
        Invoice invoice = CreateInvoice(PaymentTerm.Credit, IssueDate);

        Assert.Equal(IssueDate, invoice.DueDate);
    }

    [Fact]
    public void Constructor_RejectsACreditInvoiceWithoutDueDate()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateInvoice(PaymentTerm.Credit, dueDate: null));
    }

    [Fact]
    public void Constructor_RejectsACreditInvoiceDueBeforeTheIssueDate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInvoice(PaymentTerm.Credit, IssueDate.AddDays(-1)));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedPaymentTerm()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateInvoice((PaymentTerm)99, dueDate: null));
    }

    private static Invoice CreateInvoice(PaymentTerm paymentTerm, DateOnly? dueDate) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            IssueDate,
            CurrencyCode.CRC,
            paymentTerm,
            dueDate,
            CreatedAtUtc,
            UserId);
}
