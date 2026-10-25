using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.Receivables;

/// <summary>
/// Retención aplicada por un cliente al pagar, registrada dentro de un abono (regla de negocio 3).
/// </summary>
/// <remarks>
/// Es 0..1 por <see cref="ReceivablePayment"/> (FK única por <see cref="ReceivablePaymentId"/>).
/// Conserva una fotografía de <c>WithholdingType</c> (<see cref="Code"/>, <see cref="Name"/>,
/// <see cref="Rate"/>) al momento del abono: los cambios posteriores del catálogo no la alteran,
/// igual que <c>InvoiceLineTax</c> hace con el IVA. Es inmutable: no admite modificaciones ni estado
/// propio, porque su vigencia depende por completo de la de su <see cref="ReceivablePayment"/> (si el
/// abono se anula, la retención queda sin efecto junto con él, regla 4).
///
/// Conexiones: la crea <c>ReceivablePaymentService.RegisterAsync</c>, en la misma transacción que su
/// <see cref="ReceivablePayment"/>; se guarda en <c>finanzas.Retenciones</c> vía
/// <c>WithholdingConfiguration</c>.
/// </remarks>
public sealed class Withholding
{
    /// <summary>Longitud máxima del código.</summary>
    public const int CodeMaxLength = 30;

    /// <summary>Longitud máxima del nombre.</summary>
    public const int NameMaxLength = 120;

    /// <summary>Tarifa máxima admitida.</summary>
    public const decimal MaxRate = 100m;

    private Withholding()
    {
    }

    /// <summary>
    /// Crea la retención de un abono a partir de la fotografía del tipo elegido (código, nombre y
    /// tarifa vigentes) y de la base indicada (por defecto el neto de la factura, editable).
    /// </summary>
    public Withholding(
        Guid id,
        Guid receivablePaymentId,
        Guid withholdingTypeId,
        string code,
        string name,
        decimal rate,
        decimal baseAmount,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        Id = DomainRules.RequiredId(id, nameof(id));
        ReceivablePaymentId = DomainRules.RequiredId(receivablePaymentId, nameof(receivablePaymentId));
        WithholdingTypeId = DomainRules.RequiredId(withholdingTypeId, nameof(withholdingTypeId));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        Name = DomainRules.RequiredText(name, NameMaxLength, nameof(name));
        Rate = ValidateRate(rate, nameof(rate));
        Base = ValidatePositiveAmount(baseAmount, nameof(baseAmount));
        Amount = DomainRules.RoundMoney(Base * Rate / 100m);
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
    }

    /// <summary>Obtiene el identificador técnico estable.</summary>
    public Guid Id { get; private set; }

    /// <summary>Obtiene el abono al que pertenece.</summary>
    public Guid ReceivablePaymentId { get; private set; }

    /// <summary>Obtiene el tipo de retención elegido.</summary>
    public Guid WithholdingTypeId { get; private set; }

    /// <summary>Obtiene el código del tipo, tal como estaba al momento del abono.</summary>
    public string Code { get; private set; } = string.Empty;

    /// <summary>Obtiene el nombre del tipo, tal como estaba al momento del abono.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Obtiene el porcentaje aplicado, tal como estaba al momento del abono.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Obtiene la base sobre la que se calculó, editable al registrar el abono.</summary>
    public decimal Base { get; private set; }

    /// <summary>Obtiene el monto retenido, calculado sobre <see cref="Base"/>.</summary>
    public decimal Amount { get; private set; }

    /// <summary>Obtiene el instante UTC de creación.</summary>
    public DateTimeOffset CreatedAtUtc { get; private set; }

    /// <summary>Obtiene el usuario que la registró.</summary>
    public Guid CreatedByUserId { get; private set; }

    private static decimal ValidateRate(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.Round(value, DomainRules.QuantityDecimalPlaces);

        if (rounded < 0 || rounded > MaxRate)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"La tarifa debe estar entre 0 y {MaxRate}.");
        }

        return rounded;
    }

    private static decimal ValidatePositiveAmount(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        if (rounded <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "El monto debe ser mayor que cero.");
        }

        return rounded;
    }
}
