using SistemaFinanciero.Domain.Parameters;

namespace SistemaFinanciero.UnitTests.Parameters;

public sealed class SystemParameterChangeTests
{
    private static readonly DateTimeOffset ChangedAtUtc =
        new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_AllowsNullPreviousValuesForTheFirstConfiguration()
    {
        SystemParameterChange change = new(
            Guid.NewGuid(),
            previousAuthorizationLimitCrc: null,
            newAuthorizationLimitCrc: 500_000m,
            previousOverdueAlertDays: null,
            newOverdueAlertDays: 7,
            ChangedAtUtc,
            ActorId);

        Assert.Null(change.PreviousAuthorizationLimitCrc);
        Assert.Null(change.PreviousOverdueAlertDays);
        Assert.Equal(500_000m, change.NewAuthorizationLimitCrc);
        Assert.Equal(7, change.NewOverdueAlertDays);
    }

    [Fact]
    public void Constructor_KeepsBothPreviousAndNewValuesForASubsequentChange()
    {
        SystemParameterChange change = new(
            Guid.NewGuid(),
            previousAuthorizationLimitCrc: 500_000m,
            newAuthorizationLimitCrc: 750_000m,
            previousOverdueAlertDays: 7,
            newOverdueAlertDays: 10,
            ChangedAtUtc,
            ActorId);

        Assert.Equal(500_000m, change.PreviousAuthorizationLimitCrc);
        Assert.Equal(750_000m, change.NewAuthorizationLimitCrc);
        Assert.Equal(7, change.PreviousOverdueAlertDays);
        Assert.Equal(10, change.NewOverdueAlertDays);
        Assert.Equal(ChangedAtUtc, change.ChangedAtUtc);
        Assert.Equal(ActorId, change.ChangedByUserId);
    }

    [Fact]
    public void Constructor_RejectsAnEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new SystemParameterChange(
            Guid.Empty,
            null,
            500_000m,
            null,
            7,
            ChangedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RequiresAnActor()
    {
        Assert.Throws<ArgumentException>(() => new SystemParameterChange(
            Guid.NewGuid(),
            null,
            500_000m,
            null,
            7,
            ChangedAtUtc,
            Guid.Empty));
    }

    [Theory]
    [InlineData(-1)]
    public void Constructor_RejectsANegativeNewLimit(decimal newLimit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemParameterChange(
            Guid.NewGuid(),
            null,
            newLimit,
            null,
            7,
            ChangedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsANegativePreviousLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemParameterChange(
            Guid.NewGuid(),
            previousAuthorizationLimitCrc: -1m,
            newAuthorizationLimitCrc: 500_000m,
            previousOverdueAlertDays: 7,
            newOverdueAlertDays: 7,
            ChangedAtUtc,
            ActorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsNewAlertDaysThatAreNotPositive(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemParameterChange(
            Guid.NewGuid(),
            null,
            500_000m,
            null,
            days,
            ChangedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsAZeroOrNegativePreviousAlertDays()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SystemParameterChange(
            Guid.NewGuid(),
            500_000m,
            500_000m,
            previousOverdueAlertDays: 0,
            newOverdueAlertDays: 7,
            ChangedAtUtc,
            ActorId));
    }
}
