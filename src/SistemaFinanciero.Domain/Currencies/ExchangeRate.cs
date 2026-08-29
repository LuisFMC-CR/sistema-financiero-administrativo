namespace SistemaFinanciero.Domain.Currencies;

/// <summary>
/// Representa la cantidad de colones costarricenses equivalente a un dólar estadounidense.
/// </summary>
/// <remarks>
/// La tasa se conserva como una fotografía histórica en cada documento confirmado. Los resultados
/// monetarios se redondean a dos decimales con mitades alejándose de cero.
/// </remarks>
public sealed record ExchangeRate
{
    /// <summary>
    /// Cantidad de decimales conservados para una tasa de cambio.
    /// </summary>
    public const int RateDecimalPlaces = 6;

    /// <summary>
    /// Cantidad de decimales utilizada para importes monetarios finales.
    /// </summary>
    public const int MoneyDecimalPlaces = 2;

    /// <summary>
    /// Inicializa una tasa de cambio válida.
    /// </summary>
    /// <param name="crcPerUsd">Cantidad positiva de CRC por cada USD.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se produce cuando <paramref name="crcPerUsd"/> no es mayor que cero.
    /// </exception>
    public ExchangeRate(decimal crcPerUsd)
    {
        if (crcPerUsd <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(crcPerUsd),
                crcPerUsd,
                "El tipo de cambio debe ser mayor que cero.");
        }

        CrcPerUsd = decimal.Round(
            crcPerUsd,
            RateDecimalPlaces,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Obtiene la cantidad de CRC correspondiente a un USD.
    /// </summary>
    public decimal CrcPerUsd { get; }

    /// <summary>
    /// Convierte un importe en CRC o USD a la moneda base CRC.
    /// </summary>
    /// <param name="amount">Importe en la moneda indicada.</param>
    /// <param name="currency">Moneda original del importe.</param>
    /// <returns>Importe equivalente en CRC, redondeado a dos decimales.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se produce cuando la moneda no forma parte de las monedas admitidas.
    /// </exception>
    public decimal ConvertToCrc(decimal amount, CurrencyCode currency)
    {
        decimal result = currency switch
        {
            CurrencyCode.CRC => amount,
            CurrencyCode.USD => amount * CrcPerUsd,
            _ => throw UnsupportedCurrency(currency),
        };

        return RoundMoney(result);
    }

    /// <summary>
    /// Convierte un importe expresado en CRC a la moneda solicitada.
    /// </summary>
    /// <param name="amountInCrc">Importe en la moneda base CRC.</param>
    /// <param name="targetCurrency">Moneda de destino.</param>
    /// <returns>Importe equivalente en la moneda de destino, redondeado a dos decimales.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Se produce cuando la moneda no forma parte de las monedas admitidas.
    /// </exception>
    public decimal ConvertFromCrc(decimal amountInCrc, CurrencyCode targetCurrency)
    {
        decimal result = targetCurrency switch
        {
            CurrencyCode.CRC => amountInCrc,
            CurrencyCode.USD => amountInCrc / CrcPerUsd,
            _ => throw UnsupportedCurrency(targetCurrency),
        };

        return RoundMoney(result);
    }

    private static decimal RoundMoney(decimal amount)
    {
        return decimal.Round(amount, MoneyDecimalPlaces, MidpointRounding.AwayFromZero);
    }

    private static ArgumentOutOfRangeException UnsupportedCurrency(CurrencyCode currency)
    {
        return new ArgumentOutOfRangeException(
            nameof(currency),
            currency,
            "La moneda indicada no está admitida por el sistema.");
    }
}
