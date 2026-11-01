using SistemaFinanciero.Domain.Ledger;

namespace SistemaFinanciero.UnitTests.Ledger;

public sealed class JournalEntryTests
{
    private static readonly DateOnly EntryDate = new(2026, 11, 1);
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CashAccountId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid IncomeAccountId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid VatAccountId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Constructor_BalancesTwoLines()
    {
        JournalEntry entry = new(
            Guid.NewGuid(),
            1,
            EntryDate,
            "Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            reversesEntryId: null,
            [new(CashAccountId, 100m, 0m), new(IncomeAccountId, 0m, 100m)],
            CreatedAtUtc,
            ActorId);

        Assert.Equal(2, entry.Lines.Count);
        Assert.Equal(1, entry.Lines.First(line => line.AccountId == CashAccountId).LineNumber);
        Assert.Equal(2, entry.Lines.First(line => line.AccountId == IncomeAccountId).LineNumber);
    }

    [Fact]
    public void Constructor_OmitsLinesInZero()
    {
        JournalEntry entry = new(
            Guid.NewGuid(),
            1,
            EntryDate,
            "Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            reversesEntryId: null,
            [new(CashAccountId, 113m, 0m), new(IncomeAccountId, 0m, 113m), new(VatAccountId, 0m, 0m)],
            CreatedAtUtc,
            ActorId);

        Assert.Equal(2, entry.Lines.Count);
        Assert.DoesNotContain(entry.Lines, line => line.AccountId == VatAccountId);
    }

    [Fact]
    public void Constructor_RejectsFewerThanTwoLinesWithAmount()
    {
        Assert.Throws<ArgumentException>(() => new JournalEntry(
            Guid.NewGuid(),
            1,
            EntryDate,
            "Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            reversesEntryId: null,
            [new(CashAccountId, 100m, 0m), new(IncomeAccountId, 0m, 0m)],
            CreatedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsUnbalancedLines()
    {
        Assert.Throws<ArgumentException>(() => new JournalEntry(
            Guid.NewGuid(),
            1,
            EntryDate,
            "Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            reversesEntryId: null,
            [new(CashAccountId, 100m, 0m), new(IncomeAccountId, 0m, 90m)],
            CreatedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsANonPositiveNumber()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new JournalEntry(
            Guid.NewGuid(),
            0,
            EntryDate,
            "Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            reversesEntryId: null,
            [new(CashAccountId, 100m, 0m), new(IncomeAccountId, 0m, 100m)],
            CreatedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_KeepsTheReversalLink()
    {
        Guid originalId = Guid.NewGuid();

        JournalEntry reversal = new(
            Guid.NewGuid(),
            2,
            EntryDate,
            "Reversión — Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            originalId,
            [new(CashAccountId, 0m, 100m), new(IncomeAccountId, 100m, 0m)],
            CreatedAtUtc,
            ActorId);

        Assert.Equal(originalId, reversal.ReversesEntryId);
    }

    [Fact]
    public void LineConstructor_RejectsBothDebitAndCreditGreaterThanZero()
    {
        Assert.Throws<ArgumentException>(() => new JournalEntry(
            Guid.NewGuid(),
            1,
            EntryDate,
            "Factura 1",
            JournalEntrySourceType.Invoice,
            Guid.NewGuid(),
            reversesEntryId: null,
            [new(CashAccountId, 100m, 100m), new(IncomeAccountId, 0m, 100m)],
            CreatedAtUtc,
            ActorId));
    }
}
