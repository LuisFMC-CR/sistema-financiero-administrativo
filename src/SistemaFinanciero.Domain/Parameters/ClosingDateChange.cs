using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Registro inmutable de un cambio en la fecha de cierre: conserva la fecha anterior y la nueva, el
/// motivo (obligatorio solo al retroceder, es decir, al reabrir) y quién lo hizo y cuándo (regla de
/// negocio 16). Es la bitácora de ese cambio, separada de <see cref="SystemParameterChange"/> porque
/// es una operación propia (<c>SystemSettings.ChangeClosingDate</c>, distinta de
/// <c>UpdateLimits</c>).
/// </summary>
/// <remarks>
/// Conexiones: la crea <c>SystemParametersService.ChangeClosingDateAsync</c> (Infrastructure); se
/// guarda en <c>finanzas.ParametrosFechaCierreHistorial</c> vía <c>ClosingDateChangeConfiguration</c>.
/// <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c> impide modificarla o borrarla. No tiene
/// pantalla propia de consulta todavía.
/// </remarks>
public sealed class ClosingDateChange
{
    private ClosingDateChange()
    {
    }

    /// <summary>Crea un registro de cambio. No admite modificaciones posteriores.</summary>
    public ClosingDateChange(
        Guid id,
        DateOnly previousClosingDate,
        DateOnly newClosingDate,
        string? reason,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        PreviousClosingDate = previousClosingDate;
        NewClosingDate = newClosingDate;
        Reason = DomainRules.OptionalText(reason, SystemParameters.ClosingDateChangeReasonMaxLength, nameof(reason));
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    /// <summary>Obtiene el identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la fecha de cierre anterior.</summary>
    public DateOnly PreviousClosingDate { get; private set; }

    /// <summary>Obtiene la fecha de cierre nueva.</summary>
    public DateOnly NewClosingDate { get; private set; }

    /// <summary>Obtiene el motivo; presente siempre que el cambio fue una reapertura.</summary>
    public string? Reason { get; private set; }

    /// <summary>Obtiene el instante UTC del cambio.</summary>
    public DateTimeOffset ChangedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario de Gerencia que realizó el cambio.</summary>
    public Guid ChangedByUserId { get; private set; }
}
