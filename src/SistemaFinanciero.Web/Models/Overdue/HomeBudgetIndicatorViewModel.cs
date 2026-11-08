using SistemaFinanciero.Application.Budgets;

namespace SistemaFinanciero.Web.Models.Overdue;

public sealed record HomeBudgetIndicatorViewModel(
    BudgetExceededSummary ExceededBudgets,
    int PeriodYear,
    int PeriodMonth);
