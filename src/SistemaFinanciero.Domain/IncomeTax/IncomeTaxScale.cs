using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.IncomeTax;

/// <summary>
/// Escala del impuesto sobre la renta para personas jurídicas (RF-99): tres límites de tramo con cuatro
/// porcentajes progresivos para pequeñas empresas, y el umbral de renta bruta a partir del cual se
/// tributa con la tarifa general sobre toda la utilidad.
/// </summary>
/// <remarks>
/// Es un valor inmutable: para cambiar la escala se crea una nueva y se entrega a
/// <c>SystemParameters.UpdateIncomeTaxScale</c>, que la guarda en las columnas de
/// <c>finanzas.ParametrosSistema</c>. <see cref="Default"/> contiene los valores de 2026. Hay una sola
/// escala vigente: si Gerencia la actualiza, toda estimación posterior usa los valores nuevos, incluso
/// la de un período anterior.
///
/// Conexiones: la usa <see cref="IncomeTaxEstimate.Calculate"/>; la edita Gerencia desde la pantalla
/// de Parámetros.
/// </remarks>
public sealed class IncomeTaxScale
{
    /// <summary>Porcentaje máximo admitido en cualquier tramo.</summary>
    public const decimal MaxRate = 100m;

    /// <summary>Decimales de los porcentajes (columnas <c>decimal(5,2)</c>).</summary>
    private const int RateDecimalPlaces = 2;

    /// <summary>Crea una escala validada.</summary>
    public IncomeTaxScale(
        decimal bracket1UpperLimit,
        decimal bracket2UpperLimit,
        decimal bracket3UpperLimit,
        decimal bracket1Rate,
        decimal bracket2Rate,
        decimal bracket3Rate,
        decimal bracket4Rate,
        decimal grossIncomeThreshold,
        decimal generalRate)
    {
        Bracket1UpperLimit = RequirePositiveAmount(bracket1UpperLimit, nameof(bracket1UpperLimit));
        Bracket2UpperLimit = RequirePositiveAmount(bracket2UpperLimit, nameof(bracket2UpperLimit));
        Bracket3UpperLimit = RequirePositiveAmount(bracket3UpperLimit, nameof(bracket3UpperLimit));

        if (Bracket2UpperLimit <= Bracket1UpperLimit)
        {
            throw new ArgumentException(
                "El límite del segundo tramo debe ser mayor que el del primero.",
                nameof(bracket2UpperLimit));
        }

        if (Bracket3UpperLimit <= Bracket2UpperLimit)
        {
            throw new ArgumentException(
                "El límite del tercer tramo debe ser mayor que el del segundo.",
                nameof(bracket3UpperLimit));
        }

        Bracket1Rate = RequireRate(bracket1Rate, nameof(bracket1Rate));
        Bracket2Rate = RequireRate(bracket2Rate, nameof(bracket2Rate));
        Bracket3Rate = RequireRate(bracket3Rate, nameof(bracket3Rate));
        Bracket4Rate = RequireRate(bracket4Rate, nameof(bracket4Rate));
        GrossIncomeThreshold = RequirePositiveAmount(grossIncomeThreshold, nameof(grossIncomeThreshold));
        GeneralRate = RequireRate(generalRate, nameof(generalRate));
    }

    /// <summary>Escala vigente para el período 2026, la que cargan las migraciones.</summary>
    public static IncomeTaxScale Default { get; } = new(
        bracket1UpperLimit: 5_621_000m,
        bracket2UpperLimit: 8_433_000m,
        bracket3UpperLimit: 11_243_000m,
        bracket1Rate: 5m,
        bracket2Rate: 10m,
        bracket3Rate: 15m,
        bracket4Rate: 20m,
        grossIncomeThreshold: 119_174_000m,
        generalRate: 30m);

    /// <summary>Renta neta hasta la que se aplica el primer porcentaje.</summary>
    public decimal Bracket1UpperLimit { get; }

    /// <summary>Renta neta hasta la que se aplica el segundo porcentaje, sobre el exceso del primer límite.</summary>
    public decimal Bracket2UpperLimit { get; }

    /// <summary>Renta neta hasta la que se aplica el tercer porcentaje, sobre el exceso del segundo límite.</summary>
    public decimal Bracket3UpperLimit { get; }

    /// <summary>Porcentaje del primer tramo.</summary>
    public decimal Bracket1Rate { get; }

    /// <summary>Porcentaje del segundo tramo.</summary>
    public decimal Bracket2Rate { get; }

    /// <summary>Porcentaje del tercer tramo.</summary>
    public decimal Bracket3Rate { get; }

    /// <summary>Porcentaje sobre el exceso del tercer límite, sin tope.</summary>
    public decimal Bracket4Rate { get; }

    /// <summary>
    /// Renta bruta anual por encima de la cual no aplican los tramos reducidos y se usa
    /// <see cref="GeneralRate"/> sobre toda la renta neta.
    /// </summary>
    public decimal GrossIncomeThreshold { get; }

    /// <summary>Tarifa general del impuesto sobre las utilidades.</summary>
    public decimal GeneralRate { get; }

    private static decimal RequirePositiveAmount(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        return rounded > 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto debe ser mayor que cero.");
    }

    private static decimal RequireRate(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.Round(value, RateDecimalPlaces);

        return rounded is >= 0 and <= MaxRate
            ? rounded
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"El porcentaje debe estar entre 0 y {MaxRate}.");
    }
}
