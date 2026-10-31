using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Audit;

namespace SistemaFinanciero.Application.Audit;

/// <summary>Página solicitada de la bitácora de operaciones; sin filtros, veinte filas por página.</summary>
public sealed record OperationAuditQuery
{
    public OperationAuditQuery(int page = 1)
    {
        Page = Math.Max(1, page);
    }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }
}

/// <summary>Una fila de la bitácora de operaciones, lista para mostrar.</summary>
public sealed record OperationAuditEntryModel(
    Guid Id,
    OperationLogEntityType EntityType,
    Guid EntityId,
    OperationLogAction Action,
    string? Reason,
    string PerformedByFullName,
    DateTimeOffset PerformedAtUtc);

/// <summary>
/// Consulta de solo lectura, paginada, de la bitácora de operaciones: confirmar, anular y ajustar
/// documentos financieros (regla de negocio 4 y 16).
/// </summary>
/// <remarks>
/// Conexiones: la implementa <c>OperationAuditService</c> (Infrastructure/Audit) sobre
/// <c>OperationLogEntry</c>; la consume <c>OperationAuditController</c> (Web, ruta
/// <c>auditoria/bitacora-operaciones</c>), exclusivo de Gerencia vía <c>SystemPolicies.ViewAuditTrail</c>
/// (regla de perfiles: Administrador "sin acceso a información financiera", así que no entra aquí; es
/// la mitad "temas de operación" de la bitácora, complementaria a <c>ISecurityAuditService</c>).
/// </remarks>
public interface IOperationAuditService
{
    /// <summary>Busca y pagina la bitácora de operaciones, de la más reciente a la más antigua.</summary>
    public Task<PagedResult<OperationAuditEntryModel>> SearchAsync(
        OperationAuditQuery query,
        CancellationToken cancellationToken = default);
}
