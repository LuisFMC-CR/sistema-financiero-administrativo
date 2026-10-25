namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Subtipo de una cuenta contable de efectivo, es decir, la que recibe o entrega dinero.
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>LedgerAccount.CashKind</c>, nulo cuando la cuenta no es de efectivo.
/// Aparece en <c>CashAccountOption</c> (el selector que usarán los futuros ingresos y pagos) y en
/// <c>CatalogDisplayExtensions.ToDisplayName</c> (Web) como «Caja» o «Cuenta bancaria».
/// </remarks>
public enum CashAccountKind
{
    /// <summary>Caja física o fondo de efectivo.</summary>
    Cash = 1,

    /// <summary>Cuenta mantenida en una entidad bancaria.</summary>
    Bank = 2,
}
