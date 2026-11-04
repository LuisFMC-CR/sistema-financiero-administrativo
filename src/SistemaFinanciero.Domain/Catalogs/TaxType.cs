using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Representa una tarifa de IVA configurable que las líneas de factura pueden aplicar.
/// </summary>
/// <remarks>
/// La tarifa es siempre porcentual. Las facturas no dependen de estos valores después de creadas: cada
/// línea conserva su propia fotografía.
///
/// Conexiones: lo crea y edita <c>TaxTypeService</c> (Infrastructure/Catalogs/TaxTypes); se guarda en
/// <c>catalogos.TiposImpuesto</c> vía <c>TaxTypeConfiguration</c>; lo presenta
/// <c>TaxTypesController</c> (Web, ruta <c>/catalogos/tipos-impuesto</c>), incluido el botón que carga
/// las tarifas de <c>ReferenceTaxTypes</c>. <see cref="Id"/> es el valor que
/// <c>InvoiceLineTax.TaxTypeId</c> apunta cuando la línea usó un tipo de este catálogo.
/// </remarks>
public sealed class TaxType
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 300;

    /// <summary>Tarifa máxima admitida.</summary>
    public const decimal MaxPercentageRate = 100m;

    /// <summary>Longitud exacta del código oficial de tarifa de Hacienda (bloque 3).</summary>
    public const int HaciendaRateCodeLength = 2;

    private TaxType()
    {
    }

    /// <summary>
    /// Crea una tarifa de IVA activa. Siempre nace sin rol de sistema: ese rol solo lo asigna la
    /// migración de siembra directamente por SQL (bloque 1 del paquete v7), nunca este constructor.
    /// </summary>
    public TaxType(
        Guid id,
        string code,
        string name,
        decimal rate,
        string haciendaRateCode,
        string? description,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Rate = RequireRate(rate, nameof(rate));
        HaciendaRateCode = DomainRules.RequiredDigits(
            haciendaRateCode,
            HaciendaRateCodeLength,
            nameof(haciendaRateCode));
        Description = DomainRules.OptionalText(description, DescriptionMaxLength, nameof(description));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        IsActive = true;
        SystemRole = null;
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el código visible, almacenado en mayúsculas.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene el nombre del impuesto.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Obtiene el porcentaje de la tarifa, con cuatro decimales.</summary>
    public decimal Rate { get; private set; }

    /// <summary>
    /// Obtiene el código oficial de tarifa de IVA de Hacienda (bloque 3 del paquete de mejoras
    /// finales), de 2 dígitos; no se emite, firma ni transmite nada con él, solo prepara los datos
    /// para el traslado manual a la plataforma externa. Nulo únicamente en la tarifa de sistema "No
    /// sujeto" (bloque 1 del paquete v7), porque el catálogo de Hacienda no tiene un código para ella;
    /// cualquier tarifa agregada a mano siempre lo exige.
    /// </summary>
    public string? HaciendaRateCode { get; private set; }

    /// <summary>Obtiene la descripción opcional.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Obtiene el rol de sistema, cuando esta tarifa es una de las cuatro que las reglas del sistema
    /// buscan por su rol (bloque 1 del paquete v7): nunca por código ni nombre, porque son editables.
    /// Nulo para cualquier tarifa que Gerencia agregue; solo la migración de siembra lo asigna.
    /// </summary>
    public TaxTypeSystemRole? SystemRole { get; private set; }

    /// <summary>Indica si puede seleccionarse en facturas nuevas.</summary>
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
    /// Actualiza código, nombre, tarifa, código de Hacienda y descripción. Solo la tarifa de sistema
    /// "No sujeto" admite un código de Hacienda vacío (bloque 1 del paquete v7); cualquier otra, de
    /// sistema o agregada a mano, siempre lo exige.
    /// </summary>
    public void UpdateDetails(
        string code,
        string name,
        decimal rate,
        string? haciendaRateCode,
        string? description,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedCode = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        decimal normalizedRate = RequireRate(rate, nameof(rate));
        string? normalizedHaciendaRateCode = SystemRole == TaxTypeSystemRole.NotSubject && string.IsNullOrWhiteSpace(haciendaRateCode)
            ? null
            : DomainRules.RequiredDigits(haciendaRateCode, HaciendaRateCodeLength, nameof(haciendaRateCode));
        string? normalizedDescription = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Code = normalizedCode;
        Name = normalizedName;
        Rate = normalizedRate;
        HaciendaRateCode = normalizedHaciendaRateCode;
        Description = normalizedDescription;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>Habilita el tipo para facturas nuevas.</summary>
    public void Activate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(true, updatedAtUtc, updatedByUserId);
    }

    /// <summary>Deshabilita el tipo sin afectar las facturas que ya lo usaron.</summary>
    public void Deactivate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(false, updatedAtUtc, updatedByUserId);
    }

    private static decimal RequireRate(decimal rate, string parameterName)
    {
        decimal roundedRate = DomainRules.Round(rate, DomainRules.QuantityDecimalPlaces);

        if (roundedRate < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                rate,
                "La tarifa del impuesto no puede ser negativa.");
        }

        if (roundedRate > MaxPercentageRate)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                rate,
                "La tarifa porcentual no puede superar 100 %.");
        }

        return roundedRate;
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
