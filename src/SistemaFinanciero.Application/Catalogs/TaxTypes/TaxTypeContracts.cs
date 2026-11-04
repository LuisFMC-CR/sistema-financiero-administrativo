using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Application.Catalogs.TaxTypes;

/// <summary>Datos requeridos para crear una tarifa de IVA.</summary>
/// <remarks>
/// Conexiones: lo recibe <c>ITaxTypeService.CreateAsync</c>, implementado por <c>TaxTypeService</c>
/// (Infrastructure/Catalogs/TaxTypes). Lo consume <c>TaxesAndWithholdingsController</c> (Web, ruta
/// <c>/impuestos-y-retenciones/tarifas/crear</c>).
/// </remarks>
public sealed record CreateTaxTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string HaciendaRateCode,
    string? Description);

/// <summary>
/// Datos editables de una tarifa de IVA. <see cref="HaciendaRateCode"/> es nulo únicamente cuando se
/// edita la tarifa de sistema "No sujeto" (bloque 1 del paquete v7); cualquier otra siempre lo exige.
/// </summary>
public sealed record UpdateTaxTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string? HaciendaRateCode,
    string? Description);

/// <summary>Proyección administrativa de una tarifa de IVA.</summary>
public sealed record TaxTypeModel(
    Guid Id,
    string Code,
    string Name,
    decimal Rate,
    string? HaciendaRateCode,
    string? Description,
    bool IsActive,
    TaxTypeSystemRole? SystemRole,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Opción activa para el selector de IVA de una línea de factura.</summary>
public sealed record TaxTypeOption(
    Guid Id,
    string Code,
    string Name,
    decimal Rate,
    TaxTypeSystemRole? SystemRole);

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
}
