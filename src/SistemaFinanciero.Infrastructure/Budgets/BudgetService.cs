using Microsoft.EntityFrameworkCore;
using SistemaFinanciero.Application.Budgets;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.Budgets;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Infrastructure.Catalogs;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.Infrastructure.Budgets;

/// <summary>
/// Consulta y configura el presupuesto mensual por categoría de gasto de último nivel (regla de
/// negocio 6).
/// </summary>
/// <remarks>
/// Conexiones: implementa <c>IBudgetService</c> (Application); se registra en
/// <c>DependencyInjection.AddInfrastructure</c>; lo inyecta <c>BudgetsController</c> (Web, ruta
/// <c>/presupuesto</c>). Lo ejecutado nunca se guarda: se calcula en caliente a partir de
/// <c>finanzas.Gastos</c> confirmados de la categoría y el mes, para que anular un gasto después de
/// confirmado lo saque de inmediato sin tocar ninguna fila de presupuesto. Cada cambio de monto queda
/// en <see cref="BudgetChange"/>, igual criterio que <c>SystemParameterChange</c>.
/// </remarks>
internal sealed class BudgetService(
    FinancialDbContext dbContext,
    TimeProvider timeProvider) : IBudgetService
{
    public async Task<IReadOnlyList<BudgetModel>> SearchByPeriodAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        List<LedgerAccount> leafAccounts = await GetLeafExpenseAccountsAsync(cancellationToken);

        if (leafAccounts.Count == 0)
        {
            return [];
        }

        Guid[] accountIds = leafAccounts.Select(account => account.Id).ToArray();

        Dictionary<Guid, Budget> budgets = await dbContext.Set<Budget>()
            .AsNoTracking()
            .Where(budget => accountIds.Contains(budget.AccountId)
                && budget.PeriodYear == periodYear
                && budget.PeriodMonth == periodMonth)
            .ToDictionaryAsync(budget => budget.AccountId, cancellationToken);

        Dictionary<Guid, decimal> executedByAccount = await GetExecutedAmountsAsync(
            accountIds,
            periodYear,
            periodMonth,
            cancellationToken);

        return leafAccounts
            .OrderBy(account => account.Code, StringComparer.Ordinal)
            .Select(account => Map(
                account,
                budgets.GetValueOrDefault(account.Id),
                periodYear,
                periodMonth,
                executedByAccount.GetValueOrDefault(account.Id, 0m)))
            .ToArray();
    }

    public async Task<BudgetModel?> GetAsync(
        Guid accountId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        Budget? budget = await dbContext.Set<Budget>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == accountId
                    && candidate.PeriodYear == periodYear
                    && candidate.PeriodMonth == periodMonth,
                cancellationToken);

        decimal executedAmount = await GetExecutedAmountAsync(accountId, periodYear, periodMonth, cancellationToken);

        return Map(account, budget, periodYear, periodMonth, executedAmount);
    }

    public async Task<CatalogOperationResult> SetAmountAsync(
        SetBudgetAmountCommand command,
        string? version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PeriodYear is < 2000 or > 2200 || command.PeriodMonth is < 1 or > 12)
        {
            return CatalogOperationResult.Invalid("El año o mes del presupuesto no es válido.");
        }

        DateOnly periodStart = new(command.PeriodYear, command.PeriodMonth, 1);
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        DateOnly currentMonth = new(today.Year, today.Month, 1);
        if (periodStart < currentMonth)
        {
            return CatalogOperationResult.Invalid(
                "No se puede crear ni modificar un presupuesto para un mes anterior al actual.");
        }

        CatalogOperationResult? categoryValidation = await ValidateLeafExpenseAccountAsync(
            command.AccountId,
            cancellationToken);

        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        Budget? budget = await dbContext.Set<Budget>()
            .SingleOrDefaultAsync(
                candidate => candidate.AccountId == command.AccountId
                    && candidate.PeriodYear == command.PeriodYear
                    && candidate.PeriodMonth == command.PeriodMonth,
                cancellationToken);

        try
        {
            DateTimeOffset now = timeProvider.GetUtcNow();

            if (budget is null)
            {
                Budget newBudget = new(
                    Guid.NewGuid(),
                    command.AccountId,
                    command.PeriodYear,
                    command.PeriodMonth,
                    command.Amount,
                    now,
                    actorId);

                dbContext.Add(newBudget);
                dbContext.Add(new BudgetChange(
                    Guid.NewGuid(),
                    newBudget.Id,
                    previousAmount: null,
                    newBudget.Amount,
                    now,
                    actorId));
            }
            else
            {
                if (!CatalogPersistence.TrySetOriginalVersion(dbContext, budget, version ?? string.Empty))
                {
                    return CatalogOperationResult.Invalid("El token de concurrencia no es válido.");
                }

                decimal previousAmount = budget.Amount;
                budget.ChangeAmount(command.Amount, now, actorId);

                dbContext.Add(new BudgetChange(
                    Guid.NewGuid(),
                    budget.Id,
                    previousAmount,
                    budget.Amount,
                    now,
                    actorId));
            }

            return await CatalogPersistence.SaveChangesAsync(
                dbContext,
                "Ya existe un presupuesto para esa categoría y ese mes.",
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            dbContext.ChangeTracker.Clear();
            return CatalogOperationResult.Invalid(exception.Message);
        }
    }

    public async Task<BudgetExceededSummary> GetExceededSummaryAsync(
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BudgetModel> results = await SearchByPeriodAsync(periodYear, periodMonth, cancellationToken);

        BudgetModel[] exceeded = results
            .Where(budget => budget.ExecutionStatus == BudgetExecutionStatus.Exceeded)
            .ToArray();

        decimal exceededAmount = exceeded.Sum(budget => Math.Max(0m, budget.ExecutedAmount - (budget.BudgetedAmount ?? 0m)));

        return new BudgetExceededSummary(exceeded.Length, exceededAmount);
    }

    private async Task<CatalogOperationResult?> ValidateLeafExpenseAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        LedgerAccount? account = await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == accountId, cancellationToken);

        if (account is null || !account.IsActive || account.Type != LedgerAccountType.Expense)
        {
            return CatalogOperationResult.Invalid(
                "La cuenta seleccionada no existe, está inactiva o no es de gasto.");
        }

        if (!account.IsMovement)
        {
            return CatalogOperationResult.Invalid(
                "Solo se puede presupuestar una cuenta de gasto de movimiento (último nivel); las acumuladoras solo suman.");
        }

        return null;
    }

    private async Task<List<LedgerAccount>> GetLeafExpenseAccountsAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Set<LedgerAccount>()
            .AsNoTracking()
            .Where(account =>
                account.Type == LedgerAccountType.Expense
                && account.IsActive
                && account.IsMovement)
            .OrderBy(account => account.Code)
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, decimal>> GetExecutedAmountsAsync(
        Guid[] accountIds,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly periodStart, DateOnly periodEnd) = GetPeriodRange(periodYear, periodMonth);

        return await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => accountIds.Contains(expense.AccountId)
                && expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodStart
                && expense.IssueDate <= periodEnd)
            .GroupBy(expense => expense.AccountId)
            .Select(group => new { AccountId = group.Key, Total = group.Sum(expense => expense.NetAmount) })
            .ToDictionaryAsync(entry => entry.AccountId, entry => entry.Total, cancellationToken);
    }

    private async Task<decimal> GetExecutedAmountAsync(
        Guid accountId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly periodStart, DateOnly periodEnd) = GetPeriodRange(periodYear, periodMonth);

        return await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.AccountId == accountId
                && expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodStart
                && expense.IssueDate <= periodEnd)
            .Select(expense => expense.NetAmount)
            .SumAsync(cancellationToken);
    }

    private static (DateOnly PeriodStart, DateOnly PeriodEnd) GetPeriodRange(int periodYear, int periodMonth)
    {
        DateOnly periodStart = new(periodYear, periodMonth, 1);
        DateOnly periodEnd = periodStart.AddMonths(1).AddDays(-1);
        return (periodStart, periodEnd);
    }

    private static BudgetModel Map(
        LedgerAccount account,
        Budget? budget,
        int periodYear,
        int periodMonth,
        decimal executedAmount)
    {
        decimal? budgetedAmount = budget?.Amount;
        decimal? availableAmount = budgetedAmount is decimal budgeted
            ? Math.Max(0m, budgeted - executedAmount)
            : null;
        decimal? varianceAmount = budgetedAmount is decimal budgetedForVariance
            ? executedAmount - budgetedForVariance
            : null;
        decimal? variancePercent = budgetedAmount is decimal budgetedForPercent && budgetedForPercent != 0
            ? varianceAmount!.Value / budgetedForPercent
            : null;

        return new BudgetModel(
            budget?.Id,
            account.Id,
            account.Code,
            account.Name,
            periodYear,
            periodMonth,
            budgetedAmount,
            executedAmount,
            availableAmount,
            varianceAmount,
            variancePercent,
            budget?.UpdatedAtUtc,
            budget is null ? null : CatalogPersistence.EncodeVersion(budget.RowVersion));
    }
}
