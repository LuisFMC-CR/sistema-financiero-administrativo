namespace SistemaFinanciero.Domain.Common;

/// <summary>
/// Centraliza validaciones pequeñas compartidas por las entidades sin introducir una jerarquía de persistencia.
/// </summary>
internal static class DomainRules
{
    /// <summary>Decimales de los importes, impuestos, totales, saldos y pagos.</summary>
    public const int MoneyDecimalPlaces = 2;

    /// <summary>Decimales de cantidades, precios unitarios y tarifas de impuesto.</summary>
    public const int QuantityDecimalPlaces = 4;

    /// <summary>Redondea un importe monetario a dos decimales, alejando las mitades de cero.</summary>
    public static decimal RoundMoney(decimal value) => Round(value, MoneyDecimalPlaces);

    /// <summary>Redondea a la cantidad de decimales indicada, alejando las mitades de cero.</summary>
    public static decimal Round(decimal value, int decimalPlaces) =>
        decimal.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);

    public static Guid RequiredId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("El identificador es obligatorio.", parameterName);
        }

        return value;
    }

    public static Guid? OptionalId(Guid? value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("El identificador opcional no puede ser vacío.", parameterName);
        }

        return value;
    }

    public static string RequiredText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El texto es obligatorio.", parameterName);
        }

        string normalized = value.Trim();
        EnsureMaximumLength(normalized, maximumLength, parameterName);
        return normalized;
    }

    public static string? OptionalText(string? value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized = value.Trim();
        EnsureMaximumLength(normalized, maximumLength, parameterName);
        return normalized;
    }

    public static string NormalizedCode(string? value, int maximumLength, string parameterName)
    {
        return RequiredText(value, maximumLength, parameterName).ToUpperInvariant();
    }

    public static decimal? OptionalPositiveDecimal(
        decimal? value,
        int decimalPlaces,
        string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        decimal roundedValue = decimal.Round(
            value.Value,
            decimalPlaces,
            MidpointRounding.AwayFromZero);

        if (roundedValue <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "El precio indicado debe ser mayor que cero.");
        }

        return roundedValue;
    }

    public static T DefinedEnum<T>(T value, string parameterName)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "El valor indicado no forma parte de las opciones admitidas.");
        }

        return value;
    }

    public static DateTimeOffset RequiredUtcTimestamp(
        DateTimeOffset value,
        string parameterName,
        DateTimeOffset? notBefore = null)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante debe estar expresado en UTC.", parameterName);
        }

        if (notBefore is not null && value < notBefore.Value)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "El instante de actualización no puede ser anterior al último cambio registrado.");
        }

        return value;
    }

    private static void EnsureMaximumLength(
        string value,
        int maximumLength,
        string parameterName)
    {
        if (value.Length > maximumLength)
        {
            throw new ArgumentException(
                $"El texto no puede exceder {maximumLength} caracteres.",
                parameterName);
        }
    }
}
