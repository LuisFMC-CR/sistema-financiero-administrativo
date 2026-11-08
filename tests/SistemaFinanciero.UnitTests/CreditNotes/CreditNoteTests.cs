using SistemaFinanciero.Domain.CreditNotes;

namespace SistemaFinanciero.UnitTests.CreditNotes;

public sealed class CreditNoteTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private const string SampleKey = "12345678901234567890123456789012345678901234567890";
    private const string SampleConsecutivo = "12345678901234567890";

    [Fact]
    public void Constructor_PreservesAmountsForBalanceRefundAndWithholding()
    {
        CreditNote note = CreateNote(
            appliedToBalance: 50m,
            refundedAmount: 50m,
            withholdingReversedAmount: 13m,
            cashAccountId: Guid.NewGuid(),
            refundReference: "DEV-0001");

        Assert.Equal(100m, note.NetAmount);
        Assert.Equal(13m, note.TaxAmount);
        Assert.Equal(113m, note.TotalAmount);
        Assert.Equal(50m, note.AppliedToBalance);
        Assert.Equal(50m, note.RefundedAmount);
        Assert.Equal(13m, note.WithholdingReversedAmount);
        Assert.Equal(CreditNoteStatus.Active, note.Status);
    }

    [Fact]
    public void Constructor_RejectsAmountsThatDoNotReconcileWithTheInvoiceTotal()
    {
        Assert.Throws<ArgumentException>(() => CreateNote(
            appliedToBalance: 50m,
            refundedAmount: 49m,
            withholdingReversedAmount: 13m,
            cashAccountId: Guid.NewGuid(),
            refundReference: "DEV-0001"));
    }

    [Fact]
    public void Constructor_RequiresCashAccountAndReferenceWhenRefunding()
    {
        Assert.Throws<ArgumentException>(() => CreateNote(
            appliedToBalance: 0m,
            refundedAmount: 113m,
            withholdingReversedAmount: 0m,
            cashAccountId: null,
            refundReference: null));
    }

    private static CreditNote CreateNote(
        decimal appliedToBalance,
        decimal refundedAmount,
        decimal withholdingReversedAmount,
        Guid? cashAccountId,
        string? refundReference)
    {
        CreditNoteLine line = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            netAmount: 100m,
            taxRate: 13m,
            taxAmount: 13m);

        return new CreditNote(
            Guid.NewGuid(),
            "NCR-000001",
            Guid.NewGuid(),
            new DateOnly(2026, 11, 6),
            "Devolución total",
            appliedToBalance,
            refundedAmount,
            withholdingReversedAmount,
            cashAccountId,
            refundReference,
            SampleKey,
            SampleConsecutivo,
            [line],
            CreatedAtUtc,
            ActorId);
    }

    [Fact]
    public void Constructor_RejectsAnElectronicCreditNoteKeyThatIsNotFiftyDigits()
    {
        CreditNoteLine line = new(Guid.NewGuid(), Guid.NewGuid(), netAmount: 100m, taxRate: 13m, taxAmount: 13m);

        Assert.Throws<ArgumentException>(() => new CreditNote(
            Guid.NewGuid(),
            "NCR-000001",
            Guid.NewGuid(),
            new DateOnly(2026, 11, 6),
            "Devolución total",
            appliedToBalance: 113m,
            refundedAmount: 0m,
            withholdingReversedAmount: 0m,
            cashAccountId: null,
            refundReference: null,
            electronicCreditNoteKey: "123",
            SampleConsecutivo,
            [line],
            CreatedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsAnElectronicCreditNoteNumberThatIsNotTwentyDigits()
    {
        CreditNoteLine line = new(Guid.NewGuid(), Guid.NewGuid(), netAmount: 100m, taxRate: 13m, taxAmount: 13m);

        Assert.Throws<ArgumentException>(() => new CreditNote(
            Guid.NewGuid(),
            "NCR-000001",
            Guid.NewGuid(),
            new DateOnly(2026, 11, 6),
            "Devolución total",
            appliedToBalance: 113m,
            refundedAmount: 0m,
            withholdingReversedAmount: 0m,
            cashAccountId: null,
            refundReference: null,
            SampleKey,
            electronicCreditNoteNumber: "123",
            [line],
            CreatedAtUtc,
            ActorId));
    }
}
