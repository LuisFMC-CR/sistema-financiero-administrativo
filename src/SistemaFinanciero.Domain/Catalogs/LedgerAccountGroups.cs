namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Códigos de los grupos de segundo nivel donde el usuario puede crear una cuenta de movimiento
/// nueva. Toda cuenta nueva queda siempre de tercer nivel; las cuentas superiores (raíces y grupos)
/// son acumuladoras y no se modifican.
/// </summary>
/// <remarks>
/// El grupo 4.4 queda fuera a propósito: solo contiene la cuenta de sistema de devoluciones
/// (<see cref="LedgerAccountSystemRole.SalesReturnsAndAllowances"/>), nunca cuentas de detalle del
/// usuario. Los grupos 1.2, 1.3, 5.3 y los que no aparecen aquí ya no forman parte de la estructura
/// vigente del catálogo contable.
///
/// Conexiones: <c>LedgerAccountService.CreateAsync</c> (Infrastructure) la usa para rechazar cualquier
/// otra cuenta superior; <c>LedgerAccountService.GetParentOptionsAsync</c> la usa para que el
/// formulario nunca ofrezca una cuenta superior fuera de esta lista.
/// </remarks>
public static class LedgerAccountGroups
{
    /// <summary>Los códigos de grupo de segundo nivel válidos como cuenta superior nueva.</summary>
    public static readonly IReadOnlySet<string> DetailParentCodes = new HashSet<string>(
        ["1.1", "2.1", "2.2", "3.1", "4.1", "4.2", "4.3", "5.1", "5.2"],
        StringComparer.Ordinal);
}
