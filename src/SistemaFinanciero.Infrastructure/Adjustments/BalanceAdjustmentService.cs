using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SistemaFinanciero.Application.Adjustments;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Adjustments;

/// <summary>
/// Registra ajustes manuales de saldo sobre cuentas por cobrar y por pagar (regla de negocio 4).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IBalanceAdjustmentService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>. Cada ajuste queda además en la bitácora de
/// operaciones (<see cref="OperationLogEntry"/>, regla 4/16), dentro de la misma transacción que la
/// mutación del saldo.
/// </remarks>
internal sealed class BalanceAdjustmentService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IBalanceAdjustmentService
{
    public async Task<IReadOnlyList<BalanceAdjustmentModel>> SearchByReceivableAsync(
        Guid receivableId,
        CancellationToken cancellationToken = default)
    {
        List<BalanceAdjustment> adjustments = await dbContext.Set<BalanceAdjustment>()
            .AsNoTracking()
            .Where(adjustment => adjustment.ReceivableId == receivableId)
            .OrderByDescending(adjustment => adjustment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await MapAsync(adjustments, cancellationToken);
    }

    public async Task<IReadOnlyList<BalanceAdjustmentModel>> SearchByPayableAsync(
        Guid payableId,
        CancellationToken cancellationToken = default)
    {
        List<BalanceAdjustment> adjustments = await dbContext.Set<BalanceAdjustment>()
            .AsNoTracking()
            .Where(adjustment => adjustment.PayableId == payableId)
            .OrderByDescending(adjustment => adjustment.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return await MapAsync(adjustments, cancellationToken);
    }

    public async Task<CatalogOperationResult> AdjustReceivableAsync(
        Guid receivableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        Receivable? receivable = await dbContext.Set<Receivable>()
            .SingleOrDefaultAsync(candidate => candidate.Id == receivableId, cancellationToken);

        if (receivable is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, receivable, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            receivable.Adjust(amount, direction, now, actorId);

            dbContext.Add(BalanceAdjustment.ForReceivable(
                Guid.NewGuid(), receivableId, amount, direction, reason, now, actorId));
            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Receivable,
                receivableId,
                OperationLogAction.Adjusted,
                reason,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible ajustar el saldo.",
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

    public async Task<CatalogOperationResult> AdjustPayableAsync(
        Guid payableId,
        decimal amount,
        BalanceAdjustmentDirection direction,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        Payable? payable = await dbContext.Set<Payable>()
            .SingleOrDefaultAsync(candidate => candidate.Id == payableId, cancellationToken);

        if (payable is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, payable, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        IDbContextTransaction? transaction = await CatalogPersistence.BeginTransactionIfNeededAsync(
            dbContext,
            cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            payable.Adjust(amount, direction, now, actorId);

            dbContext.Add(BalanceAdjustment.ForPayable(
                Guid.NewGuid(), payableId, amount, direction, reason, now, actorId));
            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.Payable,
                payableId,
                OperationLogAction.Adjusted,
                reason,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible ajustar el saldo.",
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

    private async Task<IReadOnlyList<BalanceAdjustmentModel>> MapAsync(
        List<BalanceAdjustment> adjustments,
        CancellationToken cancellationToken)
    {
        if (adjustments.Count == 0)
        {
            return [];
        }

        Guid[] userIds = adjustments.Select(adjustment => adjustment.CreatedByUserId).Distinct().ToArray();
        Dictionary<Guid, string> userNames = await dbContext.Users
            .AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .ToDictionaryAsync(user => user.Id, user => user.FullName, cancellationToken);

        return adjustments
            .Select(adjustment => new BalanceAdjustmentModel(
                adjustment.Id,
                adjustment.Amount,
                adjustment.Direction,
                adjustment.Reason,
                adjustment.CreatedAtUtc,
                userNames.GetValueOrDefault(adjustment.CreatedByUserId, "—")))
            .ToArray();
    }
}
