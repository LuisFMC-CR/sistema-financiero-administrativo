using SistemaFinanciero.Domain.Budgets;

namespace SistemaFinanciero.UnitTests.Budgets;

public sealed class BudgetChangeTests
{
    private static readonly DateTimeOffset ChangedAtUtc =
        new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_AllowsANullPreviousAmountForTheFirstConfiguration()
    {
        BudgetChange change = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousAmount: null,
            newAmount: 500_000m,
            ChangedAtUtc,
            ActorId);

        Assert.Null(change.PreviousAmount);
        Assert.Equal(500_000m, change.NewAmount);
    }

    [Fact]
    public void Constructor_KeepsBothPreviousAndNewAmountsForASubsequentChange()
    {
        BudgetChange change = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousAmount: 500_000m,
            newAmount: 750_000m,
            ChangedAtUtc,
            ActorId);

        Assert.Equal(500_000m, change.PreviousAmount);
        Assert.Equal(750_000m, change.NewAmount);
        Assert.Equal(ChangedAtUtc, change.ChangedAtUtc);
        Assert.Equal(ActorId, change.ChangedByUserId);
    }

    [Fact]
    public void Constructor_RejectsANegativeNewAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            -1m,
            ChangedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsANegativePreviousAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetChange(
            Guid.NewGuid(),
            Guid.NewGuid(),
            previousAmount: -1m,
            newAmount: 500_000m,
            ChangedAtUtc,
            ActorId));
    }
}
