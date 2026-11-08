using SistemaFinanciero.Domain.Parameters;

namespace SistemaFinanciero.UnitTests.Parameters;

public sealed class AccountingPeriodOverrideTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ReopenAndClose_KeepThePeriodStateAndUpdatedAudit()
    {
        AccountingPeriodOverride period = new(2026, 8, "Corregir un documento", CreatedAtUtc, ActorId);

        period.Close(CreatedAtUtc.AddMinutes(1), ActorId);

        Assert.False(period.IsOpen);
        Assert.Null(period.Reason);

        period.Reopen("Segunda revisión", CreatedAtUtc.AddMinutes(2), ActorId);

        Assert.True(period.IsOpen);
        Assert.Equal("Segunda revisión", period.Reason);
        Assert.Equal(CreatedAtUtc.AddMinutes(2), period.UpdatedAtUtc);
        Assert.Equal(ActorId, period.UpdatedByUserId);
    }

    [Fact]
    public void Constructor_RejectsInvalidMonth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AccountingPeriodOverride(2026, 13, "Revisión", CreatedAtUtc, ActorId));
    }

    [Fact]
    public void AccountingPeriodChange_RequiresDifferentStatesAndReasonForReopening()
    {
        Assert.Throws<ArgumentException>(() =>
            new AccountingPeriodChange(2026, 8, true, true, null, CreatedAtUtc, ActorId));
        Assert.Throws<ArgumentException>(() =>
            new AccountingPeriodChange(2026, 8, false, true, null, CreatedAtUtc, ActorId));
    }
}
