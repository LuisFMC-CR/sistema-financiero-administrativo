using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceConfirmationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ConfirmedAtUtc = CreatedAtUtc.AddMinutes(5);
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ConfirmerId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Confirm_FixesTheStatusAndTheAuditOfACrcInvoiceWithoutRate()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);

        invoice.Confirm(null, ConfirmedAtUtc, ConfirmerId);

        Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
        Assert.Null(invoice.ConfirmedCrcPerUsd);
        Assert.Equal(ConfirmedAtUtc, invoice.ConfirmedAtUtc);
        Assert.Equal(ConfirmerId, invoice.ConfirmedByUserId);
        Assert.Equal(ConfirmedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(ConfirmerId, invoice.UpdatedByUserId);
    }

    [Fact]
    public void Confirm_IgnoresTheRateOfACrcInvoice()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);

        invoice.Confirm(512m, ConfirmedAtUtc, ConfirmerId);

        Assert.Null(invoice.ConfirmedCrcPerUsd);
    }

    [Fact]
    public void Confirm_KeepsTheRateOfAUsdInvoice()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.USD);

        invoice.Confirm(512.5m, ConfirmedAtUtc, ConfirmerId);

        Assert.Equal(512.5m, invoice.ConfirmedCrcPerUsd);
    }

    [Fact]
    public void Confirm_RejectsAUsdInvoiceWithoutARate()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.USD);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm(null, ConfirmedAtUtc, ConfirmerId));

        AssertStillDraft(invoice);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void Confirm_RejectsAUsdInvoiceWithoutAPositiveRate(double rate)
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.USD);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm((decimal)rate, ConfirmedAtUtc, ConfirmerId));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheUserIsEmpty()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.USD);

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(512m, ConfirmedAtUtc, Guid.Empty));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheInstantIsBeforeTheLastChange()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.USD);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm(512m, CreatedAtUtc.AddMinutes(-1), ConfirmerId));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheInstantIsNotUtc()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.USD);
        DateTimeOffset notUtc = new(2026, 9, 10, 12, 30, 0, TimeSpan.FromHours(-6));

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(512m, notUtc, ConfirmerId));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_RejectsAnInvoiceThatIsNotADraft()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);
        invoice.Confirm(null, ConfirmedAtUtc, ConfirmerId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Confirm(null, ConfirmedAtUtc.AddMinutes(1), ConfirmerId));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAuditOfAConfirmedInvoice()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);
        invoice.Confirm(null, ConfirmedAtUtc, ConfirmerId);
        DateTimeOffset cancelledAtUtc = ConfirmedAtUtc.AddHours(1);

        invoice.Cancel("  Error de digitación  ", cancelledAtUtc, ConfirmerId);

        Assert.Equal(InvoiceStatus.Cancelled, invoice.Status);
        Assert.Equal("Error de digitación", invoice.CancellationReason);
        Assert.Equal(cancelledAtUtc, invoice.CancelledAtUtc);
        Assert.Equal(ConfirmerId, invoice.CancelledByUserId);
        Assert.Equal(cancelledAtUtc, invoice.UpdatedAtUtc);
    }

    [Fact]
    public void Cancel_RejectsADraft()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Cancel("Motivo", ConfirmedAtUtc, ConfirmerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_LeavesTheInvoiceConfirmedWhenTheReasonIsMissing(string reason)
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);
        invoice.Confirm(null, ConfirmedAtUtc, ConfirmerId);

        Assert.Throws<ArgumentException>(() =>
            invoice.Cancel(reason, ConfirmedAtUtc.AddHours(1), ConfirmerId));

        AssertStillConfirmed(invoice);
    }

    [Fact]
    public void Cancel_LeavesTheInvoiceConfirmedWhenTheUserIsEmpty()
    {
        Invoice invoice = CreateInvoiceWithLine(CurrencyCode.CRC);
        invoice.Confirm(null, ConfirmedAtUtc, ConfirmerId);

        Assert.Throws<ArgumentException>(() =>
            invoice.Cancel("Motivo válido", ConfirmedAtUtc.AddHours(1), Guid.Empty));

        AssertStillConfirmed(invoice);
    }

    private static void AssertStillDraft(Invoice invoice)
    {
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Null(invoice.ConfirmedCrcPerUsd);
        Assert.Null(invoice.ConfirmedAtUtc);
        Assert.Null(invoice.ConfirmedByUserId);
        Assert.Equal(CreatedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(CreatorId, invoice.UpdatedByUserId);
    }

    private static void AssertStillConfirmed(Invoice invoice)
    {
        Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
        Assert.Null(invoice.CancellationReason);
        Assert.Null(invoice.CancelledAtUtc);
        Assert.Null(invoice.CancelledByUserId);
        Assert.Equal(ConfirmedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(ConfirmerId, invoice.UpdatedByUserId);
    }

    private static Invoice CreateInvoiceWithLine(CurrencyCode currency)
    {
        Invoice invoice = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            currency,
            PaymentTerm.Cash,
            dueDate: null,
            CreatedAtUtc,
            CreatorId);

        invoice.AddLine(
            new InvoiceLine(
                Guid.NewGuid(),
                catalogItemId: null,
                "Servicio de soporte",
                "hora",
                quantity: 1m,
                unitPrice: 100m,
                discountAmount: 0m,
                taxes: null),
            CreatedAtUtc,
            CreatorId);

        return invoice;
    }
}
