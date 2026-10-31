using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Security.Audit;

/// <summary>Página solicitada de la bitácora de seguridad; sin filtros, veinte filas por página.</summary>
public sealed record SecurityAuditQuery
{
    public SecurityAuditQuery(int page = 1)
    {
        Page = Math.Max(1, page);
    }

    /// <summary>Número de página basado en uno.</summary>
    public int Page { get; }
}

/// <summary>Una fila de la bitácora de seguridad, lista para mostrar sin datos sensibles.</summary>
public sealed record SecurityAuditEntryModel(
    Guid Id,
    SecurityAuditAction Action,
    string? ActorFullName,
    string TargetFullName,
    string? Role,
    DateTimeOffset OccurredAtUtc);

/// <summary>
/// Consulta de solo lectura, paginada, de la bitácora de seguridad (altas, cambios de rol,
/// activaciones, bloqueos y contraseñas de cuentas).
/// </summary>
/// <remarks>
/// Conexiones: la implementa <c>SecurityAuditService</c> (Infrastructure/Identity) sobre
/// <c>SecurityAuditEvent</c>; la consume <c>SecurityAuditController</c> (Web, ruta
/// <c>administracion/bitacora-seguridad</c>), exclusivo de Administrador vía
/// <c>SystemPolicies.ManageUsers</c>, igual que la administración de usuarios de la que forma parte.
/// Es la mitad "temas de sistema" de la bitácora; la mitad "temas de operación" (documentos
/// financieros confirmados/anulados/ajustados) la expone <c>IOperationAuditService</c> para Gerencia.
/// </remarks>
public interface ISecurityAuditService
{
    /// <summary>Busca y pagina la bitácora de seguridad, de la más reciente a la más antigua.</summary>
    public Task<PagedResult<SecurityAuditEntryModel>> SearchAsync(
        SecurityAuditQuery query,
        CancellationToken cancellationToken = default);
}
