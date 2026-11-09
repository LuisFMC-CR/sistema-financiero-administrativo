using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Domain.VatSettlements;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Ledger;

/// <summary>
/// Compone y registra los asientos contables automáticos de partida doble (RF-78) y sus reversiones
/// (RF-79), dentro de la misma transacción que cada operación que los origina.
/// </summary>
/// <remarks>
/// Conexiones: es <c>internal</c>; lo usan <c>InvoiceService</c>, <c>ExpenseService</c>,
/// <c>ReceivablePaymentService</c>, <c>IncomeService</c>, <c>SupplierPaymentService</c> y
/// <c>BalanceAdjustmentService</c> (cada uno al confirmar o anular), y <c>JournalEntryService</c> para
/// el comando de generación de datos existentes (bloque 5 del paquete de mejoras finales). Resuelve las
/// cuentas de sistema por <see cref="LedgerAccountSystemRole"/>, nunca por código ni nombre (regla 1).
/// </remarks>
internal static class JournalPostingService
{
    /// <summary>Indica si ya existe el asiento original de un documento (no su reversión).</summary>
    public static Task<bool> ExistsOriginalAsync(
        FinancialDbContext dbContext,
        JournalEntrySourceType sourceType,
        Guid sourceId,
        CancellationToken cancellationToken) =>
        dbContext.Set<JournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                entry => entry.SourceType == sourceType && entry.SourceId == sourceId && entry.ReversesEntryId == null,
                cancellationToken);

    /// <summary>
    /// Factura confirmada de contado (débito cuenta de efectivo) o a crédito (débito cuentas por
    /// cobrar), siempre contra la categoría de ingreso (neto) y el IVA por pagar (regla
    /// de contabilización, bloque 5).
    /// </summary>
    public static async Task PostInvoiceConfirmationAsync(
        FinancialDbContext dbContext,
        Invoice invoice,
        Guid? cashAccountId,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid incomeAccountId = invoice.IncomeAccountId;
        Guid debitAccountId = cashAccountId
            ?? await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.ReceivablesFromCustomers, cancellationToken);
        Guid vatPayableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.VatPayable, cancellationToken);

        List<JournalEntryLineInput> lines =
        [
            new(debitAccountId, invoice.TotalAmount, 0m),
            new(incomeAccountId, 0m, invoice.NetAmount),
            new(vatPayableAccountId, 0m, invoice.TaxAmount),
        ];

        await CreateEntryAsync(
            dbContext,
            $"Factura {invoice.Number}",
            JournalEntrySourceType.Invoice,
            invoice.Id,
            invoice.IssueDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Gasto confirmado de contado (crédito cuenta de efectivo) o a crédito (crédito cuentas por
    /// pagar). Si el IVA es acreditable (bloque 1 del paquete v7, RF-94), debita la categoría por el
    /// neto y el IVA acreditable por separado; si no lo es, debita la categoría por el total (neto +
    /// IVA) y omite la línea de IVA acreditable, porque ese IVA pasa a formar parte del costo.
    /// </summary>
    public static async Task PostExpenseConfirmationAsync(
        FinancialDbContext dbContext,
        Expense expense,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid categoryAccountId = expense.AccountId;
        Guid vatCreditableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.VatCreditable, cancellationToken);
        Guid creditAccountId = expense.CashAccountId
            ?? await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.PayablesToSuppliers, cancellationToken);

        decimal categoryDebit = expense.VatCreditable ? expense.NetAmount : expense.TotalAmount;
        decimal vatCreditableDebit = expense.VatCreditable ? expense.TaxAmount : 0m;

        List<JournalEntryLineInput> lines =
        [
            new(categoryAccountId, categoryDebit, 0m),
            new(vatCreditableAccountId, vatCreditableDebit, 0m),
            new(creditAccountId, 0m, expense.TotalAmount),
        ];

        await CreateEntryAsync(
            dbContext,
            $"Gasto {expense.Code}",
            JournalEntrySourceType.Expense,
            expense.Id,
            expense.IssueDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Abono: débito cuenta de efectivo y retenciones a favor (si hay), crédito cuentas por cobrar por
    /// el total. El ingreso que genera el abono no tiene asiento propio.
    /// </summary>
    public static async Task PostReceivablePaymentAsync(
        FinancialDbContext dbContext,
        ReceivablePayment payment,
        decimal withholdingAmount,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid receivableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.ReceivablesFromCustomers, cancellationToken);
        Guid withholdingAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.WithholdingsReceivable, cancellationToken);

        List<JournalEntryLineInput> lines =
        [
            new(payment.CashAccountId, payment.Amount, 0m),
            new(withholdingAccountId, withholdingAmount, 0m),
            new(receivableAccountId, 0m, payment.Amount + withholdingAmount),
        ];

        await CreateEntryAsync(
            dbContext,
            $"Abono {payment.Code}",
            JournalEntrySourceType.ReceivablePayment,
            payment.Id,
            payment.PaymentDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>Pago a proveedor: débito cuentas por pagar, crédito cuenta de efectivo.</summary>
    public static async Task PostSupplierPaymentAsync(
        FinancialDbContext dbContext,
        SupplierPayment payment,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid payableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.PayablesToSuppliers, cancellationToken);

        List<JournalEntryLineInput> lines =
        [
            new(payableAccountId, payment.Amount, 0m),
            new(payment.CashAccountId, 0m, payment.Amount),
        ];

        await CreateEntryAsync(
            dbContext,
            $"Pago {payment.Code}",
            JournalEntrySourceType.SupplierPayment,
            payment.Id,
            payment.PaymentDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>Ingreso manual: débito cuenta de efectivo, crédito cuenta de su categoría.</summary>
    public static async Task PostManualIncomeAsync(
        FinancialDbContext dbContext,
        Income income,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid categoryAccountId = income.AccountId;

        List<JournalEntryLineInput> lines =
        [
            new(income.CashAccountId, income.Amount, 0m),
            new(categoryAccountId, 0m, income.Amount),
        ];

        await CreateEntryAsync(
            dbContext,
            $"Ingreso {income.Code}",
            JournalEntrySourceType.Income,
            income.Id,
            income.IncomeDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Ajuste de cuenta por cobrar: disminución débito Ajustes a cuentas por cobrar / crédito Cuentas
    /// por cobrar; aumento al revés.
    /// </summary>
    public static async Task PostReceivableAdjustmentAsync(
        FinancialDbContext dbContext,
        BalanceAdjustment adjustment,
        DateOnly entryDate,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid receivableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.ReceivablesFromCustomers, cancellationToken);
        Guid adjustmentAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.ReceivableAdjustments, cancellationToken);

        List<JournalEntryLineInput> lines = adjustment.Direction == BalanceAdjustmentDirection.Decrease
            ?
            [
                new(adjustmentAccountId, adjustment.Amount, 0m),
                new(receivableAccountId, 0m, adjustment.Amount),
            ]
            :
            [
                new(receivableAccountId, adjustment.Amount, 0m),
                new(adjustmentAccountId, 0m, adjustment.Amount),
            ];

        await CreateEntryAsync(
            dbContext,
            "Ajuste de cuenta por cobrar",
            JournalEntrySourceType.BalanceAdjustment,
            adjustment.Id,
            entryDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Ajuste de cuenta por pagar: disminución débito Cuentas por pagar / crédito Ajustes a favor en
    /// cuentas por pagar; aumento al revés.
    /// </summary>
    public static async Task PostPayableAdjustmentAsync(
        FinancialDbContext dbContext,
        BalanceAdjustment adjustment,
        DateOnly entryDate,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid payableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.PayablesToSuppliers, cancellationToken);
        Guid adjustmentAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.PayableAdjustmentsInFavor, cancellationToken);

        List<JournalEntryLineInput> lines = adjustment.Direction == BalanceAdjustmentDirection.Decrease
            ?
            [
                new(payableAccountId, adjustment.Amount, 0m),
                new(adjustmentAccountId, 0m, adjustment.Amount),
            ]
            :
            [
                new(adjustmentAccountId, adjustment.Amount, 0m),
                new(payableAccountId, 0m, adjustment.Amount),
            ];

        await CreateEntryAsync(
            dbContext,
            "Ajuste de cuenta por pagar",
            JournalEntrySourceType.BalanceAdjustment,
            adjustment.Id,
            entryDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Nota de crédito total: debita devoluciones y el IVA por pagar; acredita el saldo pendiente,
    /// la devolución en efectivo y las retenciones vigentes revertidas.
    /// </summary>
    public static async Task PostCreditNoteAsync(
        FinancialDbContext dbContext,
        CreditNote creditNote,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid returnsAccountId = await GetSystemAccountIdAsync(
            dbContext,
            LedgerAccountSystemRole.SalesReturnsAndAllowances,
            cancellationToken);
        Guid vatPayableAccountId = await GetSystemAccountIdAsync(
            dbContext,
            LedgerAccountSystemRole.VatPayable,
            cancellationToken);

        List<JournalEntryLineInput> lines =
        [
            new(returnsAccountId, creditNote.NetAmount, 0m),
            new(vatPayableAccountId, creditNote.TaxAmount, 0m),
        ];

        if (creditNote.AppliedToBalance > 0m)
        {
            Guid receivableAccountId = await GetSystemAccountIdAsync(
                dbContext,
                LedgerAccountSystemRole.ReceivablesFromCustomers,
                cancellationToken);
            lines.Add(new JournalEntryLineInput(receivableAccountId, 0m, creditNote.AppliedToBalance));
        }

        if (creditNote.RefundedAmount > 0m)
        {
            lines.Add(new JournalEntryLineInput(creditNote.CashAccountId!.Value, 0m, creditNote.RefundedAmount));
        }

        if (creditNote.WithholdingReversedAmount > 0m)
        {
            Guid withholdingAccountId = await GetSystemAccountIdAsync(
                dbContext,
                LedgerAccountSystemRole.WithholdingsReceivable,
                cancellationToken);
            lines.Add(new JournalEntryLineInput(withholdingAccountId, 0m, creditNote.WithholdingReversedAmount));
        }

        await CreateEntryAsync(
            dbContext,
            $"Nota de crédito {creditNote.Code}",
            JournalEntrySourceType.CreditNote,
            creditNote.Id,
            creditNote.IssueDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>Traspaso entre cuentas de efectivo: débito cuenta de destino, crédito cuenta de origen.</summary>
    public static async Task PostCashTransferAsync(
        FinancialDbContext dbContext,
        CashTransfer transfer,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        List<JournalEntryLineInput> lines =
        [
            new(transfer.DestinationCashAccountId, transfer.Amount, 0m),
            new(transfer.SourceCashAccountId, 0m, transfer.Amount),
        ];

        await CreateEntryAsync(
            dbContext,
            $"Traspaso {transfer.Code}",
            JournalEntrySourceType.CashTransfer,
            transfer.Id,
            transfer.TransferDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Liquidación mensual del IVA: débito IVA por pagar (monto liquidado), crédito IVA acreditable
    /// (monto aplicado) y crédito cuenta de efectivo (monto pagado, si hubo pago). Las líneas en cero
    /// se omiten (por ejemplo, el pago cuando el período queda con saldo a favor).
    /// </summary>
    public static async Task PostVatSettlementAsync(
        FinancialDbContext dbContext,
        VatSettlement settlement,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        Guid vatPayableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.VatPayable, cancellationToken);
        Guid vatCreditableAccountId = await GetSystemAccountIdAsync(dbContext, LedgerAccountSystemRole.VatCreditable, cancellationToken);

        List<JournalEntryLineInput> lines =
        [
            new(vatPayableAccountId, settlement.VatPayableAmount, 0m),
            new(vatCreditableAccountId, 0m, settlement.VatCreditApplied),
        ];

        if (settlement.PaymentAmount > 0m)
        {
            lines.Add(new JournalEntryLineInput(settlement.CashAccountId!.Value, 0m, settlement.PaymentAmount));
        }

        await CreateEntryAsync(
            dbContext,
            $"Liquidación de IVA {settlement.PeriodMonth:D2}/{settlement.PeriodYear}",
            JournalEntrySourceType.VatSettlement,
            settlement.Id,
            settlement.SettlementDate,
            lines,
            postedAtUtc,
            actorId,
            cancellationToken);
    }

    /// <summary>
    /// Genera el asiento de reversión del asiento original de un documento (RF-79): las mismas líneas
    /// con débitos y créditos intercambiados, con la misma fecha del original. No hace nada si el
    /// documento no tiene asiento o si ya fue reversado (comando de generación idempotente).
    /// </summary>
    public static async Task<bool> ReverseAsync(
        FinancialDbContext dbContext,
        JournalEntrySourceType sourceType,
        Guid sourceId,
        DateTimeOffset reversedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        JournalEntry? original = await dbContext.Set<JournalEntry>()
            .Include(entry => entry.Lines)
            .SingleOrDefaultAsync(
                entry => entry.SourceType == sourceType && entry.SourceId == sourceId && entry.ReversesEntryId == null,
                cancellationToken);

        if (original is null)
        {
            return false;
        }

        bool alreadyReversed = await dbContext.Set<JournalEntry>()
            .AnyAsync(entry => entry.ReversesEntryId == original.Id, cancellationToken);

        if (alreadyReversed)
        {
            return false;
        }

        List<JournalEntryLineInput> swapped = original.Lines
            .OrderBy(line => line.LineNumber)
            .Select(line => new JournalEntryLineInput(line.AccountId, line.Credit, line.Debit))
            .ToList();

        long number = await CatalogPersistence.GetNextSequenceValueAsync(
            dbContext, "finanzas.Seq_AsientoNumero", cancellationToken);

        JournalEntry reversal = new(
            Guid.NewGuid(),
            number,
            original.EntryDate,
            $"Reversión — {original.Description}",
            original.SourceType,
            original.SourceId,
            original.Id,
            swapped,
            reversedAtUtc,
            actorId);

        dbContext.Add(reversal);
        return true;
    }

    private static async Task CreateEntryAsync(
        FinancialDbContext dbContext,
        string description,
        JournalEntrySourceType sourceType,
        Guid sourceId,
        DateOnly entryDate,
        IReadOnlyList<JournalEntryLineInput> lines,
        DateTimeOffset postedAtUtc,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        long number = await CatalogPersistence.GetNextSequenceValueAsync(
            dbContext, "finanzas.Seq_AsientoNumero", cancellationToken);

        JournalEntry entry = new(
            Guid.NewGuid(),
            number,
            entryDate,
            description,
            sourceType,
            sourceId,
            reversesEntryId: null,
            lines,
            postedAtUtc,
            actorId);

        dbContext.Add(entry);
    }

    private static async Task<Guid> GetSystemAccountIdAsync(
        FinancialDbContext dbContext,
        LedgerAccountSystemRole role,
        CancellationToken cancellationToken)
    {
        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.SystemRole == role)
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            throw new InvalidOperationException(
                $"No existe una cuenta contable para el rol de sistema {role}.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException(
                $"La cuenta contable de sistema «{account.Name}» está inactiva; reactívela antes de registrar esta operación.");
        }

        return account.Id;
    }
}
