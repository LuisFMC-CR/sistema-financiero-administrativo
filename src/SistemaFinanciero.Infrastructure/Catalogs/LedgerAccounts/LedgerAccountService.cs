using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Ledger;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.LedgerAccounts;

/// <summary>
/// Ejecuta los casos de uso y las reglas jerárquicas del catálogo contable. El saldo de cada cuenta
/// no se almacena: se calcula desde el libro mayor y se acumula hacia las cuentas superiores.
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>ILedgerAccountService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyecta <c>LedgerAccountsController</c> (Web).
/// Lee y escribe <c>catalogos.CuentasContables</c> y consulta <c>finanzas.AsientoLineas</c> para el
/// saldo actual. Solo las cuentas de movimiento (tercer nivel) se crean, editan o desactivan; las
/// raíces y los grupos son acumuladores protegidos.
/// </remarks>
internal sealed class LedgerAccountService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : ILedgerAccountService
{
    private const string DuplicateCodeMessage = "Ya existe una cuenta contable con el mismo código.";
    private const int MaxCodeGenerationAttempts = 5;

    public async Task<PagedResult<LedgerAccountModel>> SearchAsync(
        LedgerAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<LedgerAccount> accounts = dbContext.Set<LedgerAccount>().AsNoTracking();

        if (query.Code is not null)
        {
            string code = query.Code;
            accounts = accounts.Where(account => account.Code.Contains(code));
        }

        if (query.Name is not null)
        {
            string name = query.Name;
            accounts = accounts.Where(account =>
                account.Name.Contains(name) ||
                (account.Description != null && account.Description.Contains(name)) ||
                (account.Reference != null && account.Reference.Contains(name)));
        }

        if (query.Type is LedgerAccountType type)
        {
            accounts = accounts.Where(account => account.Type == type);
        }

        if (query.IsMovement is bool isMovement)
        {
            accounts = accounts.Where(account => account.IsMovement == isMovement);
        }

        if (query.IsActive is bool isActive)
        {
            accounts = accounts.Where(account => account.IsActive == isActive);
        }

        PagedResult<LedgerAccount> page = await CatalogPersistence.ToPageAsync(
            accounts.OrderBy(account => account.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        IReadOnlyDictionary<Guid, AccountBalance> balances = await GetBalancesAsync(cancellationToken);
        IReadOnlyDictionary<Guid, LedgerAccount> parents = await GetParentsAsync(
            page.Items,
            cancellationToken);

        return new PagedResult<LedgerAccountModel>(
            page.Items.Select(account => Map(
                account,
                account.ParentId is Guid parentId ? parents.GetValueOrDefault(parentId) : null,
                balances.GetValueOrDefault(account.Id))).ToArray(),
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

        IReadOnlyDictionary<Guid, AccountBalance> balances = await GetBalancesAsync(cancellationToken);
        LedgerAccount? parent = account.ParentId is Guid parentId
            ? await dbContext.Set<LedgerAccount>()
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == parentId, cancellationToken)
            : null;

        return Map(account, parent, balances.GetValueOrDefault(account.Id));
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

        List<LedgerAccount> nodes = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Type == type)
            .ToListAsync(cancellationToken);

        HashSet<Guid> excludedIds = [];

        if (excludedAccountId is Guid excludedId)
        {
            excludedIds = FindDescendants(nodes, excludedId);
            excludedIds.Add(excludedId);
        }

        return nodes
            .Where(node => node.IsActive
                && !node.IsCash
                && node.SystemRole is null
                && !excludedIds.Contains(node.Id)
                && LedgerAccountGroups.DetailParentCodes.Contains(node.Code))
            .OrderBy(node => node.Code, StringComparer.Ordinal)
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

        // Solo cuentas de movimiento: las acumuladoras no admiten facturar ni registrar asientos.
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.Type == type
                && account.IsActive
                && account.IsMovement
                && account.SystemRole == null)
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

    public async Task<IReadOnlyList<LedgerAccountOption>> GetDetailAccountOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => account.IsMovement)
            .OrderBy(account => account.Code)
            .Select(account => new LedgerAccountOption(account.Id, account.Code, account.Name))
            .ToListAsync(cancellationToken);
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

        if (parent.SystemRole is not null)
        {
            return CatalogOperationResult.Invalid("Una cuenta de sistema no puede tener cuentas hijas.");
        }

        if (!LedgerAccountGroups.DetailParentCodes.Contains(parent.Code))
        {
            return CatalogOperationResult.Invalid(
                "Las cuentas nuevas solo se crean dentro de un grupo de segundo nivel del catálogo contable.");
        }

