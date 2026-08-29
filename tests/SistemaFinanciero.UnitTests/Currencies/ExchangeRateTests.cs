using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.UnitTests.Currencies;

public sealed class ExchangeRateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenRateIsNotPositive_Throws(decimal invalidRate)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ExchangeRate(invalidRate));

        Assert.Equal("crcPerUsd", exception.ParamName);
    }

    [Fact]
    public void ConvertToCrc_WhenCurrencyIsUsd_UsesStoredRate()
    {
        ExchangeRate rate = new(512.3456789m);

        decimal result = rate.ConvertToCrc(10.25m, CurrencyCode.USD);

        Assert.Equal(5251.54m, result);
        Assert.Equal(512.345679m, rate.CrcPerUsd);
    }

    [Fact]
    public void ConvertToCrc_WhenCurrencyIsCrc_DoesNotApplyExchangeRate()
    {
        ExchangeRate rate = new(512.345678m);

        decimal result = rate.ConvertToCrc(1234.567m, CurrencyCode.CRC);

        Assert.Equal(1234.57m, result);
    }

    [Fact]
    public void ConvertFromCrc_WhenTargetCurrencyIsUsd_UsesStoredRate()
    {
        ExchangeRate rate = new(500m);

        decimal result = rate.ConvertFromCrc(1250m, CurrencyCode.USD);

        Assert.Equal(2.50m, result);
    }
}
