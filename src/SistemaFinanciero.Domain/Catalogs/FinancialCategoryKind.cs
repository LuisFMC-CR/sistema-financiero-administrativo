namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Naturaleza administrativa de una categoría financiera.
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>FinancialCategory.Kind</c>;
/// <c>FinancialCategoryKindExtensions.ToLedgerAccountType</c> lo traduce al tipo de cuenta contable
/// (Ingreso o Gasto) que esa categoría debe usar.
/// </remarks>
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
