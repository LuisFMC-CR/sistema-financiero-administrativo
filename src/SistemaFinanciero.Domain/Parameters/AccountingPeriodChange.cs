using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>Bitácora inmutable de reaperturas y cierres explícitos de meses reabiertos.</summary>
public sealed class AccountingPeriodChange
{
    private AccountingPeriodChange()
    {
    }

    public AccountingPeriodChange(
        int year,
        int month,
        bool wasOpen,
        bool isOpen,
        string? reason,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        AccountingPeriodOverride.ValidatePeriod(year, month);
        if (wasOpen == isOpen)
        {
            throw new ArgumentException("El estado del período debe cambiar.", nameof(isOpen));
        }

        if (isOpen)
        {
            DomainRules.RequiredText(reason, SystemParameters.ClosingDateChangeReasonMaxLength, nameof(reason));
        }

        Year = year;
        Month = month;
        WasOpen = wasOpen;
        IsOpen = isOpen;
        Reason = DomainRules.OptionalText(reason, SystemParameters.ClosingDateChangeReasonMaxLength, nameof(reason));
        ChangedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        ChangedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
    }

    public Guid Id { get; private set; } = Guid.NewGuid();

    public int Year { get; private set; }

    public int Month { get; private set; }

    public bool WasOpen { get; private set; }

    public bool IsOpen { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public Guid ChangedByUserId { get; private set; }
}
