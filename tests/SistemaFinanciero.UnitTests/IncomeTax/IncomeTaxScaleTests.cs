using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Domain.Parameters;

namespace SistemaFinanciero.UnitTests.IncomeTax;

/// <summary>RF-99: validación de la escala del impuesto sobre la renta y su lugar en los parámetros.</summary>
public sealed class IncomeTaxScaleTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Default_HasThe2026Values()
    {
        IncomeTaxScale scale = IncomeTaxScale.Default;

        Assert.Equal(5_621_000m, scale.Bracket1UpperLimit);
        Assert.Equal(8_433_000m, scale.Bracket2UpperLimit);
        Assert.Equal(11_243_000m, scale.Bracket3UpperLimit);
        Assert.Equal([5m, 10m, 15m, 20m], new[] { scale.Bracket1Rate, scale.Bracket2Rate, scale.Bracket3Rate, scale.Bracket4Rate });
        Assert.Equal(119_174_000m, scale.GrossIncomeThreshold);
        Assert.Equal(30m, scale.GeneralRate);
    }

    [Theory]
    [InlineData(8_433_000, 8_433_000, 11_243_000)]
    [InlineData(5_621_000, 11_243_000, 8_433_000)]
    public void Constructor_RejectsLimitsThatDoNotIncrease(decimal limit1, decimal limit2, decimal limit3)
    {
        Assert.Throws<ArgumentException>(() => CreateScale(limit1: limit1, limit2: limit2, limit3: limit3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Constructor_RejectsARateOutsideZeroToOneHundred(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateScale(generalRate: rate));
    }

    [Fact]
    public void Constructor_RejectsAZeroGrossIncomeThreshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateScale(threshold: 0m));
    }

    [Fact]
    public void SystemParameters_StartWithTheDefaultScale()
    {
        SystemParameters parameters = new(500_000m, 7, CreatedAtUtc, CreatorId);

        IncomeTaxScale scale = parameters.GetIncomeTaxScale();

        Assert.Equal(IncomeTaxScale.Default.Bracket3UpperLimit, scale.Bracket3UpperLimit);
        Assert.Equal(IncomeTaxScale.Default.GeneralRate, scale.GeneralRate);
    }

    [Fact]
    public void SystemParameters_UpdateIncomeTaxScale_ReplacesTheValuesAndTheAudit()
    {
        SystemParameters parameters = new(500_000m, 7, CreatedAtUtc, CreatorId);
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        parameters.UpdateIncomeTaxScale(CreateScale(limit1: 6_000_000m), updatedAtUtc, EditorId);

        Assert.Equal(6_000_000m, parameters.IncomeTaxBracket1UpperLimit);
        Assert.Equal(updatedAtUtc, parameters.UpdatedAtUtc);
        Assert.Equal(EditorId, parameters.UpdatedByUserId);
    }

    private static IncomeTaxScale CreateScale(
        decimal limit1 = 5_621_000m,
        decimal limit2 = 8_433_000m,
        decimal limit3 = 11_243_000m,
        decimal threshold = 119_174_000m,
        decimal generalRate = 30m) =>
        new(limit1, limit2, limit3, 5m, 10m, 15m, 20m, threshold, generalRate);
}
