namespace SistemaFinanciero.Domain.Currencies;

/// <summary>
/// Monedas transaccionales admitidas por el sistema.
/// </summary>
public enum CurrencyCode
{
    /// <summary>
    /// Colón costarricense, utilizado como moneda base para presupuestos y reportes consolidados.
    /// </summary>
    CRC = 1,

    /// <summary>
    /// Dólar estadounidense.
    /// </summary>
    USD = 2,
}
