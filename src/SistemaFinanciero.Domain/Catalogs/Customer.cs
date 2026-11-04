using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Representa una persona u organización a la que la empresa puede facturar.
/// </summary>
/// <remarks>
/// El código lo asigna el sistema al crear (<c>CLI-0001</c>, regla 12) y no cambia después; tampoco la
/// identificación, que es obligatoria y única en esta tabla.
///
/// Conexiones: lo crea y edita <c>CustomerCatalogService</c> (Infrastructure/Catalogs/Customers); se
/// guarda en <c>catalogos.Clientes</c> vía <c>CustomerConfiguration</c>; lo presenta
/// <c>CustomersController</c> (Web, ruta <c>/catalogos/clientes</c>). <see cref="Id"/> es el valor que
/// <c>Invoice.CustomerId</c> apunta sin una referencia de objeto (son proyectos distintos: Domain no
/// puede depender de sí mismo circularmente entre carpetas de módulo, así que el vínculo es solo por
/// identificador).
/// </remarks>
public sealed class Customer
{
    /// <summary>Longitud máxima del código visible del cliente.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre del cliente.</summary>
    public const int NameMaxLength = 150;

    /// <summary>Longitud máxima de su identificación fiscal o personal.</summary>
    public const int IdentificationMaxLength = 50;

    /// <summary>Longitud máxima del correo electrónico de contacto.</summary>
    public const int EmailMaxLength = 254;

    /// <summary>Longitud máxima del teléfono de contacto.</summary>
    public const int PhoneMaxLength = 30;

    /// <summary>Longitud máxima de la dirección.</summary>
    public const int AddressMaxLength = 500;

    /// <summary>
    /// Longitud máxima del código de actividad económica (bloque 2 del paquete v7), usado para
    /// preparar la exportación de datos de la factura (RF-88).
    /// </summary>
    public const int ActivityCodeMaxLength = 10;

    private Customer()
    {
    }

