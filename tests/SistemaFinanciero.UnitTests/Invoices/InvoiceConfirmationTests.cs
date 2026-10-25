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
    public void Confirm_FixesTheStatusAuditAndNumber()
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Null(invoice.Number);

        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 42L);

        Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
        Assert.Equal(42L, invoice.Number);
        Assert.Equal(ConfirmedAtUtc, invoice.ConfirmedAtUtc);
        Assert.Equal(ConfirmerId, invoice.ConfirmedByUserId);
        Assert.Equal(ConfirmedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(ConfirmerId, invoice.UpdatedByUserId);
    }

    [Fact]
    public void Confirm_RejectsAnInvoiceWithoutLines()
    {
        Invoice invoice = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            PaymentTerm.Cash,
            CreatedAtUtc,
            CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheNumberIsNotPositive(long number)
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm(ConfirmedAtUtc, ConfirmerId, number));

        AssertStillDraft(invoice);
        Assert.Null(invoice.Number);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheUserIsEmpty()
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(ConfirmedAtUtc, Guid.Empty, 1L));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheInstantIsBeforeTheLastChange()
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm(CreatedAtUtc.AddMinutes(-1), ConfirmerId, 1L));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheInstantIsNotUtc()
    {
        Invoice invoice = CreateInvoiceWithLine();
        DateTimeOffset notUtc = new(2026, 9, 10, 12, 30, 0, TimeSpan.FromHours(-6));

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(notUtc, ConfirmerId, 1L));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_RejectsAnInvoiceThatIsNotADraft()
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Confirm(ConfirmedAtUtc.AddMinutes(1), ConfirmerId, 2L));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAuditOfAConfirmedInvoice()
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L);
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
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Cancel("Motivo", ConfirmedAtUtc, ConfirmerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_LeavesTheInvoiceConfirmedWhenTheReasonIsMissing(string reason)
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L);

        Assert.Throws<ArgumentException>(() =>
            invoice.Cancel(reason, ConfirmedAtUtc.AddHours(1), ConfirmerId));

        AssertStillConfirmed(invoice);
    }

    [Fact]
    public void Cancel_LeavesTheInvoiceConfirmedWhenTheUserIsEmpty()
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L);

        Assert.Throws<ArgumentException>(() =>
            invoice.Cancel("Motivo válido", ConfirmedAtUtc.AddHours(1), Guid.Empty));

        AssertStillConfirmed(invoice);
    }

    private static void AssertStillDraft(Invoice invoice)
    {
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
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

    private static Invoice CreateInvoiceWithLine()
    {
        Invoice invoice = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            PaymentTerm.Cash,
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
                tax: new InvoiceTaxSpecification("IVA-EXENTO", "IVA exento", 0m)),
            CreatedAtUtc,
            CreatorId);

        return invoice;
    }
}
