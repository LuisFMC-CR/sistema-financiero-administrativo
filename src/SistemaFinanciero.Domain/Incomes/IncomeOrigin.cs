namespace SistemaFinanciero.Domain.Incomes;

/// <summary>Origen de un ingreso (regla de negocio 2).</summary>
/// <remarks>
/// Conexiones: es el valor de <see cref="Income.Origin"/>; condiciona qué otros campos son
/// obligatorios (ver <c>Income.EnsureOriginConsistency</c>).
/// </remarks>
public enum IncomeOrigin
{
    /// <summary>Generado al confirmar una factura de contado.</summary>
    InvoiceCash = 1,

    /// <summary>Generado al registrar un abono a una cuenta por cobrar.</summary>
    ReceivablePayment = 2,

    /// <summary>Registrado manualmente (reintegros, intereses que acredite el banco).</summary>
    Manual = 3,
}
