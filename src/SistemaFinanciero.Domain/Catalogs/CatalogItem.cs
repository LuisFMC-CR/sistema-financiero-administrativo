using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Representa un producto o servicio facturable sin incorporar control de inventario.
/// </summary>
/// <remarks>
/// El precio es una referencia editable para preparar documentos, en colones. Las líneas confirmadas
/// deberán conservar su propia fotografía de descripción y precio.
///
/// El código lo asigna el sistema al crear (<c>PRD-0001</c>/<c>SRV-0001</c> según el tipo, regla 12) y
/// no cambia después; tampoco <see cref="Type"/>, porque el código ya lo codifica.
///
/// Conexiones: lo crea y edita <c>CatalogItemService</c> (Infrastructure/Catalogs/Items); se guarda en
/// <c>catalogos.ProductosServicios</c> vía <c>CatalogItemConfiguration</c>; lo presenta
/// <c>CatalogItemsController</c> (Web, ruta <c>/catalogos/productos-servicios</c>).
/// <see cref="DefaultIncomeAccountId"/> apunta a una <c>FinancialCategory</c> de tipo Ingreso.
/// <see cref="DefaultTaxTypeId"/> apunta a un <c>TaxType</c> (RF-52): la tarifa de IVA que se propondrá
/// al agregar el artículo a una línea de factura, editable en la línea misma. <see cref="Id"/> es el
/// valor que <c>InvoiceLine.CatalogItemId</c> apunta cuando la línea viene de este catálogo; una línea
/// con descripción libre lo deja nulo.
/// </remarks>
public sealed class CatalogItem
{
    /// <summary>Longitud máxima del código visible.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 150;

    /// <summary>Longitud máxima de la descripción.</summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>Longitud exacta del código CABYS (bloque 3 del paquete de mejoras finales).</summary>
    public const int CabysCodeLength = 13;

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
        string cabysCode,
        string? description,
        UnitOfMeasureCode unitOfMeasure,
        decimal? referencePriceCrc,
        Guid? defaultIncomeAccountId,
        Guid? defaultTaxTypeId,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Type = DomainRules.DefinedEnum(type, nameof(type));
        CabysCode = DomainRules.RequiredDigits(cabysCode, CabysCodeLength, nameof(cabysCode));
        Description = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        UnitOfMeasure = DomainRules.DefinedEnum(unitOfMeasure, nameof(unitOfMeasure));
        ReferencePriceCrc = NormalizeReferencePrice(referencePriceCrc, nameof(referencePriceCrc));
        DefaultIncomeAccountId = DomainRules.OptionalId(
            defaultIncomeAccountId,
            nameof(defaultIncomeAccountId));
        DefaultTaxTypeId = DomainRules.OptionalId(defaultTaxTypeId, nameof(defaultTaxTypeId));
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

    /// <summary>
    /// Obtiene el código CABYS (bloque 3 del paquete de mejoras finales), obligatorio y de 13 dígitos;
    /// lo copia <c>InvoiceLine.CabysCode</c> al agregar la línea, igual que el nombre y la unidad.
    /// </summary>
    public string CabysCode { get; private set; } = string.Empty;

    /// <summary>Obtiene la descripción opcional mostrada al preparar documentos.</summary>
    public string? Description { get; private set; }

    /// <summary>Obtiene la unidad utilizada para expresar cantidades, de una lista fija.</summary>
    public UnitOfMeasureCode UnitOfMeasure { get; private set; }

    /// <summary>Obtiene el precio de referencia en colones, si fue definido.</summary>
    public decimal? ReferencePriceCrc { get; private set; }

    /// <summary>
    /// Obtiene la categoría de ingreso sugerida. Application debe comprobar que sea de ingreso.
    /// </summary>
    public Guid? DefaultIncomeAccountId { get; private set; }

    /// <summary>
    /// Obtiene la tarifa de IVA que se propone al agregar el artículo a una línea de factura (RF-52);
    /// la línea puede cambiarla.
    /// </summary>
    public Guid? DefaultTaxTypeId { get; private set; }

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
    /// Actualiza los datos editables y los precios de referencia del producto o servicio. El código y
    /// el tipo no cambian: el código codifica el tipo (<c>PRD-</c>/<c>SRV-</c>, regla 12).
    /// </summary>
    public void UpdateDetails(
        string name,
        string cabysCode,
        string? description,
        UnitOfMeasureCode unitOfMeasure,
        decimal? referencePriceCrc,
        Guid? defaultIncomeAccountId,
        Guid? defaultTaxTypeId,
        DateTimeOffset updatedAtUtc,
        Guid updatedByUserId)
    {
        string normalizedName = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        string normalizedCabysCode = DomainRules.RequiredDigits(cabysCode, CabysCodeLength, nameof(cabysCode));
        string? normalizedDescription = DomainRules.OptionalText(
            description,
            DescriptionMaxLength,
            nameof(description));
        UnitOfMeasureCode normalizedUnitOfMeasure = DomainRules.DefinedEnum(unitOfMeasure, nameof(unitOfMeasure));
        decimal? normalizedReferencePriceCrc = NormalizeReferencePrice(
            referencePriceCrc,
            nameof(referencePriceCrc));
        Guid? normalizedDefaultIncomeAccountId = DomainRules.OptionalId(
            defaultIncomeAccountId,
            nameof(defaultIncomeAccountId));
        Guid? normalizedDefaultTaxTypeId = DomainRules.OptionalId(defaultTaxTypeId, nameof(defaultTaxTypeId));
        DateTimeOffset normalizedUpdatedAtUtc = ValidateUpdateAudit(updatedAtUtc, updatedByUserId);

        Name = normalizedName;
        CabysCode = normalizedCabysCode;
        Description = normalizedDescription;
        UnitOfMeasure = normalizedUnitOfMeasure;
        ReferencePriceCrc = normalizedReferencePriceCrc;
        DefaultIncomeAccountId = normalizedDefaultIncomeAccountId;
        DefaultTaxTypeId = normalizedDefaultTaxTypeId;
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
