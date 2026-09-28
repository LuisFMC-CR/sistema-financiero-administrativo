using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.Application.Catalogs.LedgerAccounts;

/// <summary>
/// Datos requeridos para crear una cuenta contable. Una cuenta de efectivo indica subtipo y moneda.
/// </summary>
public sealed record CreateLedgerAccountCommand(
    string Code,
    string Name,
    LedgerAccountType Type,
    Guid? ParentId,
    CashAccountKind? CashKind,
    CurrencyCode? Currency,
    string? Reference,
    string? Description);

/// <summary>
/// Datos editables de una cuenta existente; tipo, subtipo de efectivo y moneda se omiten porque son
/// inmutables.
/// </summary>
public sealed record UpdateLedgerAccountCommand(
    string Code,
    string Name,
    Guid? ParentId,
    string? Reference,
    string? Description);

/// <summary>Proyección administrativa de una cuenta contable.</summary>
public sealed record LedgerAccountModel(
    Guid Id,
    string Code,
    string Name,
    LedgerAccountType Type,
    Guid? ParentId,
    string? ParentName,
    CashAccountKind? CashKind,
    CurrencyCode? Currency,
    string? Reference,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string Version)
{
    /// <summary>Indica si es una cuenta de efectivo.</summary>
    public bool IsCash => CashKind is not null;
}

/// <summary>Opción activa para selectores de cuenta contable.</summary>
public sealed record LedgerAccountOption(Guid Id, string Code, string Name);

/// <summary>Opción activa para elegir una caja o un banco en ingresos y pagos.</summary>
public sealed record CashAccountOption(
    Guid Id,
    string Code,
    string Name,
    CashAccountKind CashKind,
    CurrencyCode Currency);

/// <summary>Casos de uso del catálogo de cuentas contables jerárquicas.</summary>
public interface ILedgerAccountService
{
    /// <summary>Busca cuentas contables con filtros y paginación.</summary>
    public Task<PagedResult<LedgerAccountModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>Obtiene una cuenta contable por identificador.</summary>
    public Task<LedgerAccountModel?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista las cuentas activas que pueden ser cuenta superior de otra del tipo indicado: del mismo
    /// tipo y que no sean de efectivo. Excluye la cuenta indicada y sus descendientes.
    /// </summary>
    public Task<IReadOnlyList<LedgerAccountOption>> GetParentOptionsAsync(
        LedgerAccountType type,
        Guid? excludedAccountId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Lista las cuentas activas del tipo indicado, por ejemplo para las categorías.</summary>
    public Task<IReadOnlyList<LedgerAccountOption>> GetActiveOptionsAsync(
        LedgerAccountType type,
        CancellationToken cancellationToken = default);

    /// <summary>Lista las cajas y bancos activos, opcionalmente de una sola moneda.</summary>
    public Task<IReadOnlyList<CashAccountOption>> GetActiveCashAccountsAsync(
        CurrencyCode? currency = null,
        CancellationToken cancellationToken = default);

    /// <summary>Crea una cuenta contable.</summary>
    public Task<CatalogOperationResult> CreateAsync(
        CreateLedgerAccountCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Actualiza una cuenta y valida que la jerarquía permanezca acíclica.</summary>
    public Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateLedgerAccountCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);

    /// <summary>Activa o desactiva una cuenta respetando sus dependencias activas.</summary>
    public Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default);
}
