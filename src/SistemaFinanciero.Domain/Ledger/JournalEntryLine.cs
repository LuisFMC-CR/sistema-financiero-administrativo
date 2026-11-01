using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Ledger;

/// <summary>
/// Línea inmutable de un asiento contable: un débito o un crédito sobre una cuenta de detalle.
/// </summary>
/// <remarks>
/// Conexiones: vive dentro de un <see cref="JournalEntry"/>, que es quien la construye y valida que el
/// conjunto balancee (RF-78, bloque 5 del paquete de mejoras finales); <c>AccountId</c> apunta a una
/// cuenta de detalle del catálogo contable. Se guarda en <c>finanzas.AsientoLineas</c> vía
/// <c>JournalEntryLineConfiguration</c>.
/// </remarks>
public sealed class JournalEntryLine
{
    private JournalEntryLine()
    {
    }

    internal JournalEntryLine(Guid id, int lineNumber, Guid accountId, decimal debit, decimal credit)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        LineNumber = lineNumber > 0
            ? lineNumber
            : throw new ArgumentOutOfRangeException(nameof(lineNumber), lineNumber, "La posición debe ser mayor que cero.");
        AccountId = DomainRules.RequiredId(accountId, nameof(accountId));
        Debit = ValidateNonNegative(debit, nameof(debit));
        Credit = ValidateNonNegative(credit, nameof(credit));

        if ((Debit > 0) == (Credit > 0))
        {
            throw new ArgumentException(
                "Cada línea del asiento debe tener exactamente un débito o un crédito mayor que cero.",
                nameof(debit));
        }
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene la posición de la línea dentro de su asiento, desde 1.</summary>
    public int LineNumber { get; private set; }

    /// <summary>Obtiene la cuenta de detalle afectada.</summary>
    public Guid AccountId { get; private set; }

    /// <summary>Obtiene el importe al débito; cero cuando la línea es de crédito.</summary>
    public decimal Debit { get; private set; }

    /// <summary>Obtiene el importe al crédito; cero cuando la línea es de débito.</summary>
    public decimal Credit { get; private set; }

    private static decimal ValidateNonNegative(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        if (rounded < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "El importe no puede ser negativo.");
        }

        return rounded;
    }
}
