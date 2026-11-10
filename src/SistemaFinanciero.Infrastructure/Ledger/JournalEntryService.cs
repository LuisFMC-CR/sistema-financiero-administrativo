using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Ledger;

/// <summary>
/// Consulta los asientos contables automáticos: la consulta general del período (por mes y estado) y
/// la trazabilidad "Ver asiento" de un documento.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IJournalEntryService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo consume <c>JournalEntriesController</c> (Web).
/// Los asientos los crea <c>JournalPostingService</c> dentro de la transacción de cada operación, en
/// vivo al confirmar, registrar o anular; este servicio solo lee.
/// </remarks>
internal sealed class JournalEntryService(FinancialDbContext dbContext) : IJournalEntryService
{
    public async Task<JournalEntryTraceModel?> GetTraceAsync(
        JournalEntrySourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        JournalEntry? original = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(
                entry => entry.SourceType == sourceType && entry.SourceId == sourceId && entry.ReversesEntryId == null,
                cancellationToken);

        if (original is null)
        {
            return null;
        }

        JournalEntry? reversal = await dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(entry => entry.ReversesEntryId == original.Id, cancellationToken);

        JournalEntry[] both = reversal is null ? [original] : [original, reversal];

        IReadOnlyDictionary<Guid, (string Code, string Name)> accounts = await GetAccountInfoAsync(both, cancellationToken);
        IReadOnlyDictionary<Guid, string> userNames = await GetUserNamesAsync(
            both.Select(entry => entry.CreatedByUserId), cancellationToken);
        Dictionary<Guid, (JournalEntryDocumentKind Kind, Guid DocumentId)> documentLinks =
            await ResolveDocumentLinksAsync(both, cancellationToken);

        return new JournalEntryTraceModel(
            Map(original, accounts, userNames, documentLinks),
            reversal is null ? null : Map(reversal, accounts, userNames, documentLinks));
    }

    public async Task<JournalEntryListResult> SearchAsync(
        int periodYear,
        int periodMonth,
        JournalEntryStateFilter state,
        long? entryNumber = null,
        CancellationToken cancellationToken = default)
    {
        DateOnly periodFrom = new(periodYear, periodMonth, 1);
        DateOnly periodTo = periodFrom.AddMonths(1).AddDays(-1);

        IQueryable<JournalEntry> query = dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .Include(entry => entry.Lines)
            .Where(entry => entry.EntryDate >= periodFrom && entry.EntryDate <= periodTo);

        query = state switch
        {
            JournalEntryStateFilter.Posted => query.Where(entry => entry.ReversesEntryId == null),
            JournalEntryStateFilter.Reversal => query.Where(entry => entry.ReversesEntryId != null),
            _ => query,
        };

        if (entryNumber is long number)
        {
            query = query.Where(entry => entry.Number == number);
        }

        List<JournalEntry> entries = await query
            .OrderByDescending(entry => entry.Number)
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<Guid, (string Code, string Name)> accounts = await GetAccountInfoAsync(entries, cancellationToken);
        Dictionary<Guid, (JournalEntryDocumentKind Kind, Guid DocumentId)> documentLinks =
            await ResolveDocumentLinksAsync(entries, cancellationToken);

        JournalEntryListItem[] items = entries
            .Select(entry =>
            {
                JournalEntryListLine[] lines = entry.Lines
                    .OrderBy(line => line.LineNumber)
                    .Select(line =>
                    {
                        (string code, string name) = accounts.TryGetValue(line.AccountId, out (string Code, string Name) info)
                            ? info
                            : ("—", "—");
                        return new JournalEntryListLine(line.LineNumber, code, name, line.Debit, line.Credit);
                    })
                    .ToArray();

                (JournalEntryDocumentKind documentKind, Guid documentId) = documentLinks.TryGetValue(entry.SourceId, out var link)
                    ? link
                    : (DefaultDocumentKind(entry.SourceType), entry.SourceId);

                return new JournalEntryListItem(
                    entry.Id,
                    entry.Number,
                    entry.EntryDate,
                    entry.Description,
                    entry.SourceType,
                    entry.SourceId,
                    entry.ReversesEntryId,
                    lines.Sum(line => line.Debit),
                    lines,
                    documentKind,
                    documentId);
            })
            .ToArray();

        return new JournalEntryListResult(
            items.Length,
            items.Sum(item => item.TotalAmount),
            items);
    }

