namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Rol fijo de una cuenta contable de sistema (bloque 4 del paquete de mejoras finales, catálogo
/// contable ampliado): las reglas de contabilización buscan estas cuentas por su rol, nunca por
/// código o nombre.
/// </summary>
/// <remarks>
/// Conexiones: es el valor de <c>LedgerAccount.SystemRole</c>, nulo en las demás cuentas y único
/// cuando no es nulo. Solo lo asignan las migraciones de siembra, igual que
/// <c>LedgerAccount.IsProtected</c>; no hay un parámetro de constructor para asignarlo. A diferencia
/// de una cuenta de grupo protegida, una cuenta de sistema se puede renombrar, pero no inactivar ni
/// mover (<c>LedgerAccountService</c>).
/// </remarks>
public enum LedgerAccountSystemRole
{
    /// <summary>1.2.01 Cuentas por cobrar a clientes.</summary>
    ReceivablesFromCustomers = 1,

    /// <summary>1.3.01 IVA acreditable.</summary>
    VatCreditable = 2,

    /// <summary>1.3.02 Retenciones de renta a favor.</summary>
    WithholdingsReceivable = 3,

    /// <summary>2.1.01 Cuentas por pagar a proveedores.</summary>
    PayablesToSuppliers = 4,

    /// <summary>2.2.01 IVA por pagar.</summary>
    VatPayable = 5,

    /// <summary>5.2.05 Ajustes a cuentas por cobrar.</summary>
    ReceivableAdjustments = 6,

    /// <summary>4.3.02 Ajustes a favor en cuentas por pagar.</summary>
    PayableAdjustmentsInFavor = 7,

    /// <summary>4.4.01 Devoluciones y descuentos sobre ventas.</summary>
    SalesReturnsAndAllowances = 8,
}
