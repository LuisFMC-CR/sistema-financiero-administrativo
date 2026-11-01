using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Tipo de identificación de un <see cref="Customer"/> o <see cref="Supplier"/> (bloque 3 del paquete
/// de mejoras finales), que determina cuántos dígitos exige el número.
/// </summary>
public enum IdentificationType
{
    /// <summary>Física, 9 dígitos.</summary>
    Individual = 1,

    /// <summary>Jurídica, 10 dígitos.</summary>
    LegalEntity = 2,

    /// <summary>DIMEX, 11 o 12 dígitos.</summary>
    Dimex = 3,

    /// <summary>NITE, 10 dígitos.</summary>
    Nite = 4,

    /// <summary>Extranjero no domiciliado, texto libre.</summary>
    ForeignNonResident = 5,
}

/// <summary>
/// Equivalencia de cada <see cref="IdentificationType"/> con el código oficial de tipo de
/// identificación de Hacienda (bloque 3 del paquete de mejoras finales); coincide numéricamente con
/// el valor del enum, así que solo se formatea a 2 dígitos. Para la exportación de datos de la
/// factura (RF-88).
/// </summary>
public static class IdentificationTypeHaciendaExtensions
{
    public static string ToHaciendaCode(this IdentificationType identificationType) =>
        ((int)DomainRules.DefinedEnum(identificationType, nameof(identificationType)))
            .ToString("D2", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// Valida el número de identificación según su <see cref="IdentificationType"/>; lo comparten
/// <see cref="Customer"/> y <see cref="Supplier"/> para no duplicar la misma regla.
/// </summary>
internal static class IdentificationTypeRules
{
    public static string Validate(
        IdentificationType type,
        string? identification,
        int maxLength,
        string parameterName)
    {
        DomainRules.DefinedEnum(type, nameof(type));

        return type switch
        {
            IdentificationType.Individual => DomainRules.RequiredDigits(identification, 9, parameterName),
            IdentificationType.LegalEntity => DomainRules.RequiredDigits(identification, 10, parameterName),
            IdentificationType.Dimex => DomainRules.RequiredDigitsInRange(identification, 11, 12, parameterName),
            IdentificationType.Nite => DomainRules.RequiredDigits(identification, 10, parameterName),
            IdentificationType.ForeignNonResident => DomainRules.RequiredText(identification, maxLength, parameterName),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de identificación desconocido."),
        };
    }
}
