namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Naturaleza de una cuenta contable. Solo clasifica: el sistema no lleva partida doble en el
/// catálogo en sí (la partida doble vive en los asientos automáticos, bloque 5 del paquete de mejoras
/// finales).
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>LedgerAccount.Type</c>. <c>LedgerAccountService</c> exige que la
/// cuenta superior sea del mismo tipo, y que solo <see cref="Asset"/> pueda marcarse de efectivo.
/// <see cref="Income"/> y <see cref="Expense"/> son los que debe tener la cuenta de cada
/// <c>FinancialCategory</c>, según <c>FinancialCategoryKindExtensions.ToLedgerAccountType</c>.
/// <see cref="Liability"/> y <see cref="Equity"/> (bloque 4 del paquete de mejoras finales) son
/// siempre cuentas de sistema (<c>LedgerAccount.SystemRole</c>): el usuario nunca elige ni crea
/// cuentas de estos dos tipos.
/// </remarks>
public enum LedgerAccountType
{
    /// <summary>Bienes y derechos, incluidas las cajas y cuentas bancarias.</summary>
    Asset = 1,

    /// <summary>Obligaciones con terceros; siempre cuentas de sistema (bloque 4).</summary>
    Liability = 2,

    /// <summary>Patrimonio; siempre cuentas de sistema, sin cuentas de detalle en esta etapa (bloque 4).</summary>
    Equity = 3,

    /// <summary>Ingresos; las categorías de ingreso apuntan a cuentas de este tipo.</summary>
    Income = 4,

    /// <summary>Gastos; las categorías de gasto apuntan a cuentas de este tipo.</summary>
    Expense = 5,
}
