using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Audit;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Audit;

/// <summary>
/// Consulta de solo lectura de <see cref="OperationLogEntry"/> (bitácora de operaciones).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IOperationAuditService</c> (Application); la inyecta
/// <c>OperationAuditController</c> (Web, exclusivo de Gerencia). No escribe en la bitácora: eso lo hacen
/// los servicios de cada documento (<c>InvoiceService</c>, <c>ExpenseService</c>, <c>IncomeService</c>,
/// <c>ReceivablePaymentService</c>, <c>SupplierPaymentService</c>, <c>BalanceAdjustmentService</c>)
/// dentro de la misma transacción que confirman, anulan o ajustan.
/// </remarks>
internal sealed class OperationAuditService(FinancialDbContext dbContext) : IOperationAuditService
{
    private const int PageSize = 20;

    public async Task<PagedResult<OperationAuditEntryModel>> SearchAsync(
        OperationAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<OperationLogEntry> entries = dbContext.OperationLogEntries.AsNoTracking();

        IQueryable<OperationAuditEntryModel> projected = entries
            .OrderByDescending(entry => entry.PerformedAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new OperationAuditEntryModel(
                entry.Id,
                entry.EntityType,
                entry.EntityId,
                entry.Action,
                entry.Reason,
                dbContext.Users
                    .Where(user => user.Id == entry.PerformedByUserId)
                    .Select(user => user.FullName)
                    .FirstOrDefault() ?? "Usuario desconocido",
                entry.PerformedAtUtc));

        return await CatalogPersistence.ToPageAsync(projected, query.Page, PageSize, cancellationToken);
    }
}
