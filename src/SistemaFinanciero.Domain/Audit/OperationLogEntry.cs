using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Audit;

/// <summary>
/// Registro inmutable de una acción sensible (confirmar, anular) sobre un documento financiero.
/// </summary>
/// <remarks>
/// Es la "BitacoraOperaciones" de la regla de negocio 4/16. A diferencia de los campos
/// <c>ConfirmedAtUtc</c>/<c>CancelledAtUtc</c> que cada documento ya guarda, esta tabla permite
/// consultar en un solo lugar el historial de varios tipos de documento (ver
/// <c>SystemPolicies.ViewAuditTrail</c>, todavía sin pantalla propia).
///
/// Conexiones: la crean los servicios de Infrastructure (hoy <c>InvoiceService</c>) dentro de la
/// misma transacción que confirma o anula el documento; se guarda en
/// <c>seguridad.BitacoraOperaciones</c> vía <c>OperationLogEntryConfiguration</c>.
/// <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c> impide modificarla o borrarla.
/// </remarks>
public sealed class OperationLogEntry
{
    /// <summary>Longitud máxima del motivo.</summary>
    public const int ReasonMaxLength = 300;

    private OperationLogEntry()
    {
    }

    /// <summary>Crea un registro de bitácora. No admite modificaciones posteriores.</summary>
    public OperationLogEntry(
        Guid id,
        OperationLogEntityType entityType,
        Guid entityId,
        OperationLogAction action,
        string? reason,
        DateTimeOffset performedAtUtc,
        Guid performedByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        EntityType = DomainRules.DefinedEnum(entityType, nameof(entityType));
        EntityId = DomainRules.RequiredId(entityId, nameof(entityId));
        Action = DomainRules.DefinedEnum(action, nameof(action));
        Reason = DomainRules.OptionalText(reason, ReasonMaxLength, nameof(reason));
        PerformedAtUtc = DomainRules.RequiredUtcTimestamp(performedAtUtc, nameof(performedAtUtc));
        PerformedByUserId = DomainRules.RequiredId(performedByUserId, nameof(performedByUserId));
    }

    /// <summary>Obtiene el identificador del registro.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el tipo de documento sobre el que se actuó.</summary>
    public OperationLogEntityType EntityType { get; private set; }

    /// <summary>Obtiene el identificador del documento.</summary>
    public Guid EntityId { get; private set; }

    /// <summary>Obtiene la acción realizada.</summary>
    public OperationLogAction Action { get; private set; }

    /// <summary>Obtiene el motivo, presente solo en anulaciones.</summary>
    public string? Reason { get; private set; }

    /// <summary>Obtiene el instante UTC en que ocurrió la acción.</summary>
    public DateTimeOffset PerformedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que la ejecutó.</summary>
    public Guid PerformedByUserId { get; private set; }
}
