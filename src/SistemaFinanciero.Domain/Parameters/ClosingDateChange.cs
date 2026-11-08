using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Registro inmutable legado de un cambio en la fecha de cierre manual. Los cierres automáticos y
/// reaperturas actuales se guardan en <see cref="AccountingPeriodChange"/>.
/// </summary>
/// <remarks>
/// Conexiones: se conserva en <c>finanzas.ParametrosFechaCierreHistorial</c> vía
/// <c>ClosingDateChangeConfiguration</c>; <c>FinancialDbContext</c> impide modificarla o borrarla.
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
