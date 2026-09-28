namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>Relaciona la naturaleza de una categoría con el tipo de cuenta contable que le corresponde.</summary>
public static class FinancialCategoryKindExtensions
{
    /// <summary>
    /// Obtiene el tipo de cuenta contable de una categoría: Ingreso para las de ingreso y Gasto para las
    /// de gasto.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">La naturaleza no está definida.</exception>
    public static LedgerAccountType ToLedgerAccountType(this FinancialCategoryKind kind) => kind switch
    {
        FinancialCategoryKind.Income => LedgerAccountType.Income,
        FinancialCategoryKind.Expense => LedgerAccountType.Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "La naturaleza no está definida."),
    };
}
