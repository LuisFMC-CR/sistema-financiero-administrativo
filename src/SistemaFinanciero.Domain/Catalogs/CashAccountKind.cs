namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Subtipo de una cuenta contable de efectivo, es decir, la que recibe o entrega dinero.
/// </summary>
public enum CashAccountKind
{
    /// <summary>Caja física o fondo de efectivo.</summary>
    Cash = 1,

    /// <summary>Cuenta mantenida en una entidad bancaria.</summary>
    Bank = 2,
}
