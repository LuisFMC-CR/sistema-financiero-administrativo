using SistemaFinanciero.Application.Budgets;

namespace SistemaFinanciero.Web.Models.Budgets;

/// <summary>
/// Etiquetas y color del semáforo de ejecución presupuestaria (RF-92, bloque 7 del paquete de
/// mejoras finales, punto 9). El color siempre va acompañado del texto, nunca solo del color; en
/// Excel y PDF solo se usa <see cref="ToDisplayName"/>.
/// </summary>
public static class BudgetExecutionStatusDisplayExtensions
{
    public static string ToDisplayName(this BudgetExecutionStatus status) => status switch
    {
        BudgetExecutionStatus.Normal => "Normal",
        BudgetExecutionStatus.Attention => "Atención",
        BudgetExecutionStatus.Exceeded => "Excedido",
        _ => "Desconocido",
    };

    public static string ToBadgeClass(this BudgetExecutionStatus status) => status switch
    {
        BudgetExecutionStatus.Normal => "text-bg-success",
        BudgetExecutionStatus.Attention => "text-bg-warning",
        BudgetExecutionStatus.Exceeded => "text-bg-danger",
        _ => "text-bg-secondary",
    };
}
