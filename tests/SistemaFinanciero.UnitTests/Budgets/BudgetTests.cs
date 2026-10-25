using SistemaFinanciero.Domain.Budgets;

namespace SistemaFinanciero.UnitTests.Budgets;

public sealed class BudgetTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_WithZeroAmount_IsValid()
    {
        Budget budget = CreateBudget(amount: 0m);

        Assert.Equal(0m, budget.Amount);
        Assert.Equal(2026, budget.PeriodYear);
        Assert.Equal(10, budget.PeriodMonth);
    }

    [Fact]
    public void Constructor_RejectsANegativeAmount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateBudget(amount: -1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Constructor_RejectsAMonthOutOfRange(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Budget(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2026,
            month,
            1000m,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void ChangeAmount_UpdatesTheAmountAndTheAudit()
    {
        Budget budget = CreateBudget(amount: 1000m);
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        budget.ChangeAmount(1500m, updatedAtUtc, CreatorId);

        Assert.Equal(1500m, budget.Amount);
        Assert.Equal(updatedAtUtc, budget.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeAmount_AllowsChangingBackToZero()
    {
        Budget budget = CreateBudget(amount: 1000m);

        budget.ChangeAmount(0m, CreatedAtUtc.AddDays(1), CreatorId);

        Assert.Equal(0m, budget.Amount);
    }

    [Fact]
    public void ChangeAmount_RejectsANegativeAmount()
    {
        Budget budget = CreateBudget(amount: 1000m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            budget.ChangeAmount(-1m, CreatedAtUtc.AddDays(1), CreatorId));

        Assert.Equal(1000m, budget.Amount);
    }

    private static Budget CreateBudget(decimal amount) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        2026,
        10,
        amount,
        CreatedAtUtc,
        CreatorId);
}
