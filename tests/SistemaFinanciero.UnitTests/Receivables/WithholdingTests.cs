using SistemaFinanciero.Domain.Receivables;

namespace SistemaFinanciero.UnitTests.Receivables;

public sealed class WithholdingTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_CalculatesTheAmountFromTheBaseAndTheRate()
    {
        Withholding withholding = CreateWithholding(rate: 2m, baseAmount: 1000m);

        Assert.Equal(2m, withholding.Rate);
        Assert.Equal(1000m, withholding.Base);
        Assert.Equal(20m, withholding.Amount);
    }

    [Fact]
    public void Constructor_RoundsTheAmountAwayFromZero()
    {
        Withholding withholding = CreateWithholding(rate: 2m, baseAmount: 125.555m);

        // 125.555 * 2 / 100 = 2.5111 -> 2.51 (ya redondeado a 2 decimales por RoundMoney).
        Assert.Equal(2.51m, withholding.Amount);
    }

    [Fact]
    public void Constructor_KeepsTheCatalogSnapshot()
    {
        Guid withholdingTypeId = Guid.NewGuid();

        Withholding withholding = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            withholdingTypeId,
            "ret-02",
            "Renta",
            2m,
            1000m,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(withholdingTypeId, withholding.WithholdingTypeId);
        Assert.Equal("RET-02", withholding.Code);
        Assert.Equal("Renta", withholding.Name);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    public void Constructor_RejectsARateOutsideTheValidRange(decimal rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithholding(rate: rate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsABaseThatIsNotPositive(decimal baseAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithholding(baseAmount: baseAmount));
    }

    private static Withholding CreateWithholding(decimal rate = 2m, decimal baseAmount = 1000m) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "RET-02",
        "Renta",
        rate,
        baseAmount,
        CreatedAtUtc,
        CreatorId);
}
