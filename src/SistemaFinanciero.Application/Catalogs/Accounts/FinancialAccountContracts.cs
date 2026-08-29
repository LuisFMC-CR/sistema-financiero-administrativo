using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Application.Catalogs.Accounts;

/// <summary>Datos requeridos para crear una cuenta financiera.</summary>
public sealed record CreateFinancialAccountCommand(
    string Code,
    string Name,
    FinancialAccountType Type,
    CurrencyCode Currency,
    string? Reference);

/// <summary>
/// Datos editables de una cuenta existente; tipo y moneda se omiten porque son inmutables.
/// </summary>
public sealed record UpdateFinancialAccountCommand(
    string Code,
    string Name,
    string? Reference);

/// <summary>Proyección administrativa de una caja o cuenta bancaria.</summary>
public sealed record FinancialAccountModel(
    Guid Id,
    string Code,
    string Name,
    FinancialAccountType Type,
    CurrencyCode Currency,
    string? Reference,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version);

/// <summary>Casos de uso del catálogo de cuentas financieras.</summary>
public interface IFinancialAccountService
{
    /// <summary>Busca cuentas financieras con filtros y paginación.</summary>
    public Task<PagedResult<FinancialAccountModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una cuenta financiera por identificador.</summary>
    public Task<FinancialAccountModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Crea una caja o cuenta bancaria con moneda fija.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateFinancialAccountCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza únicamente los datos que no alteran tipo o moneda.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateFinancialAccountCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva una cuenta sin eliminarla ni modificar saldos.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
