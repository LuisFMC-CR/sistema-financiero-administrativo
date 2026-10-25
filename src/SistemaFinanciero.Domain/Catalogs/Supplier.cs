using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Representa una persona u organización que suministra bienes o servicios a la empresa.
/// </summary>
/// <remarks>
/// El código lo asigna el sistema al crear (<c>PRV-0001</c>, regla 12) y no cambia después; tampoco la
/// identificación, que es obligatoria y única en esta tabla.
///
/// Conexiones: lo crea y edita <c>SupplierCatalogService</c> (Infrastructure/Catalogs/Suppliers); se
/// guarda en <c>catalogos.Proveedores</c> vía <c>SupplierConfiguration</c>; lo presenta
/// <c>SuppliersController</c> (Web, ruta <c>/catalogos/proveedores</c>). Todavía no lo usa ningún
/// documento: el módulo de Gastos y cuentas por pagar, que lo referenciaría igual que
/// <c>Invoice.CustomerId</c> referencia a <see cref="Customer"/>, no se ha construido.
/// </remarks>
public sealed class Supplier
{
    /// <summary>Longitud máxima del código visible del proveedor.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre del proveedor.</summary>
    public const int NameMaxLength = 150;

    /// <summary>Longitud máxima de su identificación fiscal o personal.</summary>
    public const int IdentificationMaxLength = 50;

    /// <summary>Longitud máxima del correo electrónico de contacto.</summary>
    public const int EmailMaxLength = 254;

    /// <summary>Longitud máxima del teléfono de contacto.</summary>
    public const int PhoneMaxLength = 30;

    /// <summary>Longitud máxima de la dirección.</summary>
    public const int AddressMaxLength = 500;

    /// <summary>Longitud máxima de la actividad económica.</summary>
    public const int EconomicActivityMaxLength = 200;

    private Supplier()
    {
    }

    /// <summary>
    /// Crea un proveedor activo y registra quién lo incorporó al catálogo.
    /// </summary>
    public Supplier(
        Guid id,
        string code,
        string name,
        string identification,
        string? email,
        string? phone,
        string? address,
        string? economicActivity,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Identification = DomainRules.RequiredText(
            identification,
            IdentificationMaxLength,
            nameof(identification));
        Email = DomainRules.OptionalText(email, EmailMaxLength, nameof(email));
        Phone = DomainRules.OptionalText(phone, PhoneMaxLength, nameof(phone));
        Address = DomainRules.OptionalText(address, AddressMaxLength, nameof(address));
        EconomicActivity = DomainRules.OptionalText(
            economicActivity,
            EconomicActivityMaxLength,
            nameof(economicActivity));
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

    /// <summary>Obtiene la identificación fiscal o personal, obligatoria y única.</summary>
    public string Identification { get; private set; } = string.Empty;

    /// <summary>Obtiene el correo electrónico de contacto.</summary>
    public string? Email { get; private set; }

    /// <summary>Obtiene el teléfono de contacto.</summary>
    public string? Phone { get; private set; }

    /// <summary>Obtiene la dirección de contacto.</summary>
    public string? Address { get; private set; }

    /// <summary>Obtiene la actividad económica declarada, cuando se conoce.</summary>
    public string? EconomicActivity { get; private set; }

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
    /// Actualiza los datos editables del proveedor y conserva su identidad. El código no cambia.
    /// </summary>
    public void UpdateDetails(
        string name,
        string identification,
        string? email,
        string? phone,
        string? address,
        string? economicActivity,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        string normalizedIdentification = DomainRules.RequiredText(
            identification,
            IdentificationMaxLength,
            nameof(identification));
        string? normalizedEmail = DomainRules.OptionalText(email, EmailMaxLength, nameof(email));
        string? normalizedPhone = DomainRules.OptionalText(phone, PhoneMaxLength, nameof(phone));
        string? normalizedAddress = DomainRules.OptionalText(address, AddressMaxLength, nameof(address));
        string? normalizedEconomicActivity = DomainRules.OptionalText(
            economicActivity,
            EconomicActivityMaxLength,
            nameof(economicActivity));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Name = normalizedName;
        Identification = normalizedIdentification;
        Email = normalizedEmail;
        Phone = normalizedPhone;
        Address = normalizedAddress;
        EconomicActivity = normalizedEconomicActivity;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>Habilita al proveedor para nuevas operaciones.</summary>
    public void Activate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(true, updatedAtUtc, updatedByUserId);
    }

    /// <summary>
    /// Impide seleccionar al proveedor en operaciones nuevas sin eliminar su historial.
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
}
