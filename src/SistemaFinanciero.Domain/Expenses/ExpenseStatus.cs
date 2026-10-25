namespace SistemaFinanciero.Domain.Expenses;

/// <summary>Estados admitidos durante el ciclo de vida de un gasto (regla de negocio 4).</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="Expense.Status"/>. <see cref="Expense.Confirm"/> pasa de
/// <see cref="Draft"/> a <see cref="Confirmed"/>; <see cref="Expense.Cancel"/> pasa a
/// <see cref="Cancelled"/>.
/// </remarks>
public enum ExpenseStatus
{
    /// <summary>Editable: encabezado y montos pueden modificarse.</summary>
    Draft = 1,

    /// <summary>Inmutable: de contado guardó su cuenta de efectivo y referencia; a crédito generó su cuenta por pagar.</summary>
    Confirmed = 2,

    /// <summary>Anulado con motivo; conserva su historia.</summary>
    Cancelled = 3,
}
