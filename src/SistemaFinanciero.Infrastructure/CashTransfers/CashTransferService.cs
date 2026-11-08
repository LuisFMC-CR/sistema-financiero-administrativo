using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.CashTransfers;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Common;
using SistemaFinanciero.Infrastructure.Ledger;
using SistemaFinanciero.Infrastructure.Parameters;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.CashTransfers;

/// <summary>Registra, consulta y anula traspasos entre cuentas de efectivo propias (paquete v8, bloque 1).</summary>
/// <remarks>
/// Conexiones: implementa <c>ICashTransferService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo consume <c>CashTransfersController</c> (Web, ruta
/// <c>/cuentas-contables/traspasos</c>). Cada registro y anulación queda en la bitácora de operaciones
/// (<see cref="OperationLogEntry"/>, regla 4), en la misma transacción implícita de
/// <c>SaveChangesAsync</c> que la fila del traspaso y su asiento contable. Espejo de
/// <c>SupplierPaymentService</c>, sin agrupación: un traspaso siempre es una sola fila.
/// </remarks>
internal sealed class CashTransferService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ICashTransferService
{
    public async Task<CashTransferModel?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        CashTransfer? transfer = await dbContext.Set<CashTransfer>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (transfer is null)
        {
            return null;
        }

        IReadOnlyList<CashTransferModel> mapped = await MapAllAsync([transfer], cancellationToken);
        return mapped[0];
    }

    public async Task<PagedResult<CashTransferModel>> SearchAsync(
        CashTransferQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<CashTransfer> transfers = dbContext.Set<CashTransfer>().AsNoTracking();

        transfers = query.Status switch
        {
            CashTransferStatusFilter.Active => transfers.Where(transfer => transfer.Status == CashTransferStatus.Active),
            CashTransferStatusFilter.Cancelled => transfers.Where(transfer => transfer.Status == CashTransferStatus.Cancelled),
            CashTransferStatusFilter.All => transfers,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Code is not null)
        {
            string code = query.Code;
            transfers = transfers.Where(transfer => transfer.Code.Contains(code));
        }

        if (query.CashAccountId is Guid cashAccountId)
        {
            transfers = transfers.Where(transfer =>
                transfer.SourceCashAccountId == cashAccountId || transfer.DestinationCashAccountId == cashAccountId);
        }

        if (query.TransferDateFrom is DateOnly transferDateFrom)
        {
            transfers = transfers.Where(transfer => transfer.TransferDate >= transferDateFrom);
        }

        if (query.TransferDateTo is DateOnly transferDateTo)
        {
            transfers = transfers.Where(transfer => transfer.TransferDate <= transferDateTo);
        }

        PagedResult<CashTransfer> page = await CatalogPersistence.ToPageAsync(
            transfers.OrderByDescending(transfer => transfer.CreatedAtUtc),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<CashTransferModel>(
            await MapAllAsync(page.Items, cancellationToken),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<RegisterCashTransferResult> RegisterAsync(
        RegisterCashTransferCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.SourceCashAccountId == command.DestinationCashAccountId)
        {
            return Rejected("La cuenta de origen y la de destino deben ser distintas.");
        }

        bool isValidSourceAccount = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == command.SourceCashAccountId && account.IsActive && account.CashKind != null,
                cancellationToken);

        if (!isValidSourceAccount)
        {
            return Rejected("La cuenta de efectivo de origen no existe, está inactiva o no es de efectivo.");
        }

        bool isValidDestinationAccount = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == command.DestinationCashAccountId && account.IsActive && account.CashKind != null,
                cancellationToken);

        if (!isValidDestinationAccount)
        {
            return Rejected("La cuenta de efectivo de destino no existe, está inactiva o no es de efectivo.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, command.TransferDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            return Rejected(closingDateViolation);
        }

        if (!actorIsManagement)
        {
            SystemParameters? parameters = await dbContext.Set<SystemParameters>()
                .AsNoTracking()
                .SingleOrDefaultAsync(cancellationToken);

            // Sin parámetros configurados no hay límite que aplicar de forma segura: se exige
            // Gerencia hasta que Gerencia los configure (regla 7), igual criterio que en pagos y abonos.
            decimal limit = parameters?.AuthorizationLimitCrc ?? 0m;

            if (command.Amount > limit)
            {
                return Rejected(
                    "El monto del traspaso supera el límite de autorización; solo Gerencia puede registrarlo.");
            }
        }

        bool duplicateReferenceWarning = await DuplicateReferenceGuard.ExistsAsync(
            dbContext, command.SourceCashAccountId, command.Reference, cancellationToken)
            || await DuplicateReferenceGuard.ExistsAsync(
                dbContext, command.DestinationCashAccountId, command.Reference, cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            string code = await CatalogPersistence.GenerateSequentialCodeAsync(
                dbContext,
                "finanzas.Seq_TraspasoCodigo",
                "TRA",
                6,
                cancellationToken);

            CashTransfer transfer = new(
                Guid.NewGuid(),
                code,
                command.SourceCashAccountId,
                command.DestinationCashAccountId,
                command.Amount,
                command.Reference,
                command.Description,
                command.TransferDate,
                now,
                actorId);

            dbContext.Add(transfer);

            await JournalPostingService.PostCashTransferAsync(dbContext, transfer, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.CashTransfer,
                transfer.Id,
                OperationLogAction.Confirmed,
                reason: null,
                now,
                actorId));

            CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible registrar el traspaso.",
                cancellationToken);

            return new RegisterCashTransferResult(
                result,
                result.IsSuccess ? transfer.Id : null,
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
    }

    public async Task<CatalogOperationResult> CancelAsync(
        Guid id,
        string reason,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        CashTransfer? transfer = await dbContext.Set<CashTransfer>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (transfer is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, transfer, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        string? closingDateViolation = await ClosingDateGuard.GetViolationMessageAsync(
            dbContext, transfer.TransferDate, timeProvider, cancellationToken);

        if (closingDateViolation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(closingDateViolation);
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            transfer.Cancel(reason, now, actorId);

            await JournalPostingService.ReverseAsync(
                dbContext, JournalEntrySourceType.CashTransfer, transfer.Id, now, actorId, cancellationToken);

            dbContext.Add(new OperationLogEntry(
                Guid.NewGuid(),
                OperationLogEntityType.CashTransfer,
                transfer.Id,
                OperationLogAction.Cancelled,
                reason,
                now,
                actorId));

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible anular el traspaso.",
                cancellationToken);
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
    }

    private async Task<IReadOnlyList<CashTransferModel>> MapAllAsync(
        IReadOnlyList<CashTransfer> transfers,
        CancellationToken cancellationToken)
    {
        if (transfers.Count == 0)
        {
            return [];
        }

        Guid[] accountIds = transfers
            .SelectMany(transfer => new[] { transfer.SourceCashAccountId, transfer.DestinationCashAccountId })
            .Distinct()
            .ToArray();

        IReadOnlyDictionary<Guid, string> accountNames = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, account => account.Name, cancellationToken);

        return transfers
            .Select(transfer => Map(
                transfer,
                accountNames.GetValueOrDefault(transfer.SourceCashAccountId, "—"),
                accountNames.GetValueOrDefault(transfer.DestinationCashAccountId, "—")))
            .ToArray();
    }

    private static RegisterCashTransferResult Rejected(string message) =>
        new(CatalogOperationResult.Invalid(message), null, false);

    private static CashTransferModel Map(
        CashTransfer transfer,
        string sourceCashAccountName,
        string destinationCashAccountName)
    {
        return new CashTransferModel(
            transfer.Id,
            transfer.Code,
            transfer.SourceCashAccountId,
            sourceCashAccountName,
            transfer.DestinationCashAccountId,
            destinationCashAccountName,
            transfer.Amount,
            transfer.Reference,
            transfer.Description,
            transfer.TransferDate,
            transfer.Status,
            transfer.CancellationReason,
            transfer.CreatedAtUtc,
            CatalogPersistence.EncodeVersion(transfer.RowVersion));
    }
}
