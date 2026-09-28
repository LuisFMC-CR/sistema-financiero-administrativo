using SistemaFinanciero.Application.Catalogs.Common;

namespace SistemaFinanciero.Application.Catalogs.WithholdingTypes;

/// <summary>Datos requeridos para crear un tipo de retención.</summary>
public sealed record CreateWithholdingTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string? Description);

/// <summary>Datos editables de un tipo de retención.</summary>
public sealed record UpdateWithholdingTypeCommand(
    string Code,
    string Name,
    decimal Rate,
    string? Description);

/// <summary>Proyección administrativa de un tipo de retención.</summary>
public sealed record WithholdingTypeModel(
    Guid Id,
    string Code,
    string Name,
    decimal Rate,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Opción activa para el selector de retenciones de un abono o un pago.</summary>
public sealed record WithholdingTypeOption(Guid Id, string Code, string Name, decimal Rate);

/// <summary>Casos de uso del catálogo de tipos de retención.</summary>
public interface IWithholdingTypeService
{
    /// <summary>Busca tipos de retención con filtros y paginación.</summary>
    public Task<PagedResult<WithholdingTypeModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene un tipo de retención por identificador.</summary>
    public Task<WithholdingTypeModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista los tipos de retención activos para un selector.</summary>
    public Task<IReadOnlyList<WithholdingTypeOption>> GetActiveOptionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Crea un tipo de retención.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateWithholdingTypeCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza un tipo de retención existente.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateWithholdingTypeCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva un tipo de retención sin afectar abonos o pagos existentes.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