    /// <summary>
    /// Resuelve el documento al que cada asiento debe enlazar de vuelta ("ver documento"): directo
    /// para los orígenes con pantalla propia (<see cref="DefaultDocumentKind"/>), y a través de la
    /// cuenta por cobrar o por pagar padre para abonos, pagos a proveedores y ajustes de saldo, que no
    /// tienen una pantalla propia por id.
    /// </summary>
    private async Task<Dictionary<Guid, (JournalEntryDocumentKind Kind, Guid DocumentId)>> ResolveDocumentLinksAsync(
        IReadOnlyCollection<JournalEntry> entries,
        CancellationToken cancellationToken)
    {
        Dictionary<Guid, (JournalEntryDocumentKind Kind, Guid DocumentId)> links = [];

        Guid[] receivablePaymentIds = SourceIds(entries, JournalEntrySourceType.ReceivablePayment);
        if (receivablePaymentIds.Length > 0)
        {
            var payments = await dbContext.Set<ReceivablePayment>()
                .AsNoTracking()
                .Where(payment => receivablePaymentIds.Contains(payment.Id))
                .Select(payment => new { payment.Id, payment.ReceivableId })
                .ToListAsync(cancellationToken);
            foreach (var payment in payments)
            {
                links[payment.Id] = (JournalEntryDocumentKind.Receivable, payment.ReceivableId);
            }
        }

        Guid[] supplierPaymentIds = SourceIds(entries, JournalEntrySourceType.SupplierPayment);
        if (supplierPaymentIds.Length > 0)
        {
            var payments = await dbContext.Set<SupplierPayment>()
                .AsNoTracking()
                .Where(payment => supplierPaymentIds.Contains(payment.Id))
                .Select(payment => new { payment.Id, payment.PayableId })
                .ToListAsync(cancellationToken);
            foreach (var payment in payments)
            {
                links[payment.Id] = (JournalEntryDocumentKind.Payable, payment.PayableId);
            }
        }

        Guid[] adjustmentIds = SourceIds(entries, JournalEntrySourceType.BalanceAdjustment);
        if (adjustmentIds.Length > 0)
        {
            var adjustments = await dbContext.Set<BalanceAdjustment>()
                .AsNoTracking()
                .Where(adjustment => adjustmentIds.Contains(adjustment.Id))
                .Select(adjustment => new { adjustment.Id, adjustment.ReceivableId, adjustment.PayableId })
                .ToListAsync(cancellationToken);
            foreach (var adjustment in adjustments)
            {
                links[adjustment.Id] = adjustment.ReceivableId is Guid receivableId
                    ? (JournalEntryDocumentKind.Receivable, receivableId)
                    : (JournalEntryDocumentKind.Payable, adjustment.PayableId!.Value);
            }
        }

        return links;
    }

    private static Guid[] SourceIds(IReadOnlyCollection<JournalEntry> entries, JournalEntrySourceType sourceType) =>
        entries.Where(entry => entry.SourceType == sourceType).Select(entry => entry.SourceId).Distinct().ToArray();

    /// <summary>
    /// El documento de navegación de un origen con pantalla propia por id: coincide siempre con
    /// <see cref="JournalEntry.SourceId"/>. Los orígenes sin pantalla propia (abono, pago a proveedor,
    /// ajuste de saldo) se resuelven en <see cref="ResolveDocumentLinksAsync"/>, nunca aquí.
    /// </summary>
    private static JournalEntryDocumentKind DefaultDocumentKind(JournalEntrySourceType sourceType) => sourceType switch
    {
        JournalEntrySourceType.Invoice => JournalEntryDocumentKind.Invoice,
        JournalEntrySourceType.Income => JournalEntryDocumentKind.Income,
        JournalEntrySourceType.Expense => JournalEntryDocumentKind.Expense,
        JournalEntrySourceType.CreditNote => JournalEntryDocumentKind.CreditNote,
        JournalEntrySourceType.CashTransfer => JournalEntryDocumentKind.CashTransfer,
        JournalEntrySourceType.VatSettlement => JournalEntryDocumentKind.VatSettlement,
        JournalEntrySourceType.IncomeTaxPayment => JournalEntryDocumentKind.IncomeTaxPayment,
        _ => throw new ArgumentOutOfRangeException(
            nameof(sourceType), sourceType, "Este origen no tiene una pantalla propia por id."),
    };

    private async Task<IReadOnlyDictionary<Guid, (string Code, string Name)>> GetAccountInfoAsync(
        IReadOnlyCollection<JournalEntry> entries,
        CancellationToken cancellationToken)
    {
        Guid[] accountIds = entries
            .SelectMany(entry => entry.Lines)
            .Select(line => line.AccountId)
            .Distinct()
            .ToArray();

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => (account.Code, account.Name), cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetUserNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        Guid[] ids = userIds.Distinct().ToArray();

        return await dbContext.Users
            .AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);
    }

    private static JournalEntryModel Map(
        JournalEntry entry,
        IReadOnlyDictionary<Guid, (string Code, string Name)> accounts,
        IReadOnlyDictionary<Guid, string> userNames,
        Dictionary<Guid, (JournalEntryDocumentKind Kind, Guid DocumentId)> documentLinks)
    {
        (JournalEntryDocumentKind documentKind, Guid documentId) = documentLinks.TryGetValue(entry.SourceId, out var link)
            ? link
            : (DefaultDocumentKind(entry.SourceType), entry.SourceId);

        return new JournalEntryModel(
            entry.Id,
            entry.Number,
            entry.EntryDate,
            entry.Description,
            entry.SourceType,
            entry.SourceId,
            entry.ReversesEntryId,
            entry.Lines
                .OrderBy(line => line.LineNumber)
                .Select(line =>
                {
                    (string code, string name) = accounts.TryGetValue(line.AccountId, out (string Code, string Name) info)
                        ? info
                        : ("—", "—");
                    return new JournalEntryLineModel(line.LineNumber, line.AccountId, code, name, line.Debit, line.Credit);
                })
                .ToArray(),
            entry.CreatedAtUtc,
            userNames.GetValueOrDefault(entry.CreatedByUserId, "—"),
            documentKind,
            documentId);
    }
}
