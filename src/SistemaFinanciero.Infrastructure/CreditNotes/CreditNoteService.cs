using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Common;
using SistemaFinanciero.Infrastructure.Ledger;
using SistemaFinanciero.Infrastructure.Persistence;
using SistemaFinanciero.Infrastructure.Parameters;

namespace SistemaFinanciero.Infrastructure.CreditNotes;

internal sealed class CreditNoteService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ICreditNoteService
{
    public async Task<PagedResult<CreditNoteInvoiceOption>> SearchEligibleInvoicesAsync(
        string? invoiceOrCustomer,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Invoice> query = dbContext.Invoices.AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatus.Confirmed);

        if (!string.IsNullOrWhiteSpace(invoiceOrCustomer))
        {
            string search = invoiceOrCustomer.Trim();
            if (long.TryParse(search, out long invoiceNumber))
            {
                query = query.Where(invoice => invoice.Number == invoiceNumber
                    || dbContext.Customers.Any(customer =>
                        customer.Id == invoice.CustomerId && customer.Name.Contains(search)));
            }
            else
            {
                query = query.Where(invoice => dbContext.Customers.Any(customer =>
                    customer.Id == invoice.CustomerId && customer.Name.Contains(search)));
            }
        }

        var eligible = from invoice in query
            join customer in dbContext.Customers.AsNoTracking() on invoice.CustomerId equals customer.Id
            let receivable = dbContext.Receivables.AsNoTracking()
                .Where(item => item.InvoiceId == invoice.Id)
                .Select(item => new { item.Id, item.Balance })
                .FirstOrDefault()
            let cashReceived = invoice.PaymentTerm == PaymentTerm.Cash
                ? invoice.TotalAmount
                : (from payment in dbContext.ReceivablePayments.AsNoTracking()
                    where payment.ReceivableId == receivable!.Id
                        && payment.Status == ReceivablePaymentStatus.Active
                    select (decimal?)payment.Amount).Sum() ?? 0m
            let withholdingAmount = invoice.PaymentTerm == PaymentTerm.Cash
                ? 0m
                : (from withholding in dbContext.Withholdings.AsNoTracking()
                    join payment in dbContext.ReceivablePayments.AsNoTracking()
                        on withholding.ReceivablePaymentId equals payment.Id
                    where payment.ReceivableId == receivable!.Id
                        && payment.Status == ReceivablePaymentStatus.Active
                    select (decimal?)withholding.Amount).Sum() ?? 0m
            let hasBalanceAdjustments = invoice.PaymentTerm != PaymentTerm.Cash
                && dbContext.BalanceAdjustments.AsNoTracking()
                    .Any(adjustment => adjustment.ReceivableId == receivable!.Id)
            orderby invoice.IssueDate descending, invoice.Number descending
            select new
            {
                invoice.Id,
                invoice.RowVersion,
                InvoiceNumber = invoice.Number ?? 0,
                CustomerName = customer.Name,
                invoice.IssueDate,
                invoice.DueDate,
                IsCashInvoice = invoice.PaymentTerm == PaymentTerm.Cash,
                invoice.TotalAmount,
                ReceivableBalance = receivable == null ? 0m : receivable.Balance,
                CashReceived = cashReceived,
                WithholdingAmount = withholdingAmount,
                HasBalanceAdjustments = hasBalanceAdjustments,
            };

