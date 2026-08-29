using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Representa un producto o servicio facturable sin incorporar control de inventario.
/// </summary>
/// <remarks>
/// Los precios son referencias editables para preparar documentos. Las líneas confirmadas deberán
/// conservar su propia fotografía de descripción, moneda y precio.
/// </remarks>
public sealed class CatalogItem
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 150;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Longitud máxima de la unidad de medida.</summary>
    public const int UnitOfMeasureMaxLength = 30;

    /// <summary>Cantidad de decimales conservados en los precios de referencia.</summary>
    public const int ReferencePriceDecimalPlaces = 4;

    private CatalogItem()
    {
    }

    /// <summary>
    /// Crea un producto o servicio activo dentro del catálogo unificado.
    /// </summary>
    public CatalogItem(
        Guid id,
        string code,
        string name,
        CatalogItemType type,
        string? description,
        string unitOfMeasure,
        decimal? referencePriceCrc,
        decimal? referencePriceUsd,
        Guid? defaultIncomeCategoryId,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Type = DomainRules.DefinedEnum(type, nameof(type));
        Description = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        UnitOfMeasure = DomainRules.RequiredText(
            unitOfMeasure,
            UnitOfMeasureMaxLength,
            nameof(unitOfMeasure));
        ReferencePriceCrc = NormalizeReferencePrice(referencePriceCrc, nameof(referencePriceCrc));
        ReferencePriceUsd = NormalizeReferencePrice(referencePriceUsd, nameof(referencePriceUsd));
        DefaultIncomeCategoryId = DomainRules.OptionalId(
            defaultIncomeCategoryId,
            nameof(defaultIncomeCategoryId));
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

    /// <summary>Obtiene el nombre del producto o servicio.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Obtiene si el elemento es un producto o un servicio.</summary>
    public CatalogItemType Type { get; private set; }

    /// <summary>Obtiene la descripción opcional mostrada al preparar documentos.</summary>
    public string? Description { get; private set; }

    /// <summary>Obtiene la unidad utilizada para expresar cantidades.</summary>
    public string UnitOfMeasure { get; private set; } = string.Empty;

    /// <summary>Obtiene el precio de referencia en colones, si fue definido.</summary>
    public decimal? ReferencePriceCrc { get; private set; }

    /// <summary>Obtiene el precio de referencia en dólares, si fue definido.</summary>
    public decimal? ReferencePriceUsd { get; private set; }

    /// <summary>
    /// Obtiene la categoría de ingreso sugerida. Application debe comprobar que sea de ingreso.
    /// </summary>
    public Guid? DefaultIncomeCategoryId { get; private set; }

    /// <summary>Indica si puede seleccionarse en operaciones nuevas.</summary>
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
    /// Actualiza los datos editables y los precios de referencia del producto o servicio.
    /// </summary>
    public void UpdateDetails(
        string code,
        string name,
        CatalogItemType type,
        string? description,
        string unitOfMeasure,
        decimal? referencePriceCrc,
        decimal? referencePriceUsd,
        Guid? defaultIncomeCategoryId,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedCode = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        CatalogItemType normalizedType = DomainRules.DefinedEnum(type, nameof(type));
        string? normalizedDescription = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        string normalizedUnitOfMeasure = DomainRules.RequiredText(
            unitOfMeasure,
            UnitOfMeasureMaxLength,
            nameof(unitOfMeasure));
        decimal? normalizedReferencePriceCrc = NormalizeReferencePrice(
            referencePriceCrc,
            nameof(referencePriceCrc));
        decimal? normalizedReferencePriceUsd = NormalizeReferencePrice(
            referencePriceUsd,
            nameof(referencePriceUsd));
        Guid? normalizedDefaultIncomeCategoryId = DomainRules.OptionalId(
            defaultIncomeCategoryId,
            nameof(defaultIncomeCategoryId));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Code = normalizedCode;
        Name = normalizedName;
        Type = normalizedType;
        Description = normalizedDescription;
        UnitOfMeasure = normalizedUnitOfMeasure;
        ReferencePriceCrc = normalizedReferencePriceCrc;
        ReferencePriceUsd = normalizedReferencePriceUsd;
        DefaultIncomeCategoryId = normalizedDefaultIncomeCategoryId;
        Touch(normalizedUpdatedAtUtc, updatedByUserId);
    }

    /// <summary>Habilita el elemento para nuevas operaciones.</summary>
    public void Activate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(true, updatedAtUtc, updatedByUserId);
    }

    /// <summary>Deshabilita el elemento sin eliminar sus referencias históricas.</summary>
    public void Deactivate(DateTimeOffset updatedAtUtc, Guid updatedByUserId)
    {
        SetActive(false, updatedAtUtc, updatedByUserId);
    }

    private static decimal? NormalizeReferencePrice(decimal? value, string parameterName)
    {
        return DomainRules.OptionalPositiveDecimal(
            value,
            ReferencePriceDecimalPlaces,
            parameterName);
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
