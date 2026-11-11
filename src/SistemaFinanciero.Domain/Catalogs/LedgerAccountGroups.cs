namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Códigos de los grupos de segundo nivel donde el usuario puede crear una cuenta de movimiento
/// nueva. Toda cuenta nueva queda siempre de tercer nivel; las cuentas superiores (raíces y grupos)
/// son acumuladoras y no se modifican.
/// </summary>
/// <remarks>
/// Regla 1 de CLAUDE.md y RF-01 del documento final: 1.1 Efectivo (cuenta nueva siempre de
/// efectivo), 4.1 a 4.3 de ingresos y 5.1 a 5.3 de gastos (paquete v9, bloque 9). Pasivo y Patrimonio
/// (2.x y 3.x) quedan fuera porque solo tienen cuentas de sistema. Los grupos 1.2 y 1.3 también: solo
/// contienen cuentas de sistema (cuentas por cobrar e impuestos a favor). El grupo 4.4 queda fuera a
/// propósito: solo contiene la cuenta de sistema de devoluciones
/// (<see cref="LedgerAccountSystemRole.SalesReturnsAndAllowances"/>), nunca cuentas de detalle del
/// usuario.
///
/// Conexiones: <c>LedgerAccountService.CreateAsync</c> (Infrastructure) la usa para rechazar cualquier
/// otra cuenta superior; <c>LedgerAccountService.GetParentOptionsAsync</c> la usa para que el
/// formulario nunca ofrezca una cuenta superior fuera de esta lista.
/// </remarks>
public static class LedgerAccountGroups
{
    /// <summary>Los códigos de grupo de segundo nivel válidos como cuenta superior nueva.</summary>
    public static readonly IReadOnlySet<string> DetailParentCodes = new HashSet<string>(
        ["1.1", "4.1", "4.2", "4.3", "5.1", "5.2", "5.3"],
        StringComparer.Ordinal);
}
