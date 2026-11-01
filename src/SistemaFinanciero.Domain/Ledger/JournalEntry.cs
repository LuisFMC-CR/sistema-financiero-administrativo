using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Ledger;

/// <summary>
/// Asiento contable automático de partida doble (RF-78), generado en la misma transacción que la
/// operación que lo origina, con la fecha de negocio de esa operación.
/// </summary>
/// <remarks>
/// Inmutable: no se edita ni se elimina (bloque 5 del paquete de mejoras finales). Anular su
/// documento de origen genera un asiento de reversión nuevo (<see cref="ReversesEntryId"/> apunta al
/// original), nunca modifica este. Exige al menos dos líneas con importe y que la suma de débitos sea
/// igual a la de créditos; las líneas en cero (por ejemplo, el IVA de una tarifa exenta) se omiten
/// antes de validar.
///
/// Conexiones: la crea <c>JournalPostingService</c> (Infrastructure/Ledger) a partir de las reglas de
/// contabilización de cada operación (confirmar factura o gasto, abono, pago, ingreso manual, ajuste
/// de saldo); nunca la crean directamente los servicios de cada módulo. <see cref="SourceType"/> y
/// <see cref="SourceId"/> identifican el documento de origen para la trazabilidad ("Ver asiento") y
/// para localizar el asiento a reversar (RF-79). Se guarda en
/// <c>finanzas.Asientos</c>/<c>finanzas.AsientoLineas</c> vía
/// <c>JournalEntryConfiguration</c>/<c>JournalEntryLineConfiguration</c>.
/// <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c> impide modificarla o borrarla.
/// </remarks>
public sealed class JournalEntry
{
    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 300;

    private readonly List<JournalEntryLine> lines = [];

    private JournalEntry()
    {
    }

    /// <summary>Crea un asiento ya balanceado. No admite modificaciones posteriores.</summary>
    public JournalEntry(
        Guid id,
        long number,
        DateOnly entryDate,
        string description,
        JournalEntrySourceType sourceType,
        Guid sourceId,
        Guid? reversesEntryId,
        IReadOnlyList<JournalEntryLineInput> lineInputs,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Number = number > 0
            ? number
            : throw new ArgumentOutOfRangeException(nameof(number), number, "El número del asiento debe ser mayor que cero.");
        EntryDate = RequireDate(entryDate, nameof(entryDate));
        Description = DomainRules.RequiredText(description, DescriptionMaxLength, nameof(description));
        SourceType = DomainRules.DefinedEnum(sourceType, nameof(sourceType));
        SourceId = DomainRules.RequiredId(sourceId, nameof(sourceId));
        ReversesEntryId = DomainRules.OptionalId(reversesEntryId, nameof(reversesEntryId));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));

        BuildLines(lineInputs);
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el consecutivo del asiento (<c>finanzas.Seq_AsientoNumero</c>).</summary>
    public long Number { get; private set; }

    /// <summary>Obtiene la fecha de negocio del documento que lo origina.</summary>
    public DateOnly EntryDate { get; private set; }

    /// <summary>Obtiene la descripción: tipo y número o código del documento de origen.</summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>Obtiene el tipo de documento que lo originó.</summary>
    public JournalEntrySourceType SourceType { get; private set; }

    /// <summary>Obtiene el identificador del documento que lo originó.</summary>
    public Guid SourceId { get; private set; }

    /// <summary>Obtiene el asiento original, solo presente cuando este es su reversión (RF-79).</summary>
    public Guid? ReversesEntryId { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que generó el asiento (quien confirmó, anuló o registró el documento).</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene las líneas del asiento, ordenadas por posición.</summary>
    public IReadOnlyCollection<JournalEntryLine> Lines => lines.OrderBy(line => line.LineNumber).ToList().AsReadOnly();

    private void BuildLines(IReadOnlyList<JournalEntryLineInput> lineInputs)
    {
        ArgumentNullException.ThrowIfNull(lineInputs);

        List<JournalEntryLineInput> nonZero = lineInputs
            .Where(input => input.Debit != 0 || input.Credit != 0)
            .ToList();

        if (nonZero.Count < 2)
        {
            throw new ArgumentException("Un asiento necesita al menos dos líneas con importe.", nameof(lineInputs));
        }

        decimal totalDebit = DomainRules.RoundMoney(nonZero.Sum(input => input.Debit));
        decimal totalCredit = DomainRules.RoundMoney(nonZero.Sum(input => input.Credit));

        if (totalDebit != totalCredit)
        {
            throw new ArgumentException("La suma de débitos debe ser igual a la suma de créditos.", nameof(lineInputs));
        }

        int position = 1;

        foreach (JournalEntryLineInput input in nonZero)
        {
            lines.Add(new JournalEntryLine(Guid.NewGuid(), position, input.AccountId, input.Debit, input.Credit));
            position++;
        }
    }

    private static DateOnly RequireDate(DateOnly value, string parameterName) => value != default
        ? value
        : throw new ArgumentException("La fecha del asiento es obligatoria.", parameterName);
}
