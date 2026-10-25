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
        List<FinancialCategory> leafCategories = await GetLeafExpenseCategoriesAsync(cancellationToken);

        if (leafCategories.Count == 0)
        {
            return [];
        }

        Guid[] categoryIds = leafCategories.Select(category => category.Id).ToArray();

        Dictionary<Guid, Budget> budgets = await dbContext.Set<Budget>()
            .AsNoTracking()
            .Where(budget => categoryIds.Contains(budget.CategoryId)
                && budget.PeriodYear == periodYear
                && budget.PeriodMonth == periodMonth)
            .ToDictionaryAsync(budget => budget.CategoryId, cancellationToken);

        Dictionary<Guid, decimal> executedByCategory = await GetExecutedAmountsAsync(
            categoryIds,
            periodYear,
            periodMonth,
            cancellationToken);

        return leafCategories
            .OrderBy(category => category.Code, StringComparer.Ordinal)
            .Select(category => Map(
                category,
                budgets.GetValueOrDefault(category.Id),
                periodYear,
                periodMonth,
                executedByCategory.GetValueOrDefault(category.Id, 0m)))
            .ToArray();
    }

    public async Task<BudgetModel?> GetAsync(
        Guid categoryId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken = default)
    {
        FinancialCategory? category = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return null;
        }

        Budget? budget = await dbContext.Set<Budget>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.CategoryId == categoryId
                    && candidate.PeriodYear == periodYear
                    && candidate.PeriodMonth == periodMonth,
                cancellationToken);

        decimal executedAmount = await GetExecutedAmountAsync(categoryId, periodYear, periodMonth, cancellationToken);

        return Map(category, budget, periodYear, periodMonth, executedAmount);
    }

    public async Task<CatalogOperationResult> SetAmountAsync(
        SetBudgetAmountCommand command,
        string? version,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        CatalogOperationResult? categoryValidation = await ValidateLeafExpenseCategoryAsync(
            command.CategoryId,
            cancellationToken);

        if (categoryValidation is not null)
        {
            return categoryValidation;
        }

        Budget? budget = await dbContext.Set<Budget>()
            .SingleOrDefaultAsync(
                candidate => candidate.CategoryId == command.CategoryId
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
                    command.CategoryId,
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

    private async Task<CatalogOperationResult?> ValidateLeafExpenseCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        FinancialCategory? category = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == categoryId, cancellationToken);

        if (category is null || !category.IsActive || category.Kind != FinancialCategoryKind.Expense)
        {
            return CatalogOperationResult.Invalid(
                "La categoría seleccionada no existe, está inactiva o no es de gasto.");
        }

        bool hasChildren = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .AnyAsync(candidate => candidate.ParentId == categoryId, cancellationToken);

        if (hasChildren)
        {
            return CatalogOperationResult.Invalid(
                "Solo se puede presupuestar una categoría de gasto de último nivel; las superiores solo suman.");
        }

        return null;
    }

    private async Task<List<FinancialCategory>> GetLeafExpenseCategoriesAsync(CancellationToken cancellationToken)
    {
        List<FinancialCategory> categories = await dbContext.Set<FinancialCategory>()
            .AsNoTracking()
            .Where(category => category.Kind == FinancialCategoryKind.Expense && category.IsActive)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0)
        {
            return [];
        }

        HashSet<Guid> parentIds = categories
            .Where(category => category.ParentId is not null)
            .Select(category => category.ParentId!.Value)
            .ToHashSet();

        return categories.Where(category => !parentIds.Contains(category.Id)).ToList();
    }

    private async Task<Dictionary<Guid, decimal>> GetExecutedAmountsAsync(
        Guid[] categoryIds,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly periodStart, DateOnly periodEnd) = GetPeriodRange(periodYear, periodMonth);

        return await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => categoryIds.Contains(expense.CategoryId)
                && expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodStart
                && expense.IssueDate <= periodEnd)
            .GroupBy(expense => expense.CategoryId)
            .Select(group => new { CategoryId = group.Key, Total = group.Sum(expense => expense.TotalAmount) })
            .ToDictionaryAsync(entry => entry.CategoryId, entry => entry.Total, cancellationToken);
    }

    private async Task<decimal> GetExecutedAmountAsync(
        Guid categoryId,
        int periodYear,
        int periodMonth,
        CancellationToken cancellationToken)
    {
        (DateOnly periodStart, DateOnly periodEnd) = GetPeriodRange(periodYear, periodMonth);

        return await dbContext.Set<Expense>()
            .AsNoTracking()
            .Where(expense => expense.CategoryId == categoryId
                && expense.Status == ExpenseStatus.Confirmed
                && expense.IssueDate >= periodStart
                && expense.IssueDate <= periodEnd)
            .Select(expense => expense.TotalAmount)
            .SumAsync(cancellationToken);
    }

    private static (DateOnly PeriodStart, DateOnly PeriodEnd) GetPeriodRange(int periodYear, int periodMonth)
    {
        DateOnly periodStart = new(periodYear, periodMonth, 1);
        DateOnly periodEnd = periodStart.AddMonths(1).AddDays(-1);
        return (periodStart, periodEnd);
    }

    private static BudgetModel Map(
        FinancialCategory category,
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
            category.Id,
            category.Code,
            category.Name,
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