        int normalizedPage = Math.Max(1, page);
        int normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        int totalCount = await eligible.CountAsync(cancellationToken);
        var rows = await eligible
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CreditNoteInvoiceOption>(
            rows.Select(invoice => new CreditNoteInvoiceOption(
                invoice.Id,
                Convert.ToBase64String(invoice.RowVersion),
                invoice.InvoiceNumber,
                invoice.CustomerName,
                invoice.IssueDate,
                invoice.DueDate,
                invoice.IsCashInvoice,
                invoice.TotalAmount,
                invoice.ReceivableBalance,
                invoice.CashReceived,
                invoice.WithholdingAmount,
                invoice.HasBalanceAdjustments))
                .ToArray(),
            normalizedPage,
            normalizedPageSize,
            totalCount);
    }

    public async Task<PagedResult<CreditNoteListItem>> SearchAsync(
        CreditNoteQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<CreditNote> notes = dbContext.CreditNotes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            string code = query.Code.Trim();
            notes = notes.Where(note => note.Code.Contains(code));
        }

        if (!string.IsNullOrWhiteSpace(query.CustomerName))
        {
            string customerName = query.CustomerName.Trim();
            notes = notes.Where(note => dbContext.Invoices.Any(invoice =>
                invoice.Id == note.InvoiceId
                && dbContext.Customers.Any(customer =>
                    customer.Id == invoice.CustomerId && customer.Name.Contains(customerName))));
        }

        if (query.InvoiceNumber is long invoiceNumber)
        {
            notes = notes.Where(note => dbContext.Invoices.Any(invoice =>
                invoice.Id == note.InvoiceId && invoice.Number == invoiceNumber));
        }

        if (query.IssueDate is DateOnly issueDate)
        {
            notes = notes.Where(note => note.IssueDate == issueDate);
        }

        if (query.Status is CreditNoteStatus status)
        {
            notes = notes.Where(note => note.Status == status);
        }

        var result = from note in notes
            join invoice in dbContext.Invoices.AsNoTracking() on note.InvoiceId equals invoice.Id
            join customer in dbContext.Customers.AsNoTracking() on invoice.CustomerId equals customer.Id
            orderby note.IssueDate descending, note.Code descending
            select new CreditNoteListItem(
                note.Id,
                note.Code,
                invoice.Number ?? 0,
                customer.Name,
                note.IssueDate,
                note.TotalAmount,
                note.RefundedAmount,
                note.Status);

        return await CatalogPersistence.ToPageAsync(result, query.Page, query.PageSize, cancellationToken);
    }

    public async Task<CreditNoteInvoiceOption?> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Invoice? invoice = await dbContext.Invoices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == invoiceId && item.Status == InvoiceStatus.Confirmed, cancellationToken);
        if (invoice is null)
        {
            return null;
        }

        return (await SearchEligibleInvoicesAsync(
            invoice.Number?.ToString(System.Globalization.CultureInfo.InvariantCulture),
            1,
            100,
            cancellationToken)).Items.SingleOrDefault(item => item.InvoiceId == invoiceId);
    }

    public async Task<CreditNoteModel?> GetAsync(
        Guid creditNoteId,
        CancellationToken cancellationToken = default)
    {
        CreditNote? note = await dbContext.CreditNotes.AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == creditNoteId, cancellationToken);

        if (note is null)
        {
            return null;
        }

        var invoice = await (
            from item in dbContext.Invoices.AsNoTracking()
            join customer in dbContext.Customers.AsNoTracking() on item.CustomerId equals customer.Id
            where item.Id == note.InvoiceId
            select new { InvoiceNumber = item.Number ?? 0, customer.Name })
            .SingleAsync(cancellationToken);
        string? cashAccountName = note.CashAccountId is Guid cashAccountId
            ? await dbContext.LedgerAccounts.AsNoTracking()
                .Where(account => account.Id == cashAccountId)
                .Select(account => account.Name)
                .SingleAsync(cancellationToken)
            : null;
        string createdByName = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == note.CreatedByUserId)
            .Select(user => user.FullName)
            .SingleAsync(cancellationToken);
        Dictionary<Guid, InvoiceLineModelData> invoiceLines = await (
            from line in dbContext.Set<InvoiceLine>().AsNoTracking()
            join creditLine in dbContext.Set<CreditNoteLine>().AsNoTracking()
                on line.Id equals creditLine.InvoiceLineId
            where creditLine.CreditNoteId == creditNoteId
            select new InvoiceLineModelData(
                line.Id,
                line.Position,
                line.Description,
                line.UnitOfMeasure,
                line.Quantity))
            .ToDictionaryAsync(line => line.Id, cancellationToken);

        return new CreditNoteModel(
            note.Id,
            note.Code,
            note.InvoiceId,
            invoice.InvoiceNumber,
            invoice.Name,
            note.IssueDate,
            note.Reason,
            note.NetAmount,
            note.TaxAmount,
            note.TotalAmount,
            note.AppliedToBalance,
            note.RefundedAmount,
            note.WithholdingReversedAmount,
            cashAccountName,
            note.RefundReference,
            note.ElectronicCreditNoteKey,
            note.ElectronicCreditNoteNumber,
            note.Status,
            note.CreatedAtUtc,
            createdByName,
            note.Lines
                .Select(line =>
                {
                    InvoiceLineModelData source = invoiceLines[line.InvoiceLineId];
                    return new CreditNoteLineModel(
                        line.InvoiceLineId,
                        source.Position,
                        source.Description,
                        source.UnitOfMeasure,
                        source.Quantity,
                        line.NetAmount,
                        line.TaxRate,
                        line.TaxAmount,
                        line.TotalAmount);
                })
                .OrderBy(line => line.Position)
                .ToArray(),
            CatalogPersistence.EncodeVersion(note.RowVersion));
    }

    public async Task<CreditNoteRegistrationResult> RegisterAsync(
        RegisterCreditNoteCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Invoice? invoice = await dbContext.Invoices
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == command.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            return Rejected(CatalogOperationResult.Missing());
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, invoice, command.InvoiceVersion))
        {
            return Rejected(CatalogOperationResult.Invalid("El token de concurrencia no es válido."));
        }

        if (invoice.Status != InvoiceStatus.Confirmed)
        {
            return Rejected(CatalogOperationResult.Invalid("Solo se puede emitir una nota sobre una factura confirmada."));
        }

        if (invoice.Lines.Count == 0)
        {
            return Rejected(CatalogOperationResult.Invalid("La factura no tiene líneas para acreditar."));
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.IssueDate, timeProvider, cancellationToken);
        if (closingDateViolation is not null)
        {
            return Rejected(CatalogOperationResult.Invalid(closingDateViolation));
        }

        SystemParameters? parameters = await dbContext.SystemParameters.AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(parameters?.CompanyLegalId)
            || string.IsNullOrWhiteSpace(parameters.CompanyBranchCode)
            || string.IsNullOrWhiteSpace(parameters.CompanyTerminalCode))
        {
            return Rejected(CatalogOperationResult.Invalid(
                "Configure la cédula jurídica, la sucursal y la terminal de la empresa en Parámetros "
                + "antes de registrar notas de crédito."));
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        decimal appliedToBalance = 0m;
        decimal refundedAmount = 0m;
        decimal withholdingReversedAmount = 0m;
        Receivable? receivable = null;
        Income? cashInvoiceIncome = null;

        if (invoice.PaymentTerm == PaymentTerm.Cash)
        {
            refundedAmount = invoice.TotalAmount;
            cashInvoiceIncome = await dbContext.Incomes.SingleOrDefaultAsync(
                income => income.InvoiceId == invoice.Id,
                cancellationToken);
            if (cashInvoiceIncome is null
                || cashInvoiceIncome.Status != IncomeStatus.Active
                || cashInvoiceIncome.Amount != invoice.TotalAmount)
            {
                return Rejected(CatalogOperationResult.Invalid(
                    "No se puede registrar la nota porque el ingreso de contado asociado no está vigente o no coincide con el total de la factura."));
            }
        }
        else
        {
            receivable = await dbContext.Receivables.SingleOrDefaultAsync(
                item => item.InvoiceId == invoice.Id,
                cancellationToken);
            if (receivable is null || receivable.Status == ReceivableStatus.Cancelled)
            {
                return Rejected(CatalogOperationResult.Invalid(
                    "No se encontró una cuenta por cobrar vigente para la factura."));
            }

            bool hasAdjustments = await dbContext.BalanceAdjustments.AsNoTracking()
                .AnyAsync(adjustment => adjustment.ReceivableId == receivable.Id, cancellationToken);
            if (hasAdjustments)
            {
                return Rejected(CatalogOperationResult.Invalid(
                    "No se puede registrar la nota porque la cuenta por cobrar tiene historial de ajustes de saldo. Verifique y concilie esos movimientos antes de emitirla."));
            }

            ReceivablePayment[] activePayments = await dbContext.ReceivablePayments
                .Where(payment => payment.ReceivableId == receivable.Id
                    && payment.Status == ReceivablePaymentStatus.Active)
                .ToArrayAsync(cancellationToken);
            Guid[] activePaymentIds = activePayments.Select(payment => payment.Id).ToArray();
            withholdingReversedAmount = activePaymentIds.Length == 0
                ? 0m
                : await dbContext.Withholdings
                    .Where(withholding => activePaymentIds.Contains(withholding.ReceivablePaymentId))
                    .SumAsync(withholding => (decimal?)withholding.Amount, cancellationToken) ?? 0m;
            decimal cashReceived = activePayments.Sum(payment => payment.Amount);
            appliedToBalance = receivable.Balance;
            decimal reconciledTotal = decimal.Round(
                appliedToBalance + cashReceived + withholdingReversedAmount,
                2,
                MidpointRounding.AwayFromZero);
            if (reconciledTotal != invoice.TotalAmount)
            {
                return Rejected(CatalogOperationResult.Invalid(
                    "El saldo, los abonos y las retenciones vigentes no coinciden con el total de la factura; revise los movimientos antes de anularla."));
            }

            refundedAmount = cashReceived;
        }

        if (refundedAmount > 0m)
        {
            if (command.CashAccountId is not Guid cashAccountId
                || string.IsNullOrWhiteSpace(command.RefundReference))
            {
                return Rejected(CatalogOperationResult.Invalid(
                    "La cuenta de efectivo y la referencia son obligatorias para registrar la devolución."));
            }

            bool isValidCashAccount = await dbContext.LedgerAccounts.AsNoTracking()
                .AnyAsync(account => account.Id == cashAccountId
                    && account.IsActive
                    && account.IsCash,
                    cancellationToken);
            if (!isValidCashAccount)
            {
                return Rejected(CatalogOperationResult.Invalid(
                    "La cuenta seleccionada no existe, está inactiva o no es de efectivo."));
            }

            if (!actorIsManagement)
            {
                decimal limit = parameters.AuthorizationLimitCrc;
                if (refundedAmount > limit)
                {
                    return Rejected(CatalogOperationResult.Invalid(
                        "La devolución supera el límite de autorización; solo Gerencia puede registrar esta nota de crédito."));
                }
            }
        }
        else if (command.CashAccountId is not null || !string.IsNullOrWhiteSpace(command.RefundReference))
        {
            return Rejected(CatalogOperationResult.Invalid(
                "No indique cuenta de efectivo ni referencia si la nota no requiere devolución."));
        }

        List<CreditNoteLine> lines;
        try
        {
            lines = invoice.Lines
                .OrderBy(line => line.Position)
                .Select(line => new CreditNoteLine(
                    Guid.NewGuid(),
                    line.Id,
                    line.NetAmount,
                    line.Tax.Rate,
                    line.Tax.Amount))
                .ToList();
        }
        catch (ArgumentException exception)
        {
            return Rejected(CatalogOperationResult.Invalid(exception.Message));
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_NotaCreditoCodigo",
                "NCR",
                6,
                cancellationToken);

            long claveSequential = await CatalogPersistence.GetNextSequenceValueAsync(
                dbContext,
                "finanzas.Seq_NotaCreditoClaveConsecutivo",
                cancellationToken);

            string consecutivo = ElectronicDocumentKeyBuilder.BuildConsecutivo(
                ElectronicDocumentKeyBuilder.CreditNoteDocumentTypeCode,
                parameters.CompanyBranchCode,
                parameters.CompanyTerminalCode,
                claveSequential);

            string issuerId = ElectronicDocumentKeyBuilder.PadIssuerId(parameters.CompanyLegalId);
            string electronicCreditNoteKey = ElectronicDocumentKeyBuilder.BuildKey(
                command.IssueDate, issuerId, consecutivo, ElectronicDocumentKeyBuilder.GenerateSecurityCode());

            CreditNote creditNote = new(
                Guid.NewGuid(),
                code,
                invoice.Id,
                command.IssueDate,
                command.Reason,
                appliedToBalance,
                refundedAmount,
                withholdingReversedAmount,
                refundedAmount > 0m ? command.CashAccountId : null,
                refundedAmount > 0m ? command.RefundReference : null,
                electronicCreditNoteKey,
                consecutivo,
                lines,
                now,
                actorId);

            invoice.Cancel(command.Reason, now, actorId);
            if (receivable is not null)
            {
                receivable.CancelByCreditNote(appliedToBalance, now, actorId);
            }

            cashInvoiceIncome?.Cancel(command.Reason, now, actorId);

            dbContext.Add(creditNote);
            await JournalPostingService.PostCreditNoteAsync(dbContext, creditNote, now, actorId, cancellationToken);
            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.CreditNote,
                creditNote.Id,
                OperationLogAction.Confirmed,
                reason: command.Reason,
                now,
                actorId));
            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Invoice,
                invoice.Id,
                OperationLogAction.Cancelled,
                reason: command.Reason,
                now,
                actorId));

            bool duplicateReferenceWarning = refundedAmount > 0m
                && await DuplicateReferenceGuard.ExistsAsync(
                    dbContext,
                    command.CashAccountId!.Value,
                    command.RefundReference!,
                    cancellationToken);

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "La factura ya tiene una nota de crédito registrada.",
                cancellationToken);
            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new CreditNoteRegistrationResult(
                result,
                result.IsSuccess ? creditNote.Id : null,
                duplicateReferenceWarning);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Rejected(CatalogOperationResult.Invalid(exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Rejected(CatalogOperationResult.Invalid(exception.Message));
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static CreditNoteRegistrationResult Rejected(CatalogOperationResult operation) =>
        new(operation, null);

    private sealed record InvoiceLineModelData(
        Guid Id,
        int Position,
        string Description,
        string UnitOfMeasure,
        decimal Quantity);
}
