using SistemaFinanciero.Domain.Parameters;

namespace SistemaFinanciero.UnitTests.Parameters;

public sealed class SystemParametersTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_UsesTheFixedSingletonId()
    {
        SystemParameters parameters = CreateParameters(500_000m, 7);

        Assert.Equal(SystemParameters.SingletonId, parameters.Id);
    }

    [Fact]
    public void Constructor_RoundsTheLimitToTwoDecimals()
    {
        SystemParameters parameters = CreateParameters(500_000.125m, 7);

        Assert.Equal(500_000.13m, parameters.AuthorizationLimitCrc);
    }

    [Fact]
    public void Constructor_AllowsAZeroLimit()
    {
        SystemParameters parameters = CreateParameters(0m, 7);

        Assert.Equal(0m, parameters.AuthorizationLimitCrc);
    }

    [Fact]
    public void Constructor_RejectsANegativeLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateParameters(-1m, 7));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-7)]
    public void Constructor_RejectsAlertDaysThatAreNotPositive(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateParameters(500_000m, days));
    }

    [Fact]
    public void UpdateValues_ChangesBothFieldsAndTheAudit()
    {
        SystemParameters parameters = CreateParameters(500_000m, 7);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        parameters.UpdateValues(750_000m, 10, changedAtUtc, EditorId);

        Assert.Equal(750_000m, parameters.AuthorizationLimitCrc);
        Assert.Equal(10, parameters.OverdueAlertDays);
        Assert.Equal(changedAtUtc, parameters.UpdatedAtUtc);
        Assert.Equal(EditorId, parameters.UpdatedByUserId);
        Assert.Equal(CreatedAtUtc, parameters.CreatedAtUtc);
        Assert.Equal(CreatorId, parameters.CreatedByUserId);
    }

    [Fact]
    public void UpdateValues_LeavesTheParametersUntouchedWhenTheNewLimitIsInvalid()
    {
        SystemParameters parameters = CreateParameters(500_000m, 7);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parameters.UpdateValues(-1m, 10, CreatedAtUtc.AddMinutes(5), EditorId));

        Assert.Equal(500_000m, parameters.AuthorizationLimitCrc);
        Assert.Equal(7, parameters.OverdueAlertDays);
        Assert.Equal(CreatedAtUtc, parameters.UpdatedAtUtc);
        Assert.Equal(CreatorId, parameters.UpdatedByUserId);
    }

    [Fact]
    public void UpdateValues_RejectsAnEmptyUserOrAnInstantBeforeTheLastChange()
    {
        SystemParameters parameters = CreateParameters(500_000m, 7);

        Assert.Throws<ArgumentException>(() =>
            parameters.UpdateValues(600_000m, 7, CreatedAtUtc.AddMinutes(1), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            parameters.UpdateValues(600_000m, 7, CreatedAtUtc.AddMinutes(-1), EditorId));

        Assert.Equal(500_000m, parameters.AuthorizationLimitCrc);
    }

    private static SystemParameters CreateParameters(decimal authorizationLimitCrc, int overdueAlertDays) =>
        new(authorizationLimitCrc, overdueAlertDays, CreatedAtUtc, CreatorId);
}
