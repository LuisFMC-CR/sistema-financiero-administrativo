using SistemaFinanciero.Domain.Ledger;

namespace SistemaFinanciero.Application.Ledger;

/// <summary>Una línea de un asiento, lista para mostrar.</summary>
public sealed record JournalEntryLineModel(
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit);

/// <summary>Un asiento contable, con sus líneas, listo para mostrar ("Ver asiento").</summary>
public sealed record JournalEntryModel(
    Guid Id,
    long Number,
    DateOnly EntryDate,
    string Description,
    JournalEntrySourceType SourceType,
    Guid SourceId,
    Guid? ReversesEntryId,
    IReadOnlyList<JournalEntryLineModel> Lines,
    DateTimeOffset CreatedAtUtc,
    string CreatedByFullName);

/// <summary>
/// El asiento de un documento y, si existe, su reversión (RF-79), para la trazabilidad "Ver asiento"
/// (bloque 5 del paquete de mejoras finales, punto 5).
/// </summary>
public sealed record JournalEntryTraceModel(JournalEntryModel Entry, JournalEntryModel? Reversal);

/// <summary>Resultado del comando de generación de asientos para documentos ya existentes.</summary>
public sealed record JournalEntryBackfillResult(int EntriesCreated, int ReversalsCreated);

/// <summary>
/// Consulta de solo lectura de los asientos contables automáticos (RF-78/RF-79) y comando explícito de
/// generación para los documentos que ya existían antes de este bloque.
/// </summary>
/// <remarks>
/// Conexiones: la implementa <c>JournalEntryService</c> (Infrastructure/Ledger) sobre
/// <c>JournalEntry</c>/<c>JournalEntryLine</c>, que <c>JournalPostingService</c> genera dentro de la
/// transacción de cada operación. La consume <c>JournalEntriesController</c> (Web, ruta
/// <c>/contabilidad/asientos</c>): <see cref="GetTraceAsync"/> tras <c>SystemPolicies.ViewFinancialReports</c>
/// ("Ver asiento" en el detalle de cada documento) y <see cref="BackfillAsync"/> tras
/// <c>SystemPolicies.ManageSystemParameters</c> (exclusivo de Gerencia; comando explícito, nunca se
/// ejecuta al arrancar).
/// </remarks>
public interface IJournalEntryService
{
    /// <summary>
    /// Obtiene el asiento de un documento y, si existe, su reversión. Nulo si el documento no tiene
    /// asiento todavía (por ejemplo, un borrador, o uno anterior a este bloque sin generar).
    /// </summary>
    Task<JournalEntryTraceModel?> GetTraceAsync(
        JournalEntrySourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera los asientos de los documentos confirmados que todavía no tienen uno y las reversiones
    /// de los anulados que todavía no tienen la suya, en orden cronológico. Es idempotente: una
    /// segunda ejecución no duplica nada.
    /// </summary>
    Task<JournalEntryBackfillResult> BackfillAsync(CancellationToken cancellationToken = default);
}
