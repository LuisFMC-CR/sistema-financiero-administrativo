using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Catalogs.Accounts;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Catalogs.Accounts;

/// <summary>
/// Ejecuta los casos de uso de cajas y cuentas bancarias sin almacenar saldos editables.
/// </summary>
internal sealed class FinancialAccountService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IFinancialAccountService
{
    public async Task<PagedResult<FinancialAccountModel>> SearchAsync(
        CatalogQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQueryable<FinancialAccount> accounts = dbContext.Set<FinancialAccount>().AsNoTracking();

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
                (account.Reference != null && account.Reference.Contains(search)));
        }

        PagedResult<FinancialAccount> page = await CatalogPersistence.ToPageAsync(
            accounts
                .OrderBy(account => account.Currency)
                .ThenBy(account => account.Name)
                .ThenBy(account => account.Code),
            query.Page,
            query.PageSize,
            cancellationToken);

        return new PagedResult<FinancialAccountModel>(
            page.Items.Select(Map).ToArray(),
            page.Page,
            page.PageSize,
            page.TotalCount);
    }

    public async Task<FinancialAccountModel?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        FinancialAccount? account = await dbContext.Set<FinancialAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        return account is null ? null : Map(account);
    }

    public async Task<CatalogOperationResult> CreateAsync(
        CreateFinancialAccountCommand command,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            FinancialAccount account = new(
                Guid.NewGuid(),
                command.Code,
                command.Name,
                command.Type,
                command.Currency,
                command.Reference,
                timeProvider.GetUtcNow(),
                actorId);

            dbContext.Add(account);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe una cuenta financiera con el mismo código.",
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
        UpdateFinancialAccountCommand command,
        string version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        FinancialAccount? account = await dbContext.Set<FinancialAccount>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (account is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
        }

        try
        {
            account.UpdateDetails(
                command.Code,
                command.Name,
                command.Reference,
                timeProvider.GetUtcNow(),
                actorId);

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe una cuenta financiera con el mismo código.",
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
        FinancialAccount? account = await dbContext.Set<FinancialAccount>()
            .SingleOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (account is null)
        {
            return CatalogOperationResult.Missing();
        }

        if (!CatalogPersistence.TrySetOriginalVersion(dbContext, account, version))
        {
            return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
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
                "No fue posible cambiar el estado de la cuenta financiera.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    private static FinancialAccountModel Map(FinancialAccount account)
    {
        return new FinancialAccountModel(
            account.Id,
            account.Code,
            account.Name,
            account.Type,
            account.Currency,
            account.Reference,
            account.IsActive,
            account.CreatedAtUtc,
            account.UpdatedAtUtc,
            CatalogPersistence.EncodeVersion(account.RowVersion));
    }
}
