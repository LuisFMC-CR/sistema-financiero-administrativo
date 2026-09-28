using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Application.Catalogs.TaxTypes;

/// <summary>Datos requeridos para crear un tipo de impuesto.</summary>
public sealed record CreateTaxTypeCommand(
    string Code,
    string Name,
    TaxCalculationType CalculationType,
    decimal Rate,
    string? Description);

/// <summary>
/// Datos editables de un tipo de impuesto; el método de cálculo se omite porque es inmutable.
/// </summary>
public sealed record UpdateTaxTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string? Description);

/// <summary>Proyección administrativa de un tipo de impuesto.</summary>
public sealed record TaxTypeModel(
    Guid Id,
    string Code,
    string Name,
    TaxCalculationType CalculationType,
    decimal Rate,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Opción activa para el selector de impuestos de una línea de factura.</summary>
public sealed record TaxTypeOption(
    Guid Id,
    string Code,
    string Name,
    TaxCalculationType CalculationType,
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

    /// <summary>Crea un tipo de impuesto con método de cálculo fijo.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateTaxTypeCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza los datos que no alteran el método de cálculo.</summary>
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