        if (parent.Type == LedgerAccountType.Asset && command.CashKind is null)
        {
            return CatalogOperationResult.Invalid(
                "Una cuenta nueva dentro de Activo corriente debe indicar si es caja o banco.");
        }

        if (parent.Type != LedgerAccountType.Asset && command.CashKind is not null)
        {
            return CatalogOperationResult.Invalid(
                "Solo una cuenta de tipo Activo puede ser de efectivo.");
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

        if (!account.IsMovement || account.IsProtected)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Solo se pueden modificar las cuentas de movimiento (último nivel); las cuentas acumuladoras y los títulos no se editan.");
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

        if (!account.IsMovement || account.IsProtected)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(
                "Las cuentas acumuladoras y los títulos no se pueden activar ni desactivar.");
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        if (!isActive)
        {
            bool hasTransactions = await dbContext.Set<JournalEntryLine>()
                .AsNoTracking()
                .AnyAsync(line => line.AccountId == id, cancellationToken);

            if (hasTransactions)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Dependency(
                    "La cuenta tiene movimientos asociados en el libro mayor y no se puede desactivar.");
            }

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

    private async Task<IReadOnlyDictionary<Guid, LedgerAccount>> GetParentsAsync(
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
            return new Dictionary<Guid, LedgerAccount>();
        }

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => parentIds.Contains(account.Id))
            .ToDictionaryAsync(account => account.Id, cancellationToken);
    }

    /// <summary>
    /// Calcula el saldo de todas las cuentas desde <c>finanzas.AsientoLineas</c>. El saldo propio usa
    /// la naturaleza de la cuenta (activo/gasto: débito menos crédito; pasivo/patrimonio/ingreso:
    /// crédito menos débito) y luego se acumula hacia las cuentas superiores.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, AccountBalance>> GetBalancesAsync(
        CancellationToken cancellationToken)
    {
        List<AccountNode> accounts = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Select(account => new AccountNode(
                account.Id,
                account.ParentId,
                account.Code,
                account.Type))
            .ToListAsync(cancellationToken);

        var lines = await dbContext.Set<JournalEntryLine>()
            .AsNoTracking()
            .GroupBy(line => line.AccountId)
            .Select(group => new
            {
                AccountId = group.Key,
                Debit = group.Sum(line => line.Debit),
                Credit = group.Sum(line => line.Credit),
            })
            .ToListAsync(cancellationToken);

        Dictionary<Guid, AccountBalance> balances = accounts.ToDictionary(
            account => account.Id,
            account => new AccountBalance(0m, false));

        foreach (var line in lines)
        {
            AccountNode? account = accounts.FirstOrDefault(candidate => candidate.Id == line.AccountId);

            if (account is null)
            {
                continue;
            }

            decimal signed = account.Type is LedgerAccountType.Asset or LedgerAccountType.Expense
                ? line.Debit - line.Credit
                : line.Credit - line.Debit;

            balances[account.Id] = new AccountBalance(signed, true);
        }

        foreach (AccountNode account in accounts.OrderByDescending(candidate => candidate.Code.Length))
        {
            if (account.ParentId is not Guid parentId
                || !balances.TryGetValue(parentId, out AccountBalance? parentBalance))
            {
                continue;
            }

            balances[account.Id] = balances[account.Id] with
            {
                Balance = balances[account.Id].Balance + 0m,
            };

            balances[parentId] = parentBalance with
            {
                Balance = parentBalance.Balance + balances[account.Id].Balance,
                HasTransactions = parentBalance.HasTransactions || balances[account.Id].HasTransactions,
            };
        }

        return balances;
    }

    private static HashSet<Guid> FindDescendants(IReadOnlyList<LedgerAccount> nodes, Guid parentId)
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
        LedgerAccount? parent,
        AccountBalance? balance)
    {
        return new LedgerAccountModel(
            account.Id,
            account.Code,
            account.Name,
            account.Type,
            account.ParentId,
            parent?.Code,
            parent?.Name,
            account.CashKind,
            account.Reference,
            account.Description,
            account.IsActive,
            account.IsProtected,
            account.IsMovement,
            account.SystemRole,
            balance?.Balance ?? 0m,
            balance?.HasTransactions ?? false,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(account.RowVersion));
    }

    private sealed record AccountNode(
        Guid Id,
        Guid? ParentId,
        string Code,
        LedgerAccountType Type);

    private sealed record AccountBalance(decimal Balance, bool HasTransactions);
}
