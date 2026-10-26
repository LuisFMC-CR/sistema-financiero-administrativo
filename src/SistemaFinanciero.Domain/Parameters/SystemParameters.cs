using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Parameters;

/// <summary>
/// Parámetros financieros únicos del sistema: el límite de autorización de abonos y pagos (regla de
/// negocio 7), los días de alerta de vencimiento próximo (regla de negocio 9) y la fecha de cierre
/// (regla de negocio 16).
/// </summary>
/// <remarks>
/// Existe como máximo una fila, siempre con el mismo identificador fijo <see cref="SingletonId"/>. No
/// tiene baja lógica: los parámetros del sistema siempre están vigentes, aunque no se hayan configurado
/// todavía, en cuyo caso simplemente no existe la fila.
///
/// Conexiones: la crea y edita <c>SystemParametersService</c> (Infrastructure/Parameters); se guarda en
/// <c>finanzas.ParametrosSistema</c> vía <c>SystemParametersConfiguration</c>; la presenta
/// <c>SystemParametersController</c> (Web, ruta <c>/parametros</c>). Cada cambio de límite o días genera
/// además un <see cref="SystemParameterChange"/>; cada cambio de <see cref="ClosingDate"/> genera un
/// <see cref="ClosingDateChange"/>; cada cambio de los datos de la empresa genera un
/// <see cref="CompanyDataChange"/> (RF-55). Todavía nada lee <see cref="AuthorizationLimitCrc"/> ni
/// <see cref="OverdueAlertDays"/> para aplicar la regla: eso lo hará el futuro módulo de abonos/pagos
/// (el límite) y el de Consultas (la alerta de vencimiento). <see cref="ClosingDate"/> todavía no se
/// valida en las demás operaciones: eso queda para el siguiente bloque del roadmap.
/// </remarks>
public sealed class SystemParameters
{
    /// <summary>Longitud máxima del motivo de un cambio de fecha de cierre.</summary>
    public const int ClosingDateChangeReasonMaxLength = 300;

    /// <summary>Longitud máxima del nombre de la empresa.</summary>
    public const int CompanyNameMaxLength = 150;

    /// <summary>Longitud máxima de la cédula jurídica.</summary>
    public const int CompanyLegalIdMaxLength = 50;

    /// <summary>Longitud máxima de la dirección de la empresa.</summary>
    public const int CompanyAddressMaxLength = 300;

    /// <summary>Longitud máxima del teléfono de la empresa.</summary>
    public const int CompanyPhoneMaxLength = 30;

    /// <summary>Longitud máxima del correo de la empresa.</summary>
    public const int CompanyEmailMaxLength = 150;

    /// <summary>Identificador fijo de la única fila permitida.</summary>
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Fecha de cierre con la que nace la fila: no cierra nada hasta que Gerencia adelante la fecha
    /// por primera vez (regla 16).
    /// </summary>
    private static readonly DateOnly NoClosingDate = DateOnly.MinValue;

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
        ClosingDate = NoClosingDate;
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

    /// <summary>Obtiene el nombre de la empresa, para el encabezado de la factura interna (regla 15).</summary>
    public string? CompanyName { get; private set; }

    /// <summary>Obtiene la cédula jurídica de la empresa.</summary>
    public string? CompanyLegalId { get; private set; }

    /// <summary>Obtiene la dirección de la empresa.</summary>
    public string? CompanyAddress { get; private set; }

    /// <summary>Obtiene el teléfono de la empresa.</summary>
    public string? CompanyPhone { get; private set; }

    /// <summary>Obtiene el correo de la empresa.</summary>
    public string? CompanyEmail { get; private set; }

    /// <summary>
    /// Actualiza los datos de la empresa (RF-55). Nombre y cédula jurídica son obligatorios; dirección,
    /// teléfono y correo son opcionales. Solo Gerencia los define.
    /// </summary>
    public void UpdateCompany(
        string name,
        string legalId,
        string? address,
        string? phone,
        string? email,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string validName = DomainRules.RequiredText(name, CompanyNameMaxLength, nameof(name));
        string validLegalId = DomainRules.RequiredText(legalId, CompanyLegalIdMaxLength, nameof(legalId));
        string? validAddress = DomainRules.OptionalText(address, CompanyAddressMaxLength, nameof(address));
        string? validPhone = DomainRules.OptionalText(phone, CompanyPhoneMaxLength, nameof(phone));
        string? validEmail = DomainRules.OptionalText(email, CompanyEmailMaxLength, nameof(email));
        Guid normalizedUserId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        DateTimeOffset normalizedUpdatedAtUtc = DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);

        CompanyName = validName;
        CompanyLegalId = validLegalId;
        CompanyAddress = validAddress;
        CompanyPhone = validPhone;
        CompanyEmail = validEmail;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
        UpdatedByUserId = normalizedUserId;
    }

    /// <summary>
    /// Obtiene la fecha de cierre (regla 16): no se registra, confirma, anula ni ajusta nada con
    /// fecha igual o anterior a esta.
    /// </summary>
    public DateOnly ClosingDate { get; private set; }

    /// <summary>Indica si todavía se puede operar con la fecha de negocio indicada (regla 16).</summary>
    public bool IsOpen(DateOnly date) => date > ClosingDate;

    /// <summary>
    /// Cambia la fecha de cierre. Adelantarla es el cierre mensual, sin motivo; retrocederla es una
    /// reapertura y exige motivo (regla 16). Nunca puede quedar posterior a <paramref name="today"/>.
    /// </summary>
    public void ChangeClosingDate(
        DateOnly newClosingDate,
        DateOnly today,
        string? reason,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        if (newClosingDate > today)
        {
            throw new ArgumentOutOfRangeException(
                nameof(newClosingDate),
                newClosingDate,
                "La fecha de cierre no puede ser posterior a hoy.");
        }

        if (newClosingDate < ClosingDate)
        {
            DomainRules.RequiredText(reason, ClosingDateChangeReasonMaxLength, nameof(reason));
        }

        Guid normalizedUserId = DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        DateTimeOffset normalizedUpdatedAtUtc = DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);

        ClosingDate = newClosingDate;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
        UpdatedByUserId = normalizedUserId;
    }

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
