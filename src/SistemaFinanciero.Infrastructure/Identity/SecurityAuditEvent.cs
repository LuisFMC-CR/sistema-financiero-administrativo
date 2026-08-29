using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.Infrastructure.Identity;

/// <summary>
/// Evento inmutable de la bitácora de seguridad.
/// </summary>
public sealed class SecurityAuditEvent
{
    private SecurityAuditEvent()
    {
    }

    /// <summary>Inicializa un evento sin almacenar credenciales, tokens ni datos sensibles.</summary>
    public SecurityAuditEvent(
        Guid id,
        SecurityAuditAction action,
        Guid? actorUserId,
        Guid targetUserId,
        DateTimeOffset occurredAtUtc,
        string? role = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("El identificador del evento es obligatorio.", nameof(id));
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        if (actorUserId == Guid.Empty)
        {
            throw new ArgumentException("El actor indicado no es válido.", nameof(actorUserId));
        }

        if (targetUserId == Guid.Empty)
        {
            throw new ArgumentException("El usuario objetivo es obligatorio.", nameof(targetUserId));
        }

        if (occurredAtUtc == default || occurredAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("El instante del evento debe expresarse en UTC.", nameof(occurredAtUtc));
        }

        if (role is not null && !SystemRoles.All.Contains(role, StringComparer.Ordinal))
        {
            throw new ArgumentException("El rol indicado no pertenece al catálogo aprobado.", nameof(role));
        }

        Id = id;
        Action = action;
        ActorUserId = actorUserId;
        TargetUserId = targetUserId;
        OccurredAtUtc = occurredAtUtc;
        Role = role;
    }

    /// <summary>Identificador del evento.</summary>
    public Guid Id { get; private set; }

    /// <summary>Acción de seguridad registrada.</summary>
    public SecurityAuditAction Action { get; private set; }

    /// <summary>Usuario que ejecutó la acción; es nulo para el bootstrap técnico.</summary>
    public Guid? ActorUserId { get; private set; }

    /// <summary>Cuenta sobre la que se ejecutó la acción.</summary>
    public Guid TargetUserId { get; private set; }

    /// <summary>Instante UTC en que ocurrió la acción.</summary>
    public DateTimeOffset OccurredAtUtc { get; private set; }

    /// <summary>Rol involucrado cuando la acción lo requiere.</summary>
    public string? Role { get; private set; }
}
