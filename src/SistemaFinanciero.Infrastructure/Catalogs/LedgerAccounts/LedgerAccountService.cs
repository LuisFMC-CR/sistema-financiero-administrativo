using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.LedgerAccounts;

/// <summary>
/// Ejecuta los casos de uso y las reglas jerárquicas del catálogo de cuentas contables, sin almacenar
/// saldos.
/// </summary>
internal sealed class LedgerAccountService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ILedgerAccountService
{
    private const string DuplicateCodeMessage = "Ya existe una cuenta contable con el mismo código.";

    public async Task<PagedResult<LedgerAccountModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<LedgerAccount> accounts = dbContext.Set<LedgerAccount>().AsNoTracking();

        accounts = query.Status switch
        {
            CatalogStatusFilter.Active => accounts.Where(account => account.IsActive),
            CatalogStatusFilter.Inactive => accounts.Where(account => !account.IsActive),
            CatalogStatusFilter.All => accounts,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            accounts = accounts.Where(account =>
                account.Code.Contains(search) ||
                account.Name.Contains(search) ||
                (account.Reference != null && account.Reference.Contains(search)) ||
                (account.Description != null && account.Description.Contains(search)));
        }

        PagedResult<LedgerAccount> page = await CatalogPersistence.ToPageAsync(
            accounts
                .OrderBy(account => account.Type)
                .ThenBy(account => account.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        IReadOnlyDictionary<Guid, string> parentNames = await GetParentNamesAsync(
            page.Items,
            cancellationToken);

        return new PagedResult<LedgerAccountModel>(
            page.Items.Select(account => Map(account, parentNames)).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<LedgerAccountModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (account is null)
        {
            return null;
        }

        IReadOnlyDictionary<Guid, string> parentNames = await GetParentNamesAsync(
            [account],
            cancellationToken);

        return Map(account, parentNames);
    }

    public async Task<IReadOnlyList<LedgerAccountOption>> GetParentOptionsAsync(
        LedgerAccountType type,
        Guid? excludedAccountId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        AccountNode[] nodes = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Type == type)
            .Select(account => new AccountNode(
                account.Id,
                account.ParentId,
                account.Code,
                account.Name,
                account.IsActive,
                account.CashKind != null))
            .ToArrayAsync(cancellationToken);

        HashSet<Guid> excludedIds = [];

        if (excludedAccountId is Guid excludedId)
        {
            excludedIds = FindDescendants(nodes, excludedId);
            excludedIds.Add(excludedId);
        }

        return nodes
            .Where(node => node.IsActive && !node.IsCash && !excludedIds.Contains(node.Id))
            .OrderBy(node => node.Code)
            .Select(node => new LedgerAccountOption(node.Id, node.Code, node.Name))
            .ToArray();
    }

    public async Task<IReadOnlyList<LedgerAccountOption>> GetActiveOptionsAsync(
        LedgerAccountType type,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Type == type && account.IsActive)
            .OrderBy(account => account.Code)
            .Select(account => new LedgerAccountOption(account.Id, account.Code, account.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CashAccountOption>> GetActiveCashAccountsAsync(
        CurrencyCode? currency = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<LedgerAccount> accounts = dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.IsActive && account.CashKind != null);

        if (currency is CurrencyCode currencyFilter)
        {
            accounts = accounts.Where(account => account.Currency == currencyFilter);
        }

        List<LedgerAccount> stored = await accounts
            .OrderBy(account => account.Currency)
            .ThenBy(account => account.Name)
            .ToListAsync(cancellationToken);

        return stored
            .Select(account => new CashAccountOption(
                account.Id,
                account.Code,
                account.Name,
                account.CashKind!.Value,
                account.Currency!.Value))
            .ToArray();
    }

    public async Task<CatalogOperationResult> CreateAsync(
        CreateLedgerAccountCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogOperationResult? parentValidation = await ValidateParentAsync(
            command.ParentId,
            command.Type,
            currentAccountId: null,
            cancellationToken);

        if (parentValidation is not null)
        {
            return parentValidation;
        }

        try
        {
            LedgerAccount account = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Type,
                command.ParentId,
                command.CashKind,
                command.Currency,
                command.Reference,
                command.Description,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(account);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                DuplicateCodeMessage,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> UpdateAsync(
        Guid id,
        UpdateLedgerAccountCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (account is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        CatalogOperationResult? parentValidation = await ValidateParentAsync(
            command.ParentId,
            account.Type,
            id,
            cancellationToken);

        if (parentValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return parentValidation;
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            account.UpdateDetails(
                command.Code,
                command.Name,
                command.Reference,
                command.Description,
                now,
                actorId);
            account.ChangeParent(command.ParentId, now, actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                DuplicateCodeMessage,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<CatalogOperationResult> SetActiveAsync(
        Guid id,
        bool isActive,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (account is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        if (isActive)
        {
            CatalogOperationResult? parentValidation = await ValidateParentAsync(
                account.ParentId,
                account.Type,
                account.Id,
                cancellationToken);

            if (parentValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return parentValidation;
            }
        }
        else
        {
            bool hasActiveChildren = await dbContext.Set<LedgerAccount>()
                .AsNoTracking()
                .AnyAsync(
                    candidate => candidate.ParentId == id && candidate.IsActive,
                    cancellationToken);

            if (hasActiveChildren)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Dependency(
                    "Desactive o reasigne primero las cuentas hijas activas.");
            }

            bool isUsedByActiveCategory = await dbContext.Set<FinancialCategory>()
                .AsNoTracking()
                .AnyAsync(
                    category => category.LedgerAccountId == id && category.IsActive,
                    cancellationToken);

            if (isUsedByActiveCategory)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Dependency(
                    "La cuenta está asignada a categorías financieras activas. Reasígnelas o desactívelas primero.");
            }
        }

        try
        {
            if (isActive)
            {
                account.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                account.Deactivate(timeProvider.GetUtcNow(), actorId);
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible cambiar el estado de la cuenta contable.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private async Task<CatalogOperationResult?> ValidateParentAsync(
        Guid? parentId,
        LedgerAccountType type,
        Guid? currentAccountId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        if (parentId == currentAccountId)
        {
            return CatalogOperationResult.Invalid("Una cuenta no puede ser su propia cuenta superior.");
        }

        LedgerAccount? parent = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == parentId.Value, cancellationToken);

        if (parent is null || !parent.IsActive)
        {
            return CatalogOperationResult.Invalid("La cuenta superior seleccionada no existe o está inactiva.");
        }

        if (parent.Type != type)
        {
            return CatalogOperationResult.Invalid("La cuenta superior debe ser del mismo tipo.");
        }

        if (parent.IsCash)
        {
            return CatalogOperationResult.Invalid("Una cuenta de efectivo no puede tener cuentas hijas.");
        }

        if (currentAccountId is null)
        {
            return null;
        }

        AccountLink[] links = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Select(account => new AccountLink(account.Id, account.ParentId))
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, Guid?> parentById = links.ToDictionary(link => link.Id, link => link.ParentId);
        HashSet<Guid> visited = [];
        Guid? cursor = parentId;

        while (cursor is Guid cursorId)
        {
            if (cursorId == currentAccountId.Value)
            {
                return CatalogOperationResult.Invalid(
                    "La cuenta superior seleccionada produciría un ciclo en la jerarquía.");
            }

            if (!visited.Add(cursorId) || !parentById.TryGetValue(cursorId, out cursor))
            {
                break;
            }
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetParentNamesAsync(
        IReadOnlyList<LedgerAccount> accounts,
        CancellationToken cancellationToken)
    {
        Guid[] parentIds = accounts
            .Where(account => account.ParentId.HasValue)
            .Select(account => account.ParentId!.Value)
            .Distinct()
            .ToArray();

        if (parentIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => parentIds.Contains(account.Id))
            .ToDictionaryAsync(
                account => account.Id,
                account => account.Name,
                cancellationToken);
    }

    private static HashSet<Guid> FindDescendants(IReadOnlyList<AccountNode> nodes, Guid parentId)
    {
        Dictionary<Guid, Guid[]> childrenByParent = nodes
            .Where(node => node.ParentId.HasValue)
            .GroupBy(node => node.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(node => node.Id).ToArray());

        HashSet<Guid> descendants = [];
        Queue<Guid> pending = new();
        pending.Enqueue(parentId);

        while (pending.Count > 0)
        {
            Guid current = pending.Dequeue();

            if (!childrenByParent.TryGetValue(current, out Guid[]? children))
            {
                continue;
            }

            foreach (Guid child in children)
            {
                if (descendants.Add(child))
                {
                    pending.Enqueue(child);
                }
            }
        }

        return descendants;
    }

    private static LedgerAccountModel Map(
        LedgerAccount account,
        IReadOnlyDictionary<Guid, string> parentNames)
    {
        return new LedgerAccountModel(
            account.Id,
            account.Code,
            account.Name,
            account.Type,
            account.ParentId,
            account.ParentId is Guid parentId && parentNames.TryGetValue(parentId, out string? parentName)
                ? parentName
                : null,
            account.CashKind,
            account.Currency,
            account.Reference,
            account.Description,
            account.IsActive,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(account.RowVersion));
    }

    private sealed record AccountNode(
        Guid Id,
        Guid? ParentId,
        string Code,
        string Name,
        bool IsActive,
        bool IsCash);

    private sealed record AccountLink(Guid Id, Guid? ParentId);
}
