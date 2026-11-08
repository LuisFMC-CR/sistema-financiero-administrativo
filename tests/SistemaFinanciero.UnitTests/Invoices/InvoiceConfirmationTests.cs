using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceConfirmationTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ConfirmedAtUtc = CreatedAtUtc.AddMinutes(5);
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ConfirmerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private const string SampleKey = "12345678901234567890123456789012345678901234567890";
    private const string SampleConsecutivo = "12345678901234567890";

    [Fact]
    public void Confirm_FixesTheStatusAuditNumberAndElectronicKey()
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Null(invoice.Number);

        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 42L, SampleKey, SampleConsecutivo);

        Assert.Equal(InvoiceStatus.Confirmed, invoice.Status);
        Assert.Equal(42L, invoice.Number);
        Assert.Equal(ConfirmedAtUtc, invoice.ConfirmedAtUtc);
        Assert.Equal(ConfirmerId, invoice.ConfirmedByUserId);
        Assert.Equal(ConfirmedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(ConfirmerId, invoice.UpdatedByUserId);
        Assert.Equal(SampleKey, invoice.ElectronicInvoiceKey);
        Assert.Equal(SampleConsecutivo, invoice.ElectronicInvoiceNumber);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abc")]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheElectronicKeyIsNotFiftyDigits(string invalidKey)
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, invalidKey, SampleConsecutivo));

        AssertStillDraft(invoice);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abc")]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheElectronicNumberIsNotTwentyDigits(string invalidNumber)
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, SampleKey, invalidNumber));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_RejectsAnInvoiceWithoutLines()
    {
        Invoice invoice = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            PaymentTerm.Cash,
            CreatedAtUtc,
            CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, SampleKey, SampleConsecutivo));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheNumberIsNotPositive(long number)
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm(ConfirmedAtUtc, ConfirmerId, number, SampleKey, SampleConsecutivo));

        AssertStillDraft(invoice);
        Assert.Null(invoice.Number);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheUserIsEmpty()
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(ConfirmedAtUtc, Guid.Empty, 1L, SampleKey, SampleConsecutivo));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheInstantIsBeforeTheLastChange()
    {
        Invoice invoice = CreateInvoiceWithLine();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.Confirm(CreatedAtUtc.AddMinutes(-1), ConfirmerId, 1L, SampleKey, SampleConsecutivo));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_LeavesTheInvoiceAsDraftWhenTheInstantIsNotUtc()
    {
        Invoice invoice = CreateInvoiceWithLine();
        DateTimeOffset notUtc = new(2026, 9, 10, 12, 30, 0, TimeSpan.FromHours(-6));

        Assert.Throws<ArgumentException>(() =>
            invoice.Confirm(notUtc, ConfirmerId, 1L, SampleKey, SampleConsecutivo));

        AssertStillDraft(invoice);
    }

    [Fact]
    public void Confirm_RejectsAnInvoiceThatIsNotADraft()
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, SampleKey, SampleConsecutivo);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.Confirm(ConfirmedAtUtc.AddMinutes(1), ConfirmerId, 2L, SampleKey, SampleConsecutivo));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAuditOfAConfirmedInvoice()
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, SampleKey, SampleConsecutivo);
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
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, SampleKey, SampleConsecutivo);

        Assert.Throws<ArgumentException>(() =>
            invoice.Cancel(reason, ConfirmedAtUtc.AddHours(1), ConfirmerId));

        AssertStillConfirmed(invoice);
    }

    [Fact]
    public void Cancel_LeavesTheInvoiceConfirmedWhenTheUserIsEmpty()
    {
        Invoice invoice = CreateInvoiceWithLine();
        invoice.Confirm(ConfirmedAtUtc, ConfirmerId, 1L, SampleKey, SampleConsecutivo);

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
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            PaymentTerm.Cash,
            CreatedAtUtc,
            CreatorId);

        invoice.AddLine(
            new InvoiceLine(
                Guid.NewGuid(),
                catalogItemId: null,
                cabysCode: null,
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
