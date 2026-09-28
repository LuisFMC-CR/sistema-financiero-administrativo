using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.Categories;

/// <summary>
/// Ejecuta los casos de uso y las reglas jerárquicas de categorías financieras.
/// </summary>
internal sealed class FinancialCategoryService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IFinancialCategoryService
{
    public async Task<PagedResult<FinancialCategoryModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<FinancialCategory> categories = dbContext.Set<FinancialCategory>().AsNoTracking();

        categories = query.Status switch
        {
            CatalogStatusFilter.Active => categories.Where(category => category.IsActive),
            CatalogStatusFilter.Inactive => categories.Where(category => !category.IsActive),
            CatalogStatusFilter.All => categories,
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        if (query.Search is not null)
        {
            string search = query.Search;
            categories = categories.Where(category =>
                category.Code.Contains(search) ||
                category.Name.Contains(search) ||
                (category.Description != null && category.Description.Contains(search)));
        }

        PagedResult<FinancialCategory> page = await CatalogPersistence.ToPageAsync(
            categories
                .OrderBy(category => category.Kind)
                .ThenBy(category => category.Name)
                .ThenBy(category => category.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        IReadOnlyDictionary<Guid, string> parentNames = await GetParentNamesAsync(
            page.Items,
            cancellationToken);
        IReadOnlyDictionary<Guid, LedgerAccountLabel> ledgerAccounts = await GetLedgerAccountLabelsAsync(
            page.Items,
            cancellationToken);

        return new PagedResult<FinancialCategoryModel>(
            page.Items.Select(category => Map(category, parentNames, ledgerAccounts)).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<FinancialCategoryModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        FinancialCategory? category = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (category is null)
        {
            return null;
        }

        IReadOnlyDictionary<Guid, string> parentNames = await GetParentNamesAsync(
            [category],
            cancellationToken);
        IReadOnlyDictionary<Guid, LedgerAccountLabel> ledgerAccounts = await GetLedgerAccountLabelsAsync(
            [category],
            cancellationToken);

        return Map(category, parentNames, ledgerAccounts);
    }

    public async Task<IReadOnlyList<FinancialCategoryOption>> GetActiveOptionsAsync(
        FinancialCategoryKind kind,
        Guid? excludedCategoryId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        CategoryNode[] nodes = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => category.Kind == kind)
            .Select(category => new CategoryNode(
                category.Id,
                category.ParentId,
                category.Code,
                category.Name,
                category.IsActive))
            .ToArrayAsync(cancellationToken);

        HashSet<Guid> excludedIds = excludedCategoryId is Guid excludedId
            ? FindDescendants(nodes, excludedId)
            : [];

        if (excludedCategoryId is Guid currentId)
        {
            excludedIds.Add(currentId);
        }

        return nodes
            .Where(node => node.IsActive && !excludedIds.Contains(node.Id))
            .OrderBy(node => node.Name)
            .ThenBy(node => node.Code)
            .Select(node => new FinancialCategoryOption(node.Id, node.Code, node.Name))
            .ToArray();
    }

    public async Task<CatalogOperationResult> CreateAsync(
        SaveFinancialCategoryCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogOperationResult? parentValidation = await ValidateParentAsync(
            command.ParentId,
            command.Kind,
            currentCategoryId: null,
            cancellationToken);

        if (parentValidation is not null)
        {
            return parentValidation;
        }

        CatalogOperationResult? ledgerAccountValidation = await ValidateLedgerAccountAsync(
            command.LedgerAccountId,
            command.Kind,
            cancellationToken);

        if (ledgerAccountValidation is not null)
        {
            return ledgerAccountValidation;
        }

        try
        {
            FinancialCategory category = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Kind,
                command.ParentId,
                command.LedgerAccountId,
                command.Description,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(category);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe una categoría financiera con el mismo código.",
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
        SaveFinancialCategoryCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        FinancialCategory? category = await dbContext.Set<FinancialCategory>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (category is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (category.Kind != command.Kind)
        {
            return CatalogOperationResult.Invalid(
                "La naturaleza de una categoría existente no puede modificarse.");
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, category, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        CatalogOperationResult? parentValidation = await ValidateParentAsync(
            command.ParentId,
            command.Kind,
            id,
            cancellationToken);

        if (parentValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return parentValidation;
        }

        CatalogOperationResult? ledgerAccountValidation = await ValidateLedgerAccountAsync(
            command.LedgerAccountId,
            command.Kind,
            cancellationToken);

        if (ledgerAccountValidation is not null)
        {
            dbContext.ChangeTracker.Clear();
            return ledgerAccountValidation;
        }

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            category.UpdateDetails(
                command.Code,
                command.Name,
                command.Description,
                now,
                actorId);
            category.ChangeParent(command.ParentId, now, actorId);
            category.ChangeLedgerAccount(command.LedgerAccountId, now, actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe una categoría financiera con el mismo código.",
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
        FinancialCategory? category = await dbContext.Set<FinancialCategory>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (category is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, category, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        if (isActive)
        {
            CatalogOperationResult? parentValidation = await ValidateParentAsync(
                category.ParentId,
                category.Kind,
                category.Id,
                cancellationToken);

            if (parentValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return parentValidation;
            }

            CatalogOperationResult? ledgerAccountValidation = await ValidateLedgerAccountAsync(
                category.LedgerAccountId,
                category.Kind,
                cancellationToken);

            if (ledgerAccountValidation is not null)
            {
                dbContext.ChangeTracker.Clear();
                return ledgerAccountValidation;
            }
        }
        else
        {
            bool hasActiveChildren = await dbContext.Set<FinancialCategory>()
                .AsNoTracking()
                .AnyAsync(
                    candidate => candidate.ParentId == id && candidate.IsActive,
                    cancellationToken);

            if (hasActiveChildren)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Dependency(
                    "Desactive o reasigne primero las categorías hijas activas.");
            }

            bool isUsedByActiveItem = await dbContext.Set<CatalogItem>()
                .AsNoTracking()
                .AnyAsync(
                    item => item.DefaultIncomeCategoryId == id && item.IsActive,
                    cancellationToken);

            if (isUsedByActiveItem)
            {
                dbContext.ChangeTracker.Clear();
                return CatalogOperationResult.Dependency(
                    "La categoría está asignada a productos o servicios activos.");
            }
        }

        try
        {
            if (isActive)
            {
                category.Activate(timeProvider.GetUtcNow(), actorId);
            }
            else
            {
                category.Deactivate(timeProvider.GetUtcNow(), actorId);
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "No fue posible cambiar el estado de la categoría.",
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
        FinancialCategoryKind kind,
        Guid? currentCategoryId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        if (parentId == currentCategoryId)
        {
            return CatalogOperationResult.Invalid("Una categoría no puede ser su propio padre.");
        }

        FinancialCategory? parent = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == parentId.Value, cancellationToken);

        if (parent is null || !parent.IsActive)
        {
            return CatalogOperationResult.Invalid("La categoría padre seleccionada no existe o está inactiva.");
        }

        if (parent.Kind != kind)
        {
            return CatalogOperationResult.Invalid(
                "La categoría padre debe tener la misma naturaleza de ingreso o gasto.");
        }

        if (currentCategoryId is null)
        {
            return null;
        }

        CategoryLink[] links = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Select(category => new CategoryLink(category.Id, category.ParentId))
            .ToArrayAsync(cancellationToken);

        Dictionary<Guid, Guid?> parentById = links.ToDictionary(link => link.Id, link => link.ParentId);
        HashSet<Guid> visited = [];
        Guid? cursor = parentId;

        while (cursor is Guid cursorId)
        {
            if (cursorId == currentCategoryId.Value)
            {
                return CatalogOperationResult.Invalid(
                    "La categoría padre seleccionada produciría un ciclo en la jerarquía.");
            }

            if (!visited.Add(cursorId) || !parentById.TryGetValue(cursorId, out cursor))
            {
                break;
            }
        }

        return null;
    }

    private async Task<CatalogOperationResult?> ValidateLedgerAccountAsync(
        Guid ledgerAccountId,
        FinancialCategoryKind kind,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(kind))
        {
            return CatalogOperationResult.Invalid("La naturaleza de la categoría no es válida.");
        }

        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == ledgerAccountId, cancellationToken);

        if (account is null || !account.IsActive)
        {
            return CatalogOperationResult.Invalid("La cuenta contable seleccionada no existe o está inactiva.");
        }

        if (account.Type != kind.ToLedgerAccountType())
        {
            return CatalogOperationResult.Invalid(kind == FinancialCategoryKind.Income
                ? "Una categoría de ingreso requiere una cuenta contable de tipo Ingreso."
                : "Una categoría de gasto requiere una cuenta contable de tipo Gasto.");
        }

        return null;
    }

    private async Task<IReadOnlyDictionary<Guid, LedgerAccountLabel>> GetLedgerAccountLabelsAsync(
        IReadOnlyList<FinancialCategory> categories,
        CancellationToken cancellationToken)
    {
        Guid[] accountIds = categories
            .Select(category => category.LedgerAccountId)
            .Distinct()
            .ToArray();

        if (accountIds.Length == 0)
        {
            return new Dictionary<Guid, LedgerAccountLabel>();
        }

        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account => accountIds.Contains(account.Id))
            .ToDictionaryAsync(
                account => account.Id,
                account => new LedgerAccountLabel(account.Code, account.Name),
                cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetParentNamesAsync(
        IReadOnlyList<FinancialCategory> categories,
        CancellationToken cancellationToken)
    {
        Guid[] parentIds = categories
            .Where(category => category.ParentId.HasValue)
            .Select(category => category.ParentId!.Value)
            .Distinct()
            .ToArray();

        if (parentIds.Length == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => parentIds.Contains(category.Id))
            .ToDictionaryAsync(
                category => category.Id,
                category => category.Name,
                cancellationToken);
    }

    private static HashSet<Guid> FindDescendants(
        IReadOnlyList<CategoryNode> nodes,
        Guid parentId)
    {
        Dictionary<Guid, Guid[]> childrenByParent = nodes
            .Where(node => node.ParentId.HasValue)
            .GroupBy(node => node.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(node => node.Id).ToArray());

        HashSet<Guid> descendants = [];
        Queue<Guid> pending = new();
        pending.Enqueue(parentId);

        while (pending.TryDequeue(out Guid currentId))
        {
            if (!childrenByParent.TryGetValue(currentId, out Guid[]? children))
            {
                continue;
            }

            foreach (Guid childId in children)
            {
                if (descendants.Add(childId))
                {
                    pending.Enqueue(childId);
                }
            }
        }

        return descendants;
    }

    private static FinancialCategoryModel Map(
        FinancialCategory category,
        IReadOnlyDictionary<Guid, string> parentNames,
        IReadOnlyDictionary<Guid, LedgerAccountLabel> ledgerAccounts)
    {
        string? parentName = category.ParentId is Guid parentId &&
            parentNames.TryGetValue(parentId, out string? foundName)
                ? foundName
                : null;
        LedgerAccountLabel ledgerAccount = ledgerAccounts.TryGetValue(category.LedgerAccountId, out LedgerAccountLabel? label)
            ? label
            : new LedgerAccountLabel(string.Empty, string.Empty);

        return new FinancialCategoryModel(
            category.Id,
            category.Code,
            category.Name,
            category.Kind,
            category.ParentId,
            parentName,
            category.LedgerAccountId,
            ledgerAccount.Code,
            ledgerAccount.Name,
            category.Description,
            category.IsActive,
            category.CreatedAtUtc,
            category.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(category.RowVersion));
    }

    private sealed record LedgerAccountLabel(string Code, string Name);

    private sealed record CategoryLink(Guid Id, Guid? ParentId);

    private sealed record CategoryNode(
        Guid Id,
        Guid? ParentId,
        string Code,
        string Name,
        bool IsActive);
}
