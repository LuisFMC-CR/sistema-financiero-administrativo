namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Naturaleza de una cuenta contable. Solo clasifica: el sistema no lleva partida doble.
/// </summary>
public enum LedgerAccountType
{
    /// <summary>Bienes y derechos, incluidas las cajas y cuentas bancarias.</summary>
    Asset = 1,

    /// <summary>Obligaciones con terceros.</summary>
    Liability = 2,

    /// <summary>Aportes y resultados acumulados de los propietarios.</summary>
    Equity = 3,

    /// <summary>Ingresos; las categorías de ingreso apuntan a cuentas de este tipo.</summary>
    Income = 4,

    /// <summary>Gastos; las categorías de gasto apuntan a cuentas de este tipo.</summary>
    Expense = 5,
}
