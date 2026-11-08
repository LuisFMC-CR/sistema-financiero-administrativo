using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>Registro inmutable de una configuración o cambio del período fiscal anual.</summary>
public sealed class FiscalPeriodChange
{
    private FiscalPeriodChange()
    {
    }

    public FiscalPeriodChange(
        DateOnly? previousStartDate,
        DateOnly? previousEndDate,
        DateOnly newStartDate,
        DateOnly newEndDate,
        string? reason,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        if (newStartDate.Year >= DateOnly.MaxValue.Year
            || newStartDate.Day != 1
            || newEndDate != newStartDate.AddYears(1).AddDays(-1))
        {
            throw new ArgumentException(
                "El período fiscal debe iniciar el primer día de un mes y abarcar exactamente doce meses.",
                nameof(newEndDate));
        }

        PreviousStartDate = previousStartDate;
        PreviousEndDate = previousEndDate;
        NewStartDate = newStartDate;
        NewEndDate = newEndDate;
        Reason = DomainRules.OptionalText(reason, SystemParameters.ClosingDateChangeReasonMaxLength, nameof(reason));
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public DateOnly? PreviousStartDate { get; private set; }

    public DateOnly? PreviousEndDate { get; private set; }

    public DateOnly NewStartDate { get; private set; }

    public DateOnly NewEndDate { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public Guid ChangedByUserId { get; private set; }
}
