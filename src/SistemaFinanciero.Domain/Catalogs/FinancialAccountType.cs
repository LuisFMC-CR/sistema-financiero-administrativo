namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Tipos de cuentas utilizadas para registrar entradas y salidas de dinero.
/// </summary>
public enum FinancialAccountType
{
    /// <summary>
    /// Caja física o fondo de efectivo.
    /// </summary>
    Cash = 1,

    /// <summary>
    /// Cuenta mantenida en una entidad bancaria.
    /// </summary>
    Bank = 2,
}
