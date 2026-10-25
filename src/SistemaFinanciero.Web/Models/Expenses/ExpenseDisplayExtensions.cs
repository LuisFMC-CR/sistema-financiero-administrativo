using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Domain.Expenses;

namespace SistemaFinanciero.Web.Models.Expenses;

/// <summary>Centraliza las etiquetas en español utilizadas en las pantallas de gastos.</summary>
public static class ExpenseDisplayExtensions
{
    public static string ToDisplayName(this ExpenseStatus status) => status switch
    {
        ExpenseStatus.Draft => "Borrador",
        ExpenseStatus.Confirmed => "Confirmado",
        ExpenseStatus.Cancelled => "Anulado",
        _ => "Desconocido",
    };

    public static string ToDisplayName(this ExpenseStatusFilter status) => status switch
    {
        ExpenseStatusFilter.All => "Todos",
        ExpenseStatusFilter.Draft => "Borradores",
        ExpenseStatusFilter.Confirmed => "Confirmados",
        ExpenseStatusFilter.Cancelled => "Anulados",
        _ => "Desconocido",
    };
}
