using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.LedgerAccounts;

/// <summary>
/// Ejecuta los casos de uso y las reglas jerárquicas del catálogo de cuentas contables, sin almacenar
/// saldos.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>ILedgerAccountService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyectan <c>LedgerAccountsController</c> y
/// <c>FinancialCategoriesController</c> (Web, este último solo para los selectores). Lee y escribe
/// <c>FinancialDbContext.LedgerAccounts</c> (tabla <c>catalogos.CuentasContables</c>) y consulta
/// <c>FinancialCategories</c> al desactivar, para no dejar una cuenta usada por una categoría activa.
/// <see cref="CreateAsync"/> calcula el código jerárquico (regla 12) como el siguiente hijo libre del
/// padre, con reintento ante colisiones de concurrencia sobre el índice único de código.
/// </remarks>
internal sealed class LedgerAccountService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ILedgerAccountService
{
    private const string DuplicateCodeMessage = "Ya existe una cuenta contable con el mismo código.";
    private const int MaxCodeGenerationAttempts = 5;

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
        CancellationToken cancellationToken = default)
    {
        List<LedgerAccount> stored = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.IsActive && account.CashKind != null)
            .OrderBy(account => account.Name)
            .ToListAsync(cancellationToken);

        return stored
            .Select(account => new CashAccountOption(
                account.Id,
                account.Code,
                account.Name,
                account.CashKind!.Value))
            .ToArray();
    }

    public async Task<CatalogOperationResult> CreateAsync(
        CreateLedgerAccountCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        LedgerAccount? parent = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == command.ParentId, cancellationToken);

        if (parent is null || !parent.IsActive)
        {
            return CatalogOperationResult.Invalid("La cuenta superior seleccionada no existe o está inactiva.");
        }

        if (parent.IsCash)
        {
            return CatalogOperationResult.Invalid("Una cuenta de efectivo no puede tener cuentas hijas.");
        }

        for (int attempt = 1; attempt <= MaxCodeGenerationAttempts; attempt++)
        {
            string code = await ComputeNextChildCodeAsync(parent, cancellationToken);

            try
            {
                LedgerAccount account = new(
                    Guid.NewGuid(),
                    code,
                    command.Name,
                    parent.Type,
                    parent.Id,
                    command.CashKind,
                    command.Reference,
                    command.Description,
                    timeProvider.GetUtcNow(),
                    actorId);

                dbContext.Add(account);

                CatalogOperationResult result = await CatalogPersistence.SaveChangesAsync(
                    dbContext,
                    DuplicateCodeMessage,
                    cancellationToken);

                if (result.Status != CatalogOperationStatus.Duplicate)
                {
                    return result;
                }

                // Otra transacción generó el mismo código al mismo tiempo: se recalcula y reintenta.
                dbContext.ChangeTracker.Clear();
            }
            catch (ArgumentException exception)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Invalid(exception.Message);
            }
        }

        return CatalogOperationResult.Invalid(
            "No fue posible generar un código único para la cuenta; intente nuevamente.");
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

        if (account.IsProtected)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Una cuenta de grupo del catálogo base no se puede modificar.");
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            account.UpdateDetails(
                command.Name,
                command.Reference,
                command.Description,
                timeProvider.GetUtcNow(),
                actorId);

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

        if (account.IsProtected)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Una cuenta de grupo del catálogo base no se puede desactivar.");
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        if (!isActive)
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
        else if (account.ParentId is Guid parentId)
        {
            bool parentIsActive = await dbContext.Set<LedgerAccount>()
                .AsNoTracking()
                .AnyAsync(candidate => candidate.Id == parentId && candidate.IsActive, cancellationToken);

            if (!parentIsActive)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Invalid(
                    "No se puede reactivar una cuenta cuya cuenta superior está inactiva.");
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

    private async Task<string> ComputeNextChildCodeAsync(
        LedgerAccount parent,
        CancellationToken cancellationToken)
    {
        string prefix = parent.Code + ".";

        List<string> siblingCodes = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.ParentId == parent.Id)
            .Select(account => account.Code)
            .ToListAsync(cancellationToken);

        int nextSuffix = siblingCodes
            .Where(code => code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(code => int.TryParse(code[prefix.Length..], out int value) ? value : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{parent.Code}.{nextSuffix:D2}";
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
            account.Reference,
            account.Description,
            account.IsActive,
            account.IsProtected,
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
}
