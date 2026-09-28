using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Parámetros financieros únicos del sistema: el límite de autorización de abonos y pagos (regla de
/// negocio 7) y los días de alerta de vencimiento próximo (regla de negocio 9).
/// </summary>
/// <remarks>
/// Existe como máximo una fila, siempre con el mismo identificador fijo <see cref="SingletonId"/>. No
/// tiene baja lógica: los parámetros del sistema siempre están vigentes, aunque no se hayan configurado
/// todavía, en cuyo caso simplemente no existe la fila.
/// </remarks>
public sealed class SystemParameters
{
    /// <summary>Identificador fijo de la única fila permitida.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private SystemParameters()
    {
    }

    /// <summary>Crea la fila única de parámetros con sus valores iniciales.</summary>
    public SystemParameters(
        decimal authorizationLimitCrc,
        int overdueAlertDays,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = SingletonId;
        AuthorizationLimitCrc = RequireNonNegativeLimit(authorizationLimitCrc, nameof(authorizationLimitCrc));
        OverdueAlertDays = RequirePositiveDays(overdueAlertDays, nameof(overdueAlertDays));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
    }

    /// <summary>Obtiene el identificador técnico, siempre igual a <see cref="SingletonId"/>.</summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Obtiene el límite en CRC: los abonos y pagos por encima de este monto solo los registra Gerencia.
    /// </summary>
    public decimal AuthorizationLimitCrc { get; private set; }

    /// <summary>
    /// Obtiene los días de anticipación para marcar una cuenta por cobrar o por pagar como próxima a
    /// vencer.
    /// </summary>
    public int OverdueAlertDays { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que configuró los parámetros por primera vez.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario responsable del último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Obtiene el token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>Actualiza el límite de autorización y los días de alerta.</summary>
    public void UpdateValues(
        decimal authorizationLimitCrc,
        int overdueAlertDays,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        decimal normalizedLimit = RequireNonNegativeLimit(authorizationLimitCrc, nameof(authorizationLimitCrc));
        int normalizedDays = RequirePositiveDays(overdueAlertDays, nameof(overdueAlertDays));
        Guid normalizedUserId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        DateTimeOffset normalizedUpdatedAtUtc = DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);

        AuthorizationLimitCrc = normalizedLimit;
        OverdueAlertDays = normalizedDays;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
        UpdatedByUserId = normalizedUserId;
    }

    private static decimal RequireNonNegativeLimit(decimal value, string parameterName)
    {
        decimal roundedValue = DomainRules.RoundMoney(value);

        return roundedValue >= 0
            ? roundedValue
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "El límite de autorización no puede ser negativo.");
    }

    private static int RequirePositiveDays(int value, string parameterName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Los días de alerta deben ser mayores que cero.");
    }
}
