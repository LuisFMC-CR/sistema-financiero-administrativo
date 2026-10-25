namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Naturaleza de una cuenta contable. Solo clasifica: el sistema no lleva partida doble. Sin pasivo
/// ni patrimonio (regla 1): el sistema no administra préstamos, aportes de socios ni bancos de
/// terceros.
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>LedgerAccount.Type</c>. <c>LedgerAccountService</c> exige que la
/// cuenta superior sea del mismo tipo, y que solo <see cref="Asset"/> pueda marcarse de efectivo.
/// <see cref="Income"/> y <see cref="Expense"/> son los que debe tener la cuenta de cada
/// <c>FinancialCategory</c>, según <c>FinancialCategoryKindExtensions.ToLedgerAccountType</c>. Los
/// valores numéricos de <see cref="Income"/> y <see cref="Expense"/> no se renumeraron al quitar
/// Pasivo y Patrimonio, para no alterar los datos ya guardados.
/// </remarks>
public enum LedgerAccountType
{
    /// <summary>Bienes y derechos, incluidas las cajas y cuentas bancarias.</summary>
    Asset = 1,

    /// <summary>Ingresos; las categorías de ingreso apuntan a cuentas de este tipo.</summary>
    Income = 4,

    /// <summary>Gastos; las categorías de gasto apuntan a cuentas de este tipo.</summary>
    Expense = 5,
}
