using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Ledger;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Ledger;

/// <summary>
/// Consulta los asientos contables automáticos para la trazabilidad "Ver asiento" y ejecuta el
/// comando explícito e idempotente de generación para los documentos que ya existían antes del bloque
/// 5 del paquete de mejoras finales.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IJournalEntryService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo consume <c>JournalEntriesController</c> (Web).
/// <see cref="BackfillAsync"/> reutiliza <c>JournalPostingService</c>, las mismas reglas de
/// contabilización que ya aplican los seis servicios transaccionales, para que un documento nunca
/// tenga un asiento distinto según si se generó en vivo o por este comando.
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

    public async Task<JournalEntryBackfillResult> BackfillAsync(CancellationToken cancellationToken = default)
    {
        HashSet<(JournalEntrySourceType SourceType, Guid SourceId)> existingOriginals =
            (await dbContext.Set<JournalEntry>()
                .AsNoTracking()
                .Where(entry => entry.ReversesEntryId == null)
                .Select(entry => new { entry.SourceType, entry.SourceId })
                .ToListAsync(cancellationToken))
            .Select(entry => (entry.SourceType, entry.SourceId))
            .ToHashSet();

        List<(DateTimeOffset At, Func<CancellationToken, Task> Post)> pendingOriginals = [];

        foreach (Invoice invoice in await dbContext.Set<Invoice>()
            .Where(invoice => invoice.Status != InvoiceStatus.Draft)
            .ToListAsync(cancellationToken))
        {
            if (!existingOriginals.Add((JournalEntrySourceType.Invoice, invoice.Id)))
            {
                continue;
            }

            bool isCash = invoice.PaymentTerm == PaymentTerm.Cash;
            Guid? cashAccountId = isCash
                ? await dbContext.Set<Income>().AsNoTracking()
                    .Where(income => income.InvoiceId == invoice.Id)
                    .Select(income => (Guid?)income.CashAccountId)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            DateTimeOffset at = invoice.ConfirmedAtUtc!.Value;
            Guid actor = invoice.ConfirmedByUserId!.Value;
            pendingOriginals.Add((
                at,
                ct => JournalPostingService.PostInvoiceConfirmationAsync(dbContext, invoice, cashAccountId, at, actor, ct)));
        }

        foreach (Expense expense in await dbContext.Set<Expense>()
            .Where(expense => expense.Status != ExpenseStatus.Draft)
            .ToListAsync(cancellationToken))
        {
            if (!existingOriginals.Add((JournalEntrySourceType.Expense, expense.Id)))
            {
                continue;
            }

            DateTimeOffset at = expense.ConfirmedAtUtc!.Value;
            Guid actor = expense.ConfirmedByUserId!.Value;
            pendingOriginals.Add((at, ct => JournalPostingService.PostExpenseConfirmationAsync(dbContext, expense, at, actor, ct)));
        }

        foreach (ReceivablePayment payment in await dbContext.Set<ReceivablePayment>().ToListAsync(cancellationToken))
        {
            if (!existingOriginals.Add((JournalEntrySourceType.ReceivablePayment, payment.Id)))
            {
                continue;
            }

            decimal withholdingAmount = await dbContext.Set<Withholding>().AsNoTracking()
                .Where(withholding => withholding.ReceivablePaymentId == payment.Id)
                .Select(withholding => (decimal?)withholding.Amount)
                .SingleOrDefaultAsync(cancellationToken) ?? 0m;
            DateTimeOffset at = payment.CreatedAtUtc;
            Guid actor = payment.CreatedByUserId;
            pendingOriginals.Add((
                at,
                ct => JournalPostingService.PostReceivablePaymentAsync(dbContext, payment, withholdingAmount, at, actor, ct)));
        }

        foreach (SupplierPayment payment in await dbContext.Set<SupplierPayment>().ToListAsync(cancellationToken))
        {
            if (!existingOriginals.Add((JournalEntrySourceType.SupplierPayment, payment.Id)))
            {
                continue;
            }

            DateTimeOffset at = payment.CreatedAtUtc;
            Guid actor = payment.CreatedByUserId;
            pendingOriginals.Add((at, ct => JournalPostingService.PostSupplierPaymentAsync(dbContext, payment, at, actor, ct)));
        }

        foreach (Income income in await dbContext.Set<Income>()
            .Where(income => income.Origin == IncomeOrigin.Manual)
            .ToListAsync(cancellationToken))
        {
            if (!existingOriginals.Add((JournalEntrySourceType.Income, income.Id)))
            {
                continue;
            }

            DateTimeOffset at = income.CreatedAtUtc;
            Guid actor = income.CreatedByUserId;
            pendingOriginals.Add((at, ct => JournalPostingService.PostManualIncomeAsync(dbContext, income, at, actor, ct)));
        }

        foreach (BalanceAdjustment adjustment in await dbContext.Set<BalanceAdjustment>().ToListAsync(cancellationToken))
        {
            if (!existingOriginals.Add((JournalEntrySourceType.BalanceAdjustment, adjustment.Id)))
            {
                continue;
            }

            DateOnly entryDate = DateOnly.FromDateTime(adjustment.CreatedAtUtc.UtcDateTime);
            DateTimeOffset at = adjustment.CreatedAtUtc;
            Guid actor = adjustment.CreatedByUserId;

            pendingOriginals.Add((
                at,
                adjustment.ReceivableId is not null
                    ? ct => JournalPostingService.PostReceivableAdjustmentAsync(dbContext, adjustment, entryDate, at, actor, ct)
                    : ct => JournalPostingService.PostPayableAdjustmentAsync(dbContext, adjustment, entryDate, at, actor, ct)));
        }

        pendingOriginals.Sort((left, right) => left.At.CompareTo(right.At));

        foreach ((DateTimeOffset _, Func<CancellationToken, Task> post) in pendingOriginals)
        {
            await post(cancellationToken);
        }

        List<(DateTimeOffset At, Func<CancellationToken, Task<bool>> Reverse)> pendingReversals = [];

        foreach (Invoice invoice in await dbContext.Set<Invoice>()
            .Where(invoice => invoice.Status == InvoiceStatus.Cancelled)
            .ToListAsync(cancellationToken))
        {
            DateTimeOffset at = invoice.CancelledAtUtc!.Value;
            Guid actor = invoice.CancelledByUserId!.Value;
            pendingReversals.Add((
                at, ct => JournalPostingService.ReverseAsync(dbContext, JournalEntrySourceType.Invoice, invoice.Id, at, actor, ct)));
        }

        foreach (Expense expense in await dbContext.Set<Expense>()
            .Where(expense => expense.Status == ExpenseStatus.Cancelled)
            .ToListAsync(cancellationToken))
        {
            DateTimeOffset at = expense.CancelledAtUtc!.Value;
            Guid actor = expense.CancelledByUserId!.Value;
            pendingReversals.Add((
                at, ct => JournalPostingService.ReverseAsync(dbContext, JournalEntrySourceType.Expense, expense.Id, at, actor, ct)));
        }

        foreach (ReceivablePayment payment in await dbContext.Set<ReceivablePayment>()
            .Where(payment => payment.Status == ReceivablePaymentStatus.Cancelled)
            .ToListAsync(cancellationToken))
        {
            DateTimeOffset at = payment.CancelledAtUtc!.Value;
            Guid actor = payment.CancelledByUserId!.Value;
            pendingReversals.Add((
                at,
                ct => JournalPostingService.ReverseAsync(dbContext, JournalEntrySourceType.ReceivablePayment, payment.Id, at, actor, ct)));
        }

        foreach (SupplierPayment payment in await dbContext.Set<SupplierPayment>()
            .Where(payment => payment.Status == SupplierPaymentStatus.Cancelled)
            .ToListAsync(cancellationToken))
        {
            DateTimeOffset at = payment.CancelledAtUtc!.Value;
            Guid actor = payment.CancelledByUserId!.Value;
            pendingReversals.Add((
                at,
                ct => JournalPostingService.ReverseAsync(dbContext, JournalEntrySourceType.SupplierPayment, payment.Id, at, actor, ct)));
        }

        foreach (Income income in await dbContext.Set<Income>()
            .Where(income => income.Origin == IncomeOrigin.Manual && income.Status == IncomeStatus.Cancelled)
            .ToListAsync(cancellationToken))
        {
            DateTimeOffset at = income.CancelledAtUtc!.Value;
            Guid actor = income.CancelledByUserId!.Value;
            pendingReversals.Add((
                at, ct => JournalPostingService.ReverseAsync(dbContext, JournalEntrySourceType.Income, income.Id, at, actor, ct)));
        }

        pendingReversals.Sort((left, right) => left.At.CompareTo(right.At));

        int reversalsCreated = 0;

        foreach ((DateTimeOffset _, Func<CancellationToken, Task<bool>> reverse) in pendingReversals)
        {
            if (await reverse(cancellationToken))
            {
                reversalsCreated++;
            }
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(dbContext, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }

        return new JournalEntryBackfillResult(pendingOriginals.Count, reversalsCreated);
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
