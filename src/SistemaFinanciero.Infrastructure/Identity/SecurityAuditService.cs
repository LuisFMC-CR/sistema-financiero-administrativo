using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security.Audit;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Consulta de solo lectura de <see cref="SecurityAuditEvent"/> (bitácora de seguridad).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>ISecurityAuditService</c> (Application); la inyecta
/// <c>SecurityAuditController</c> (Web, exclusivo de Administrador). No escribe en la bitácora: eso lo
/// hacen <c>IdentityBootstrapper</c>, <c>UserAdministrationService</c> y <c>UserAccountService</c>.
/// </remarks>
internal sealed class SecurityAuditService(FinancialDbContext dbContext) : ISecurityAuditService
{
    private const int PageSize = 20;

    public async Task<PagedResult<SecurityAuditEntryModel>> SearchAsync(
        SecurityAuditQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<SecurityAuditEvent> events = dbContext.SecurityAuditEvents.AsNoTracking();

        IQueryable<SecurityAuditEntryModel> projected = events
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new SecurityAuditEntryModel(
                entry.Id,
                entry.Action,
                entry.ActorUserId.HasValue
                    ? dbContext.Users
                        .Where(user => user.Id == entry.ActorUserId.Value)
                        .Select(user => user.FullName)
                        .FirstOrDefault()
                    : null,
                dbContext.Users
                    .Where(user => user.Id == entry.TargetUserId)
                    .Select(user => user.FullName)
                    .FirstOrDefault() ?? "Usuario desconocido",
                entry.Role,
                entry.OccurredAtUtc));

        return await CatalogPersistence.ToPageAsync(projected, query.Page, PageSize, cancellationToken);
    }
}
