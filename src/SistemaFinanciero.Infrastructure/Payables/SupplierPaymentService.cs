using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Payables;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Payables;

/// <summary>
/// Registra y anula pagos a proveedores, incluido el reparto a varias cuentas por pagar con un
/// <c>GroupId</c> común (regla de negocio 2). Espejo de <c>ReceivablePaymentService</c>, del lado de
/// ingresos, sin retención ni generación de ingresos.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>ISupplierPaymentService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>. Cada fila de <c>Pagos</c> queda en la bitácora de
/// operaciones (<see cref="OperationLogEntry"/>, regla 4), en la misma transacción que la fila del
/// pago y la actualización de su cuenta por pagar.
/// </remarks>
internal sealed class SupplierPaymentService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ISupplierPaymentService
{
    public async Task<SupplierPaymentModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        SupplierPayment? payment = await dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return null;
        }

        string cashAccountName = await GetCashAccountNameAsync(payment.CashAccountId, cancellationToken);
        return Map(payment, cashAccountName);
    }

    public async Task<IReadOnlyList<SupplierPaymentModel>> SearchByPayableAsync(
        Guid payableId,
        CancellationToken cancellationToken = default)
    {
        List<SupplierPayment> payments = await dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .Where(payment => payment.PayableId == payableId)
            .OrderByDescending(payment => payment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (payments.Count == 0)
        {
            return [];
        }

        Guid[] cashAccountIds = payments.Select(payment => payment.CashAccountId).Distinct().ToArray();
        IReadOnlyDictionary<Guid, string> cashAccountNames = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => cashAccountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return payments
            .Select(payment => Map(payment, cashAccountNames.GetValueOrDefault(payment.CashAccountId, "—")))
            .ToArray();
    }

    public async Task<RegisterSupplierPaymentResult> RegisterAsync(
        RegisterSupplierPaymentCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Allocations.Count == 0)
        {
            return Rejected("Debe indicar al menos una cuenta por pagar para aplicar el pago.");
        }

        Guid[] payableIds = command.Allocations.Select(allocation => allocation.PayableId).ToArray();

        if (payableIds.Distinct().Count() != payableIds.Length)
        {
            return Rejected("No puede repetir la misma cuenta por pagar en un mismo pago.");
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

        List<Payable> payables = await dbContext.Set<Payable>()
            .Where(payable => payableIds.Contains(payable.Id))
            .ToListAsync(cancellationToken);

        if (payables.Count != payableIds.Length)
        {
            return new RegisterSupplierPaymentResult(CatalogOperationResult.Missing(), [], false);
        }

        if (payables.Any(payable => payable.SupplierId != command.SupplierId))
        {
            return Rejected("Todas las cuentas por pagar del pago deben pertenecer al proveedor indicado.");
        }

        DateTimeOffset preparedAtUtc = timeProvider.GetUtcNow();
        (List<PreparedAllocation> prepared, decimal total) = PrepareAllocations(
            command,
            payables,
            actorId,
            preparedAtUtc);

        if (!actorIsManagement)
        {
            SystemParameters? parameters = await dbContext.Set<SystemParameters>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            // Sin parámetros configurados no hay límite que aplicar de forma segura: se exige
            // Gerencia hasta que Gerencia los configure (regla 7), igual criterio que en abonos.
            decimal limit = parameters?.AuthorizationLimitCrc ?? 0m;

            if (total > limit)
            {
                return Rejected(
                    "El total del pago supera el límite de autorización; solo Gerencia puede registrarlo.");
            }
        }

        bool duplicateReferenceWarning = await HasDuplicateReferenceAsync(
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
                allocation.Payable.ApplyPayment(allocation.Payment.Amount, preparedAtUtc, actorId);

                dbContext.Add(allocation.Payment);
                createdIds.Add(allocation.Payment.Id);

                dbContext.Add(new OperationLogEntry(
                    Guid.NewGuid(),
                    OperationLogEntityType.SupplierPayment,
                    allocation.Payment.Id,
                    OperationLogAction.Confirmed,
                    reason: null,
                    preparedAtUtc,
                    actorId));
            }

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible registrar el pago.",
                cancellationToken);

            if (result.IsSuccess && transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return new RegisterSupplierPaymentResult(
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
        SupplierPayment? payment = await dbContext.Set<SupplierPayment>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (payment is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, payment, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            payment.Cancel(reason, now, actorId);

            Payable payable = await dbContext.Set<Payable>()
                .SingleAsync(candidate => candidate.Id == payment.PayableId, cancellationToken);
            payable.RevertPayment(payment.Amount, now, actorId);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.SupplierPayment,
                payment.Id,
                OperationLogAction.Cancelled,
                reason,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular el pago.",
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

    private static (List<PreparedAllocation> Prepared, decimal Total) PrepareAllocations(
        RegisterSupplierPaymentCommand command,
        List<Payable> payables,
        Guid actorId,
        DateTimeOffset now)
    {
        Guid groupId = Guid.NewGuid();
        List<PreparedAllocation> prepared = [];
        decimal total = 0m;

        foreach (SupplierPaymentAllocationCommand allocation in command.Allocations)
        {
            Payable payable = payables.Single(candidate => candidate.Id == allocation.PayableId);

            SupplierPayment payment = new(
                Guid.NewGuid(),
                payable.Id,
                groupId,
                allocation.Amount,
                command.CashAccountId,
                command.Reference,
                command.PaymentDate,
                now,
                actorId);

            total += payment.Amount;
            prepared.Add(new PreparedAllocation(payable, payment));
        }

        return (prepared, total);
    }

    private async Task<bool> HasDuplicateReferenceAsync(
        Guid cashAccountId,
        string reference,
        CancellationToken cancellationToken)
    {
        bool existsInPayments = await dbContext.Set<SupplierPayment>()
            .AsNoTracking()
            .AnyAsync(
                payment => payment.CashAccountId == cashAccountId && payment.Reference == reference,
                cancellationToken);

        if (existsInPayments)
        {
            return true;
        }

        bool existsInReceivablePayments = await dbContext.Set<ReceivablePayment>()
            .AsNoTracking()
            .AnyAsync(
                payment => payment.CashAccountId == cashAccountId && payment.Reference == reference,
                cancellationToken);

        if (existsInReceivablePayments)
        {
            return true;
        }

        return await dbContext.Set<Income>()
            .AsNoTracking()
            .AnyAsync(
                income => income.CashAccountId == cashAccountId && income.Reference == reference,
                cancellationToken);
    }

    private async Task<string> GetCashAccountNameAsync(Guid cashAccountId, CancellationToken cancellationToken)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Id == cashAccountId)
            .Select(account => account.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "—";
    }

    private static RegisterSupplierPaymentResult Rejected(string message) =>
        new(CatalogOperationResult.Invalid(message), [], false);

    private static SupplierPaymentModel Map(SupplierPayment payment, string cashAccountName)
    {
        return new SupplierPaymentModel(
            payment.Id,
            payment.PayableId,
            payment.GroupId,
            payment.Amount,
            payment.CashAccountId,
            cashAccountName,
            payment.Reference,
            payment.PaymentDate,
            payment.Status,
            payment.CancellationReason,
            CatalogPersistence.EncodeVersion(payment.RowVersion));
    }

    private sealed record PreparedAllocation(Payable Payable, SupplierPayment Payment);
}