    /// <summary>
    /// Crea un cliente activo y registra quién lo incorporó al catálogo.
    /// </summary>
    public Customer(
        Guid id,
        string code,
        string name,
        IdentificationType identificationType,
        string identification,
        string? email,
        string? phone,
        string? address,
        decimal? creditLimit,
        VatCondition vatCondition,
        bool appliesStateWithholding,
        string? activityCode,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        IdentificationType = DomainRules.DefinedEnum(identificationType, nameof(identificationType));
        Identification = IdentificationTypeRules.Validate(
            IdentificationType,
            identification,
            IdentificationMaxLength,
            nameof(identification));
        Email = DomainRules.OptionalText(email, EmailMaxLength, nameof(email));
        Phone = DomainRules.OptionalText(phone, PhoneMaxLength, nameof(phone));
        Address = DomainRules.OptionalText(address, AddressMaxLength, nameof(address));
        CreditLimit = RequireNonNegativeLimit(creditLimit, nameof(creditLimit));
        VatCondition = DomainRules.DefinedEnum(vatCondition, nameof(vatCondition));
        AppliesStateWithholding = appliesStateWithholding;
        ActivityCode = DomainRules.OptionalText(activityCode, ActivityCodeMaxLength, nameof(activityCode));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        IsActive = true;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el código visible, almacenado en mayúsculas.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene el nombre completo o razón social.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Obtiene el tipo de identificación (bloque 3 del paquete de mejoras finales), que determina
    /// cuántos dígitos exige <see cref="Identification"/>.
    /// </summary>
    public IdentificationType IdentificationType { get; private set; }

    /// <summary>Obtiene la identificación fiscal o personal, obligatoria y única.</summary>
    public string Identification { get; private set; } = string.Empty;

    /// <summary>Obtiene el correo electrónico de contacto.</summary>
    public string? Email { get; private set; }

    /// <summary>Obtiene el teléfono de contacto.</summary>
    public string? Phone { get; private set; }

    /// <summary>Obtiene la dirección de contacto.</summary>
    public string? Address { get; private set; }

    /// <summary>
    /// Obtiene el límite propio de crédito, en colones, o <c>null</c> si no tiene uno configurado.
    /// Es solo informativo: al confirmar una factura a crédito que lo supera, el sistema muestra una
    /// alerta pero nunca bloquea la confirmación.
    /// </summary>
    public decimal? CreditLimit { get; private set; }

    /// <summary>
    /// Obtiene la condición de IVA (bloque 1 del paquete v7, RF-93), que determina automáticamente la
    /// tarifa de IVA de cada línea de las facturas de este cliente. Predeterminada en Normal.
    /// </summary>
    public VatCondition VatCondition { get; private set; }

    /// <summary>
    /// Indica si el cliente retiene el 2 % del sector público (bloque 1 del paquete v7, RF-95) en sus
    /// abonos, cuando el monto neto de la factura supera la base mínima. Predeterminado en falso.
    /// </summary>
    public bool AppliesStateWithholding { get; private set; }

    /// <summary>
    /// Obtiene el código de actividad económica del cliente (bloque 2 del paquete v7), usado para
    /// preparar la exportación de datos de la factura (RF-88). Algunos clientes institucionales lo
    /// exigen (p. ej. la UCR, código 8530.1).
    /// </summary>
    public string? ActivityCode { get; private set; }

    /// <summary>Indica si puede seleccionarse para nuevas operaciones.</summary>
    public bool IsActive { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que creó el registro.</summary>
    public Guid CreatedByUserId { get; private set; }

    /// <summary>Obtiene el instante UTC del último cambio.</summary>
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que realizó el último cambio.</summary>
    public Guid UpdatedByUserId { get; private set; }

    /// <summary>Obtiene el token de concurrencia administrado por la persistencia.</summary>
    public byte[] RowVersion { get; private set; } = [];

    /// <summary>
    /// Actualiza los datos editables del cliente y conserva su identidad. El código no cambia.
    /// </summary>
    public void UpdateDetails(
        string name,
        IdentificationType identificationType,
        string identification,
        string? email,
        string? phone,
        string? address,
        decimal? creditLimit,
        VatCondition vatCondition,
        bool appliesStateWithholding,
        string? activityCode,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        IdentificationType normalizedIdentificationType = DomainRules.DefinedEnum(
            identificationType,
            nameof(identificationType));
        string normalizedIdentification = IdentificationTypeRules.Validate(
            normalizedIdentificationType,
            identification,
            IdentificationMaxLength,
            nameof(identification));
        string? normalizedEmail = DomainRules.OptionalText(email, EmailMaxLength, nameof(email));
        string? normalizedPhone = DomainRules.OptionalText(phone, PhoneMaxLength, nameof(phone));
        string? normalizedAddress = DomainRules.OptionalText(address, AddressMaxLength, nameof(address));
        decimal? normalizedCreditLimit = RequireNonNegativeLimit(creditLimit, nameof(creditLimit));
        VatCondition normalizedVatCondition = DomainRules.DefinedEnum(vatCondition, nameof(vatCondition));
        string? normalizedActivityCode = DomainRules.OptionalText(activityCode, ActivityCodeMaxLength, nameof(activityCode));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Name = normalizedName;
        IdentificationType = normalizedIdentificationType;
        Identification = normalizedIdentification;
        Email = normalizedEmail;
        Phone = normalizedPhone;
        Address = normalizedAddress;
        CreditLimit = normalizedCreditLimit;
        VatCondition = normalizedVatCondition;
        AppliesStateWithholding = appliesStateWithholding;
        ActivityCode = normalizedActivityCode;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>Habilita al cliente para nuevas operaciones.</summary>
    public void Activate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(true, updatedAtUtc, updatedByUserId);
    }

    /// <summary>
    /// Impide seleccionar al cliente en operaciones nuevas sin eliminar su historial.
    /// </summary>
    public void Deactivate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(false, updatedAtUtc, updatedByUserId);
    }

    private void SetActive(bool isActive, DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        if (IsActive == isActive)
        {
            return;
        }

        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);
        IsActive = isActive;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    private DateTimeOffset ValidateUpdateAudit(
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        DomainRules.RequiredId(updatedByUserId, nameof(updatedByUserId));
        return DomainRules.RequiredUtcTimestamp(
            updatedAtUtc,
            nameof(updatedAtUtc),
            UpdatedAtUtc);
    }

    private void Touch(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        UpdatedAtUtc = updatedAtUtc;
        UpdatedByUserId = updatedByUserId;
    }

    private static decimal? RequireNonNegativeLimit(decimal? value, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        decimal roundedValue = DomainRules.RoundMoney(value.Value);

        return roundedValue >= 0
            ? roundedValue
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "El límite de crédito no puede ser negativo.");
    }
}
