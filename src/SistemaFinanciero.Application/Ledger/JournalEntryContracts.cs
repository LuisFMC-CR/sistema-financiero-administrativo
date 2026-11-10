using SistemaFinanciero.Domain.Ledger;

namespace SistemaFinanciero.Application.Ledger;

/// <summary>
/// El documento al que se debe enlazar de vuelta desde un asiento ("ver documento"), independiente de
/// <see cref="JournalEntrySourceType"/>: los abonos, los pagos a proveedores y los ajustes de saldo no
/// tienen pantalla propia, así que su documento de navegación es la cuenta por cobrar o por pagar a la
/// que pertenecen, no el abono/pago/ajuste mismo.
/// </summary>
public enum JournalEntryDocumentKind
{
    Invoice = 1,
    Receivable = 2,
    Income = 3,
    Expense = 4,
    Payable = 5,
    CreditNote = 6,
    CashTransfer = 7,
    VatSettlement = 8,
    IncomeTaxPayment = 9,
}

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
    string CreatedByFullName,
    JournalEntryDocumentKind DocumentKind,
    Guid DocumentId);

/// <summary>
/// El asiento de un documento y, si existe, su reversión (RF-79), para la trazabilidad "Ver asiento"
/// (bloque 5 del paquete de mejoras finales, punto 5).
/// </summary>
public sealed record JournalEntryTraceModel(JournalEntryModel Entry, JournalEntryModel? Reversal);

/// <summary>
/// Estado por el que se puede filtrar la consulta general de asientos: todos, solo los contabilizados
/// (asientos originales) o solo las reversiones generadas al anular un documento.
/// </summary>
public enum JournalEntryStateFilter
{
    /// <summary>Asientos originales y reversiones.</summary>
    All = 1,

    /// <summary>Solo asientos originales (documentos vigentes).</summary>
    Posted = 2,

    /// <summary>Solo asientos de reversión (documentos anulados).</summary>
    Reversal = 3,
}

/// <summary>Una línea de un asiento dentro de la consulta general de asientos.</summary>
public sealed record JournalEntryListLine(
    int LineNumber,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit);

/// <summary>
/// Un asiento dentro de la consulta general de asientos: sus datos de cabecera, el total del
/// movimiento (la suma de sus débitos) y sus líneas, para mostrar en pantalla sin abrir otro documento.
/// </summary>
public sealed record JournalEntryListItem(
    Guid Id,
    long Number,
    DateOnly EntryDate,
    string Description,
    JournalEntrySourceType SourceType,
    Guid SourceId,
    Guid? ReversesEntryId,
    decimal TotalAmount,
    IReadOnlyList<JournalEntryListLine> Lines,
    JournalEntryDocumentKind DocumentKind,
    Guid DocumentId);

/// <summary>
/// Resultado de la consulta general de asientos: el conteo, el total de movimientos (la suma de los
/// débitos de los asientos listados) y el detalle de cada asiento, tras aplicar los filtros vigentes.
/// </summary>
public sealed record JournalEntryListResult(
    int EntriesCount,
    decimal TotalMovements,
    IReadOnlyList<JournalEntryListItem> Entries);

/// <summary>
/// Consulta de solo lectura de los asientos contables automáticos (RF-78/RF-79), que se generan
/// dentro de la transacción de cada operación y nunca se editan ni se borran.
/// </summary>
/// <remarks>
/// Conexiones: la implementa <c>JournalEntryService</c> (Infrastructure/Ledger) sobre
/// <c>JournalEntry</c>/<c>JournalEntryLine</c>, que <c>JournalPostingService</c> genera dentro de la
/// transacción de cada operación. La consume <c>JournalEntriesController</c> (Web, ruta
/// <c>/contabilidad/asientos</c>): <see cref="SearchAsync"/> alimenta la consulta general del período y
/// <see cref="GetTraceAsync"/> el enlace "Ver asiento" del detalle de cada documento. Ambas exigen
/// <c>SystemPolicies.ViewFinancialReports</c> (Gerencia y Finanzas).
/// </remarks>
public interface IJournalEntryService
{
    /// <summary>
    /// Obtiene el asiento de un documento y, si existe, su reversión. Nulo si el documento no tiene
    /// asiento todavía (por ejemplo, un borrador).
    /// </summary>
    Task<JournalEntryTraceModel?> GetTraceAsync(
        JournalEntrySourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista los asientos del mes <paramref name="periodYear"/>/<paramref name="periodMonth"/>,
    /// filtrados por estado y número opcional, del más reciente al más antiguo.
    /// </summary>
    Task<JournalEntryListResult> SearchAsync(
        int periodYear,
        int periodMonth,
        JournalEntryStateFilter state,
        long? entryNumber = null,
        CancellationToken cancellationToken = default);
}
