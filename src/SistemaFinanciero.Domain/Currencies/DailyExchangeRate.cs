using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Currencies;

/// <summary>
/// Registra la tasa administrativa de CRC por USD correspondiente a una fecha de negocio.
/// </summary>
/// <remarks>
/// La fecha identifica el registro y no cambia. Una corrección actualiza la tasa de forma auditada;
/// los documentos confirmados deberán conservar su propia fotografía de la tasa utilizada.
/// </remarks>
public sealed class DailyExchangeRate
{
    /// <summary>Longitud máxima del nombre de la fuente.</summary>
    public const int SourceMaxLength = 100;

    /// <summary>Longitud máxima de las notas de captura o corrección.</summary>
    public const int NotesMaxLength = 300;

    private DailyExchangeRate()
    {
        Rate = null!;
    }

    /// <summary>Crea una tasa diaria y registra su procedencia.</summary>
    public DailyExchangeRate(
        Guid id,
        DateOnly effectiveDate,
        ExchangeRate rate,
        string source,
        string? notes,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        if (effectiveDate == default)
        {
            throw new ArgumentException("La fecha efectiva es obligatoria.", nameof(effectiveDate));
        }

        Id = DomainRules.RequiredId(id, nameof(id));
        EffectiveDate = effectiveDate;
        Rate = rate ?? throw new ArgumentNullException(nameof(rate));
        Source = DomainRules.RequiredText(source, SourceMaxLength, nameof(source));
        Notes = DomainRules.OptionalText(notes, NotesMaxLength, nameof(notes));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la fecha de negocio para la que aplica la tasa.</summary>
    public DateOnly EffectiveDate { get; private set; }

    /// <summary>Obtiene la cantidad de CRC equivalente a un USD.</summary>
    public ExchangeRate Rate { get; private set; }

    /// <summary>Obtiene la fuente declarada de la tasa.</summary>
    public string Source { get; private set; } = string.Empty;

    /// <summary>Obtiene las notas opcionales de captura o corrección.</summary>
    public string? Notes { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que creó el registro.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC de la última corrección.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que realizó la última corrección.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Obtiene el token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Corrige la tasa, fuente o notas sin cambiar su fecha efectiva.</summary>
    public void Correct(
        ExchangeRate rate,
        string source,
        string? notes,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        ArgumentNullException.ThrowIfNull(rate);

        string normalizedSource = DomainRules.RequiredText(
            source,
            SourceMaxLength,
            nameof(source));
        string? normalizedNotes = DomainRules.OptionalText(notes, NotesMaxLength, nameof(notes));
        DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        DateTimeOffset normalizedUpdatedAtUtc = DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);

        Rate = rate;
        Source = normalizedSource;
        Notes = normalizedNotes;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }
}
