namespace SistemaFinanciero.Domain.Incomes;

/// <summary>Estados admitidos de un ingreso.</summary>
/// <remarks>Conexiones: es el valor de <see cref="Income.Status"/>.</remarks>
public enum IncomeStatus
{
    /// <summary>Vigente: cuenta en totales, reportes y presupuesto.</summary>
    Active = 1,

    /// <summary>Anulado: sale de totales, reportes y presupuesto, pero sigue visible en consultas.</summary>
    Cancelled = 2,
}
