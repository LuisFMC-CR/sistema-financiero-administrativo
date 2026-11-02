using SistemaFinanciero.Application.Budgets;

namespace SistemaFinanciero.UnitTests.Budgets;

public sealed class BudgetExecutionStatusCalculatorTests
{
    [Fact]
    public void Compute_WithNoBudget_ReturnsNull()
    {
        Assert.Null(BudgetExecutionStatusCalculator.Compute(null, 500m));
    }

    [Fact]
    public void Compute_BelowEightyPercent_ReturnsNormal()
    {
        Assert.Equal(BudgetExecutionStatus.Normal, BudgetExecutionStatusCalculator.Compute(1000m, 799.99m));
    }

    [Fact]
    public void Compute_AtEightyPercent_ReturnsAttention()
    {
        Assert.Equal(BudgetExecutionStatus.Attention, BudgetExecutionStatusCalculator.Compute(1000m, 800m));
    }

    [Fact]
    public void Compute_AtOneHundredPercent_ReturnsAttention()
    {
        Assert.Equal(BudgetExecutionStatus.Attention, BudgetExecutionStatusCalculator.Compute(1000m, 1000m));
    }

    [Fact]
    public void Compute_AboveOneHundredPercent_ReturnsExceeded()
    {
        Assert.Equal(BudgetExecutionStatus.Exceeded, BudgetExecutionStatusCalculator.Compute(1000m, 1000.01m));
    }

    [Fact]
    public void Compute_WithZeroBudgetAndNoExecution_ReturnsNormal()
    {
        Assert.Equal(BudgetExecutionStatus.Normal, BudgetExecutionStatusCalculator.Compute(0m, 0m));
    }

    [Fact]
    public void Compute_WithZeroBudgetAndAnyExecution_ReturnsExceeded()
    {
        Assert.Equal(BudgetExecutionStatus.Exceeded, BudgetExecutionStatusCalculator.Compute(0m, 0.01m));
    }
}
