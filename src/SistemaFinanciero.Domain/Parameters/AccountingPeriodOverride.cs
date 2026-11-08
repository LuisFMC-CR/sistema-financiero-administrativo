using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>Excepción que mantiene abierto un mes ya cerrado automáticamente.</summary>
public sealed class AccountingPeriodOverride
{
    private AccountingPeriodOverride()
    {
    }

    public AccountingPeriodOverride(
        int year,
        int month,
        string reason,
        DateTimeOffset changedAtUtc,
        Guid changedByUserId)
    {
        ValidatePeriod(year, month);
        Year = year;
        Month = month;
        IsOpen = true;
        Reason = DomainRules.RequiredText(reason, SystemParameters.ClosingDateChangeReasonMaxLength, nameof(reason));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(changedAtUtc, nameof(changedAtUtc));
        CreatedByUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
    }

    public int Year { get; private set; }

    public int Month { get; private set; }

    public bool IsOpen { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public void Reopen(string reason, DateTimeOffset changedAtUtc, Guid changedByUserId)
    {
        if (IsOpen)
        {
            throw new InvalidOperationException("El período ya está abierto.");
        }

        string normalizedReason = DomainRules.RequiredText(
            reason,
            SystemParameters.ClosingDateChangeReasonMaxLength,
            nameof(reason));
        Guid normalizedUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
        DateTimeOffset normalizedChangedAtUtc = DomainRules.RequiredUtcTimestamp(
            changedAtUtc,
            nameof(changedAtUtc),
            UpdatedAtUtc);

        IsOpen = true;
        Reason = normalizedReason;
        UpdatedAtUtc = normalizedChangedAtUtc;
        UpdatedByUserId = normalizedUserId;
    }

    public void Close(DateTimeOffset changedAtUtc, Guid changedByUserId)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("El período ya está cerrado.");
        }

        Guid normalizedUserId = DomainRules.RequiredId(changedByUserId, nameof(changedByUserId));
        DateTimeOffset normalizedChangedAtUtc = DomainRules.RequiredUtcTimestamp(
            changedAtUtc,
            nameof(changedAtUtc),
            UpdatedAtUtc);

        IsOpen = false;
        Reason = null;
        UpdatedAtUtc = normalizedChangedAtUtc;
        UpdatedByUserId = normalizedUserId;
    }

    public static void ValidatePeriod(int year, int month)
    {
        if (year < 1 || year > 9998)
        {
            throw new ArgumentOutOfRangeException(nameof(year), year, "El año del período no es válido.");
        }

        if (month < 1 || month > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), month, "El mes debe estar entre 1 y 12.");
        }
    }
}
