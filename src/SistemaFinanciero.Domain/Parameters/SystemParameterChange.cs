using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Registro inmutable de un cambio en los parámetros del sistema: conserva el valor anterior y el nuevo
/// de cada campo, quién lo hizo y cuándo. Es la bitácora de la regla de negocio 7 y 9.
/// </summary>
/// <remarks>
/// Los valores «anteriores» son nulos únicamente en el registro que documenta la primera configuración,
/// porque en ese momento no existía un valor previo.
/// </remarks>
public sealed class SystemParameterChange
{
    private SystemParameterChange()
    {
    }

    /// <summary>Crea un registro de cambio. No admite modificaciones posteriores.</summary>
    public SystemParameterChange(
        Guid id,
        decimal? previousAuthorizationLimitCrc,
        decimal newAuthorizationLimitCrc,
        int? previousOverdueAlertDays,
        int newOverdueAlertDays,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        PreviousAuthorizationLimitCrc = ValidateOptionalLimit(
            previousAuthorizationLimitCrc,
            nameof(previousAuthorizationLimitCrc));
        NewAuthorizationLimitCrc = ValidateLimit(newAuthorizationLimitCrc, nameof(newAuthorizationLimitCrc));
        PreviousOverdueAlertDays = ValidateOptionalDays(
            previousOverdueAlertDays,
            nameof(previousOverdueAlertDays));
        NewOverdueAlertDays = ValidateDays(newOverdueAlertDays, nameof(newOverdueAlertDays));
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    /// <summary>Obtiene el identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el límite anterior; nulo si este registro documenta la primera configuración.</summary>
    public decimal? PreviousAuthorizationLimitCrc { get; private set; }

    /// <summary>Obtiene el límite nuevo.</summary>
    public decimal NewAuthorizationLimitCrc { get; private set; }

    /// <summary>Obtiene los días de alerta anteriores; nulos en la primera configuración.</summary>
    public int? PreviousOverdueAlertDays { get; private set; }

    /// <summary>Obtiene los días de alerta nuevos.</summary>
    public int NewOverdueAlertDays { get; private set; }

    /// <summary>Obtiene el instante UTC del cambio.</summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario, siempre de Gerencia, que realizó el cambio.</summary>
    public Guid ChangedByUserId { get; private set; }

    private static decimal? ValidateOptionalLimit(decimal? value, string parameterName) =>
        value is null ? null : ValidateLimit(value.Value, parameterName);

    private static decimal ValidateLimit(decimal value, string parameterName) =>
        value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "El límite no puede ser negativo.");

    private static int? ValidateOptionalDays(int? value, string parameterName) =>
        value is null ? null : ValidateDays(value.Value, parameterName);

    private static int ValidateDays(int value, string parameterName) =>
        value > 0
            ? value
            : throw new ArgumentOutOfRangeException(parameterName, value, "Los días deben ser mayores que cero.");
}
