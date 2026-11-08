using SistemaFinanciero.Application.Reports;

namespace SistemaFinanciero.UnitTests.Reports;

public sealed class AgingAmountsTests
{
    [Theory]
    [InlineData(-5, 100)] // Todavía no vence: al día.
    [InlineData(0, 100)] // Vence hoy: al día.
    public void Add_WithNonPositiveDaysPastDue_AddsToCurrent(int daysPastDue, decimal balance)
    {
        AgingAmounts aging = AgingAmounts.Zero.Add(daysPastDue, balance);

        Assert.Equal(balance, aging.Current);
        Assert.Equal(0m, aging.Overdue1To30);
        Assert.Equal(0m, aging.Overdue31To60);
        Assert.Equal(0m, aging.Overdue61To90);
        Assert.Equal(0m, aging.OverdueOver90);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(30)]
    public void Add_WithOneToThirtyDaysPastDue_AddsToFirstBucket(int daysPastDue)
    {
        AgingAmounts aging = AgingAmounts.Zero.Add(daysPastDue, 100m);

        Assert.Equal(0m, aging.Current);
        Assert.Equal(100m, aging.Overdue1To30);
        Assert.Equal(0m, aging.Overdue31To60);
        Assert.Equal(0m, aging.Overdue61To90);
        Assert.Equal(0m, aging.OverdueOver90);
    }

    [Theory]
    [InlineData(31)]
    [InlineData(60)]
    public void Add_WithThirtyOneToSixtyDaysPastDue_AddsToSecondBucket(int daysPastDue)
    {
        AgingAmounts aging = AgingAmounts.Zero.Add(daysPastDue, 100m);

        Assert.Equal(0m, aging.Overdue1To30);
        Assert.Equal(100m, aging.Overdue31To60);
        Assert.Equal(0m, aging.Overdue61To90);
        Assert.Equal(0m, aging.OverdueOver90);
    }

    [Theory]
    [InlineData(61)]
    [InlineData(90)]
    public void Add_WithSixtyOneToNinetyDaysPastDue_AddsToThirdBucket(int daysPastDue)
    {
        AgingAmounts aging = AgingAmounts.Zero.Add(daysPastDue, 100m);

        Assert.Equal(0m, aging.Overdue31To60);
        Assert.Equal(100m, aging.Overdue61To90);
        Assert.Equal(0m, aging.OverdueOver90);
    }

    [Theory]
    [InlineData(91)]
    [InlineData(1000)]
    public void Add_WithMoreThanNinetyDaysPastDue_AddsToLastBucket(int daysPastDue)
    {
        AgingAmounts aging = AgingAmounts.Zero.Add(daysPastDue, 100m);

        Assert.Equal(0m, aging.Overdue61To90);
        Assert.Equal(100m, aging.OverdueOver90);
    }

    [Fact]
    public void Add_AccumulatesAcrossMultipleDocumentsInTheSameBucket()
    {
        AgingAmounts aging = AgingAmounts.Zero.Add(10, 100m).Add(20, 50m);

        Assert.Equal(150m, aging.Overdue1To30);
    }

    [Fact]
    public void AdditionOperator_SumsEachBucketIndependently()
    {
        AgingAmounts first = new(10m, 20m, 30m, 40m, 50m);
        AgingAmounts second = new(1m, 2m, 3m, 4m, 5m);

        AgingAmounts total = first + second;

        Assert.Equal(11m, total.Current);
        Assert.Equal(22m, total.Overdue1To30);
        Assert.Equal(33m, total.Overdue31To60);
        Assert.Equal(44m, total.Overdue61To90);
        Assert.Equal(55m, total.OverdueOver90);
    }
}
