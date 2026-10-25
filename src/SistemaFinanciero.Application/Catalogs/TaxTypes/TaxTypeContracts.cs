using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Catalogs.TaxTypes;

/// <summary>Datos requeridos para crear una tarifa de IVA.</summary>
/// <remarks>
/// Conexiones: lo recibe <c>ITaxTypeService.CreateAsync</c>, implementado por <c>TaxTypeService</c>
/// (Infrastructure/Catalogs/TaxTypes); lo produce también <see cref="ReferenceTaxTypes"/> para la carga
/// de tarifas de referencia. Lo consume <c>TaxTypesController</c> (Web, ruta
/// <c>/catalogos/tipos-impuesto</c>).
/// </remarks>
public sealed record CreateTaxTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string? Description);

/// <summary>Datos editables de una tarifa de IVA.</summary>
public sealed record UpdateTaxTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string? Description);

/// <summary>Proyección administrativa de una tarifa de IVA.</summary>
public sealed record TaxTypeModel(
    Guid Id,
    string Code,
    string Name,
    decimal Rate,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Opción activa para el selector de IVA de una línea de factura.</summary>
public sealed record TaxTypeOption(
    Guid Id,
    string Code,
    string Name,
    decimal Rate);

/// <summary>Resultado de cargar las tarifas de referencia: cuántas se crearon y cuántas ya existían.</summary>
public sealed record ReferenceTaxTypesLoadResult(
    CatalogOperationResult Operation,
    int CreatedCount,
    int ExistingCount);

/// <summary>Casos de uso del catálogo de tipos de impuesto.</summary>
public interface ITaxTypeService
{
    /// <summary>Busca tipos de impuesto con filtros y paginación.</summary>
    public Task<PagedResult<TaxTypeModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un tipo de impuesto por identificador.</summary>
    public Task<TaxTypeModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista los tipos de impuesto activos para un selector.</summary>
    public Task<IReadOnlyList<TaxTypeOption>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Crea una tarifa de IVA.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateTaxTypeCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza una tarifa de IVA.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateTaxTypeCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva un tipo de impuesto sin afectar facturas existentes.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Crea las tarifas de IVA de referencia que aún no existan, identificadas por su código. No
    /// modifica ni reactiva los tipos que ya existen.
    /// </summary>
    public Task<ReferenceTaxTypesLoadResult> LoadReferenceTaxTypesAsync(
        Guid actorId,
        CancellationToken cancellationToken = default);
}
