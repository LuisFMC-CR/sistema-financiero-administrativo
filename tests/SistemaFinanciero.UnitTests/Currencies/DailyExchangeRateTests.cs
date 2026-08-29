using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.UnitTests.Currencies;

public sealed class DailyExchangeRateTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesSourceAndInitializesAudit()
    {
        DateOnly effectiveDate = new(2026, 8, 22);
        ExchangeRate rate = new(510.1234567m);

        DailyExchangeRate dailyRate = new(
            Guid.NewGuid(),
            effectiveDate,
            rate,
            " Fuente administrativa ",
            " Captura manual ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(effectiveDate, dailyRate.EffectiveDate);
        Assert.Same(rate, dailyRate.Rate);
        Assert.Equal(510.123457m, dailyRate.Rate.CrcPerUsd);
        Assert.Equal("Fuente administrativa", dailyRate.Source);
        Assert.Equal("Captura manual", dailyRate.Notes);
        Assert.Equal(CreatedAtUtc, dailyRate.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, dailyRate.UpdatedAtUtc);
        Assert.Equal(CreatorId, dailyRate.CreatedByUserId);
        Assert.Equal(CreatorId, dailyRate.UpdatedByUserId);
        Assert.Empty(dailyRate.RowVersion);
    }

    [Fact]
    public void Correct_ChangesRateAndAuditButPreservesEffectiveDate()
    {
        DailyExchangeRate dailyRate = CreateDailyRate();
        DateOnly originalDate = dailyRate.EffectiveDate;
        ExchangeRate correctedRate = new(511.25m);
        DateTimeOffset correctedAtUtc = CreatedAtUtc.AddMinutes(15);

        dailyRate.Correct(
            correctedRate,
            " Fuente corregida ",
            " ",
            correctedAtUtc,
            EditorId);

        Assert.Equal(originalDate, dailyRate.EffectiveDate);
        Assert.Same(correctedRate, dailyRate.Rate);
        Assert.Equal("Fuente corregida", dailyRate.Source);
        Assert.Null(dailyRate.Notes);
        Assert.Equal(correctedAtUtc, dailyRate.UpdatedAtUtc);
        Assert.Equal(EditorId, dailyRate.UpdatedByUserId);
    }

    [Fact]
    public void Constructor_RejectsDefaultEffectiveDate()
    {
        Assert.Throws<ArgumentException>(() => new DailyExchangeRate(
            Guid.NewGuid(),
            default,
            new ExchangeRate(510m),
            "Fuente",
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsMissingRate()
    {
        Assert.Throws<ArgumentNullException>(() => new DailyExchangeRate(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 22),
            null!,
            "Fuente",
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void DomainModel_DoesNotUseLogicalDeactivationForHistoricalRates()
    {
        Assert.DoesNotContain(
            typeof(DailyExchangeRate).GetProperties(),
            property => property.Name.Equals("IsActive", StringComparison.Ordinal));
    }

    private static DailyExchangeRate CreateDailyRate()
    {
        return new DailyExchangeRate(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 22),
            new ExchangeRate(510m),
            "Fuente",
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
