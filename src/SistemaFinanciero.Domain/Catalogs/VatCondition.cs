namespace SistemaFinanciero.Domain.Catalogs;

/// <summary>
/// Condición de IVA de un <see cref="Customer"/> (bloque 1 del paquete v7, RF-93): determina
/// automáticamente la tarifa de IVA de cada línea de las facturas de ese cliente. La factura nunca
/// ofrece una tarifa que no corresponda a la condición del cliente.
/// </summary>
public enum VatCondition
{
    /// <summary>
    /// Cada línea toma la tarifa predeterminada del producto o servicio, o IVA 13 % si no tiene una;
    /// el usuario puede cambiarla, pero no a una tarifa reservada para otra condición.
    /// </summary>
    Normal = 1,

    /// <summary>
    /// Institución estatal de educación superior (Ley 9635, art. 11, inciso 2.d; por ejemplo UCR,
    /// TEC, UNA, UNED y UTN): todas las líneas van a IVA 2 %, sin poder cambiarla.
    /// </summary>
    StateHigherEducationInstitution = 2,

    /// <summary>
    /// No sujeto (por ejemplo CCSS y municipalidades): todas las líneas van a la tarifa No sujeto,
    /// sin poder cambiarla.
    /// </summary>
    NotSubject = 3,
}
