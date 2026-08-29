using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.Application.Catalogs.Categories;

/// <summary>Datos editables de una categoría financiera.</summary>
public sealed record SaveFinancialCategoryCommand(
    string Code,
    string Name,
    FinancialCategoryKind Kind,
    Guid? ParentId,
    string? Description);

/// <summary>Proyección administrativa de una categoría financiera.</summary>
public sealed record FinancialCategoryModel(
    Guid Id,
    string Code,
    string Name,
    FinancialCategoryKind Kind,
    Guid? ParentId,
    string? ParentName,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Opción activa para selectores de categoría.</summary>
public sealed record FinancialCategoryOption(Guid Id, string Code, string Name);

/// <summary>Casos de uso de categorías financieras jerárquicas.</summary>
public interface IFinancialCategoryService
{
    /// <summary>Busca categorías financieras con filtros y paginación.</summary>
    public Task<PagedResult<FinancialCategoryModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una categoría por identificador.</summary>
    public Task<FinancialCategoryModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Lista categorías activas del tipo indicado para un selector.</summary>
    public Task<IReadOnlyList<FinancialCategoryOption>> GetActiveOptionsAsync(
        FinancialCategoryKind kind,
        Guid? excludedCategoryId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Crea una categoría financiera.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        SaveFinancialCategoryCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza una categoría y valida que la jerarquía permanezca acíclica.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        SaveFinancialCategoryCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva una categoría respetando sus dependencias activas.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
