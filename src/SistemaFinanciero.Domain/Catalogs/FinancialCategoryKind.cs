namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Naturaleza administrativa de una categoría financiera.
/// </summary>
public enum FinancialCategoryKind
{
    /// <summary>
    /// Clasifica ingresos o ventas.
    /// </summary>
    Income = 1,

    /// <summary>
    /// Clasifica gastos o compras.
    /// </summary>
    Expense = 2,
}
