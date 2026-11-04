using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Receivables;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Common;
using SistemaFinanciero.Infrastructure.Ledger;
using SistemaFinanciero.Infrastructure.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Receivables;

/// <summary>
/// Registra y anula abonos, incluido el reparto a varias facturas con un <c>GroupId</c> común
/// (regla de negocio 2).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IReceivablePaymentService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>. Cada fila de <c>Abonos</c> genera su propia
/// <c>Retenciones</c> (0 o 1) e <c>Ingresos</c> (siempre), todo en la misma transacción que la fila
/// del abono y la entrada en la bitácora de operaciones (<see cref="OperationLogEntry"/>, regla 4).
/// </remarks>
internal sealed class ReceivablePaymentService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IReceivablePaymentService
{
    public async Task<ReceivablePaymentModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        ReceivablePayment? payment = await dbContext.Set<ReceivablePayment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        decimal? withholdingAmount = await GetWithholdingAmountAsync(payment.Id, cancellationToken);
        string cashAccountName = await GetCashAccountNameAsync(payment.CashAccountId, cancellationToken);
        return Map(payment, withholdingAmount, cashAccountName);
    }

    public async Task<IReadOnlyList<ReceivablePaymentModel>> SearchByReceivableAsync(
        Guid receivableId,
        CancellationToken cancellationToken = default)
    {
        List<ReceivablePayment> payments = await dbContext.Set<ReceivablePayment>()
            .AsNoTracking()
            .Where(payment => payment.ReceivableId == receivableId)
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return [];
        }

        Guid[] paymentIds = payments.Select(payment => payment.Id).ToArray();
        Dictionary<Guid, decimal> withholdingAmounts = await dbContext.Set<Withholding>()
            .AsNoTracking()
            .Where(withholding => paymentIds.Contains(withholding.ReceivablePaymentId))
            .ToDictionaryAsync(withholding => withholding.ReceivablePaymentId, withholding => withholding.Amount, cancellationToken);

        Guid[] cashAccountIds = payments.Select(payment => payment.CashAccountId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> cashAccountNames = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => cashAccountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return payments
            .Select(payment => Map(
                payment,
                withholdingAmounts.TryGetValue(payment.Id, out decimal amount) ? amount : null,
                cashAccountNames.GetValueOrDefault(payment.CashAccountId, "—")))
            .ToArray();
    }

    public async Task<RegisterReceivablePaymentResult> RegisterAsync(
        RegisterReceivablePaymentCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Allocations.Count == 0)
        {
            return Rejected("Debe indicar al menos una factura para aplicar el abono.");
        }

        Guid[] receivableIds = command.Allocations.Select(allocation => allocation.ReceivableId).ToArray();

        if (receivableIds.Distinct().Count() != receivableIds.Length)
        {
            return Rejected("No puede repetir la misma factura en un mismo abono.");
        }

        bool isValidCashAccount = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == command.CashAccountId && account.IsActive && account.CashKind != null,
                cancellationToken);

        if (!isValidCashAccount)
        {
            return Rejected("La cuenta de efectivo seleccionada no existe, está inactiva o no es de efectivo.");
        }

        List<Receivable> receivables = await dbContext.Set<Receivable>()
            .Where(receivable => receivableIds.Contains(receivable.Id))
            .ToListAsync(cancellationToken);

        if (receivables.Count != receivableIds.Length)
        {
            return new RegisterReceivablePaymentResult(CatalogOperationResult.Missing(), [], false);
        }

        if (receivables.Any(receivable => receivable.CustomerId != command.CustomerId))
        {
            return Rejected("Todas las facturas del abono deben pertenecer al cliente indicado.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.PaymentDate, cancellationToken);

        if (closingDateViolation is not null)
        {
            return Rejected(closingDateViolation);
        }

        (RegisterReceivablePaymentResult? typeError, Dictionary<Guid, WithholdingType> withholdingTypes) =
            await ResolveWithholdingTypesAsync(command, receivables, cancellationToken);

        if (typeError is not null)
        {
            return typeError;
        }

        DateTimeOffset preparedAtUtc = timeProvider.GetUtcNow();
        List<PreparedAllocation> prepared;
        decimal total;

        try
        {
            (prepared, total) = await PrepareAllocationsAsync(
                command, receivables, withholdingTypes, actorId, preparedAtUtc, cancellationToken);
        }
        catch (ArgumentException exception)
        {
            return Rejected(exception.Message);
        }

        if (!actorIsManagement)
        {
            SystemParameters? parameters = await dbContext.Set<SystemParameters>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            // Sin parámetros configurados no hay límite que aplicar de forma segura: se exige
            // Gerencia hasta que Gerencia los configure (regla 7).
            decimal limit = parameters?.AuthorizationLimitCrc ?? 0m;

            if (total > limit)
            {
                return Rejected(
                    "El total del abono supera el límite de autorización; solo Gerencia puede registrarlo.");
            }
        }

        bool duplicateReferenceWarning = await DuplicateReferenceGuard.ExistsAsync(
            dbContext,
            command.CashAccountId,
            command.Reference,
            cancellationToken);

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            List<Guid> createdIds = [];

            foreach (PreparedAllocation allocation in prepared)
            {
                allocation.Receivable.ApplyPayment(allocation.Total, preparedAtUtc, actorId);

                dbContext.Add(allocation.Payment);
                createdIds.Add(allocation.Payment.Id);

                if (allocation.Withholding is not null)
                {
                    dbContext.Add(allocation.Withholding);
                }

                string incomeCode = await CatalogPersistence.GenerateSequentialCodeAsync(
                    dbContext,
                    "finanzas.Seq_IngresoCodigo",
                    "REC",
                    6,
                    cancellationToken);

                dbContext.Add(Income.FromReceivablePayment(
                    Guid.NewGuid(),
                    incomeCode,
                    allocation.Payment.Id,
                    allocation.IncomeCategoryId,
                    command.CashAccountId,
                    allocation.Total,
                    command.PaymentDate,
                    command.Reference,
                    preparedAtUtc,
                    actorId));

                await JournalPostingService.PostReceivablePaymentAsync(
                    dbContext,
                    allocation.Payment,
                    allocation.Withholding?.Amount ?? 0m,
                    preparedAtUtc,
                    actorId,
                    cancellationToken);

                dbContext.Add(new OperationLogEntry(
                    Guid.NewGuid(),
                    OperationLogEntityType.ReceivablePayment,
                    allocation.Payment.Id,
                    OperationLogAction.Confirmed,
                    reason: null,
                    preparedAtUtc,
                    actorId));
            }

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible registrar el abono.",
                cancellationToken);

            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new RegisterReceivablePaymentResult(
                result,
                result.IsSuccess ? createdIds : [],
                duplicateReferenceWarning);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Rejected(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return Rejected(exception.Message);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ReceivablePayment? payment = await dbContext.Set<ReceivablePayment>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, payment, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, payment.PaymentDate, cancellationToken);

        if (closingDateViolation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(closingDateViolation);
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            payment.Cancel(reason, now, actorId);

            Receivable receivable = await dbContext.Set<Receivable>()
                .SingleAsync(candidate => candidate.Id == payment.ReceivableId, cancellationToken);
            decimal? withholdingAmount = await GetWithholdingAmountAsync(payment.Id, cancellationToken);
            receivable.RevertPayment(payment.Amount + (withholdingAmount ?? 0m), now, actorId);

            Income income = await dbContext.Set<Income>()
                .SingleAsync(candidate => candidate.ReceivablePaymentId == payment.Id, cancellationToken);
            income.Cancel(reason, now, actorId);

            await JournalPostingService.ReverseAsync(
                dbContext, JournalEntrySourceType.ReceivablePayment, payment.Id, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.ReceivablePayment,
                payment.Id,
                OperationLogAction.Cancelled,
                reason,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular el abono.",
                cancellationToken);

            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return result;
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Carga y valida cada tipo de retención indicado en el abono. El cliente debe tener la marca de
    /// retención del sector público y, cuando el tipo tiene base mínima, el neto de la factura debe
    /// superarla; en cualquier otro caso se rechaza, aunque se haya enviado la retención (RF-95,
    /// bloque 1 del paquete v7). También valida que la base enviada no supere el neto de esa factura
    /// que todavía no tiene retención aplicada.
    /// </summary>
    private async Task<(RegisterReceivablePaymentResult? Error, Dictionary<Guid, WithholdingType> Types)>
        ResolveWithholdingTypesAsync(
            RegisterReceivablePaymentCommand command,
            List<Receivable> receivables,
            CancellationToken cancellationToken)
    {
        Dictionary<Guid, WithholdingType> withholdingTypes = [];
        List<ReceivablePaymentAllocationCommand> allocationsWithWithholding = command.Allocations
            .Where(allocation => allocation.Withholding is not null)
            .ToList();

        if (allocationsWithWithholding.Count == 0)
        {
            return (null, withholdingTypes);
        }

        bool appliesStateWithholding = await dbContext.Set<Customer>()
            .AsNoTracking()
            .Where(customer => customer.Id == command.CustomerId)
            .Select(customer => customer.AppliesStateWithholding)
            .SingleAsync(cancellationToken);

        if (!appliesStateWithholding)
        {
            return (
                Rejected("Este cliente no está marcado para retener; no se puede registrar una retención."),
                withholdingTypes);
        }

        Guid[] invoiceIds = receivables.Select(receivable => receivable.InvoiceId).Distinct().ToArray();
        Dictionary<Guid, decimal> netAmountsByInvoiceId = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoiceIds.Contains(invoice.Id))
            .ToDictionaryAsync(invoice => invoice.Id, invoice => invoice.NetAmount, cancellationToken);

        foreach (ReceivablePaymentAllocationCommand allocation in allocationsWithWithholding)
        {
            WithholdingInputCommand withholdingInput = allocation.Withholding!;

            if (!withholdingTypes.TryGetValue(withholdingInput.WithholdingTypeId, out WithholdingType? type))
            {
                type = await dbContext.Set<WithholdingType>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(
                        candidate => candidate.Id == withholdingInput.WithholdingTypeId,
                        cancellationToken);

                if (type is null || !type.IsActive)
                {
                    return (Rejected("El tipo de retención seleccionado no existe o está inactivo."), withholdingTypes);
                }

                withholdingTypes[withholdingInput.WithholdingTypeId] = type;
            }

            Receivable receivable = receivables.Single(candidate => candidate.Id == allocation.ReceivableId);
            decimal netAmount = netAmountsByInvoiceId[receivable.InvoiceId];

            if (type.MinimumBase is decimal minimumBase && netAmount <= minimumBase)
            {
                return (
                    Rejected("La retención no aplica a esta factura: no supera la base mínima del tipo."),
                    withholdingTypes);
            }

            decimal alreadyWithheld = await dbContext.Set<Withholding>()
                .AsNoTracking()
                .Where(withholding => dbContext.Set<ReceivablePayment>()
                    .Any(payment => payment.Id == withholding.ReceivablePaymentId
                        && payment.ReceivableId == allocation.ReceivableId))
                .SumAsync(withholding => withholding.Base, cancellationToken);

            if (withholdingInput.Base > netAmount - alreadyWithheld)
            {
                return (
                    Rejected("La base de la retención no puede superar el neto de la factura que aún no tiene retención."),
                    withholdingTypes);
            }
        }

        return (null, withholdingTypes);
    }

    private async Task<(List<PreparedAllocation> Prepared, decimal Total)> PrepareAllocationsAsync(
        RegisterReceivablePaymentCommand command,
        List<Receivable> receivables,
        Dictionary<Guid, WithholdingType> withholdingTypes,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Guid groupId = Guid.NewGuid();
        List<PreparedAllocation> prepared = [];
        decimal total = 0m;

        // La categoría de ingreso pertenece a cada factura, no al abono (bloque 2 del paquete de
        // mejoras finales): un mismo abono a varias facturas puede heredar categorías distintas.
        Guid[] invoiceIds = receivables.Select(receivable => receivable.InvoiceId).Distinct().ToArray();
        Dictionary<Guid, Guid> incomeCategoryIdsByInvoiceId = await dbContext.Set<Invoice>()
            .AsNoTracking()
            .Where(invoice => invoiceIds.Contains(invoice.Id))
            .ToDictionaryAsync(invoice => invoice.Id, invoice => invoice.IncomeCategoryId, cancellationToken);

        foreach (ReceivablePaymentAllocationCommand allocation in command.Allocations)
        {
            Receivable receivable = receivables.Single(candidate => candidate.Id == allocation.ReceivableId);
            Guid incomeCategoryId = incomeCategoryIdsByInvoiceId[receivable.InvoiceId];
            Guid paymentId = Guid.NewGuid();

            Withholding? withholding = null;

            if (allocation.Withholding is { } withholdingInput)
            {
                WithholdingType type = withholdingTypes[withholdingInput.WithholdingTypeId];
                withholding = new Withholding(
                    Guid.NewGuid(),
                    paymentId,
                    type.Id,
                    type.Code,
                    type.Name,
                    type.Rate,
                    withholdingInput.Base,
                    now,
                    actorId);
            }

            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_AbonoCodigo",
                "ABO",
                6,
                cancellationToken);

            ReceivablePayment payment = new(
                paymentId,
                code,
                receivable.Id,
                groupId,
                allocation.Amount,
                command.CashAccountId,
                command.Reference,
                command.PaymentDate,
                now,
                actorId);

            decimal allocationTotal = payment.Amount + (withholding?.Amount ?? 0m);
            total += allocationTotal;
            prepared.Add(new PreparedAllocation(receivable, payment, withholding, allocationTotal, incomeCategoryId));
        }

        return (prepared, total);
    }

    private async Task<decimal?> GetWithholdingAmountAsync(Guid receivablePaymentId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<Withholding>()
            .AsNoTracking()
            .Where(withholding => withholding.ReceivablePaymentId == receivablePaymentId)
            .Select(withholding => (decimal?)withholding.Amount)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<string> GetCashAccountNameAsync(Guid cashAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Id == cashAccountId)
            .Select(account => account.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "—";
    }

    private static RegisterReceivablePaymentResult Rejected(string message) =>
        new(CatalogOperationResult.Invalid(message), [], false);

    private static ReceivablePaymentModel Map(ReceivablePayment payment, decimal? withholdingAmount, string cashAccountName)
    {
        return new ReceivablePaymentModel(
            payment.Id,
            payment.Code,
            payment.ReceivableId,
            payment.GroupId,
            payment.Amount,
            withholdingAmount,
            payment.CashAccountId,
            cashAccountName,
            payment.Reference,
            payment.PaymentDate,
            payment.Status,
            payment.CancellationReason,
            CatalogPersistence.EncodeVersion(payment.RowVersion));
    }

    private sealed record PreparedAllocation(
        Receivable Receivable,
        ReceivablePayment Payment,
        Withholding? Withholding,
        decimal Total,
        Guid IncomeCategoryId);
}
