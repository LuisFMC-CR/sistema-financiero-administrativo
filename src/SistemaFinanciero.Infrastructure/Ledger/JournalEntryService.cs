using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
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

        return new JournalEntryTraceModel(
            Map(original, accounts, userNames),
            reversal is null ? null : Map(reversal, accounts, userNames));
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

                return new JournalEntryListItem(
                    entry.Id,
                    entry.Number,
                    entry.EntryDate,
                    entry.Description,
                    entry.SourceType,
                    entry.SourceId,
                    entry.ReversesEntryId,
                    lines.Sum(line => line.Debit),
                    lines);
            })
            .ToArray();

        return new JournalEntryListResult(
            items.Length,
            items.Sum(item => item.TotalAmount),
            items);
    }

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
        IReadOnlyDictionary<Guid, string> userNames)
    {
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
            userNames.GetValueOrDefault(entry.CreatedByUserId, "—"));
    }
}
