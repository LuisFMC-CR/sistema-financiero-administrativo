using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.IncomeTax;

/// <summary>Forma de cálculo que resultó de la renta bruta del período.</summary>
public enum IncomeTaxRegime
{
    /// <summary>Tramos progresivos para pequeñas empresas: la renta bruta no supera el umbral.</summary>
    Reduced = 1,

    /// <summary>Tarifa general sobre toda la renta neta: la renta bruta supera el umbral.</summary>
    General = 2,
}

/// <summary>
/// Un tramo aplicado: la parte de la renta neta que cae entre <see cref="From"/> y <see cref="To"/>
/// (sin tope cuando es nulo) y el impuesto que le corresponde.
/// </summary>
public sealed record IncomeTaxBracketLine(
    decimal From,
    decimal? To,
    decimal Rate,
    decimal TaxableAmount,
    decimal Tax);

/// <summary>
/// Estimación del impuesto sobre la renta de un período fiscal (RF-99): aplica la escala a la renta
/// neta y resta las retenciones del 2 % y los pagos hechos a Hacienda para obtener el saldo estimado.
/// </summary>
/// <remarks>
/// Es un cálculo puro: no guarda nada ni consulta la base de datos. La renta bruta y la renta neta
/// salen del estado de resultados del período (ingresos y utilidad, ya sin IVA); las retenciones, de los
/// movimientos de la cuenta de sistema "Retenciones de renta a favor"; y los pagos, de los
/// <see cref="IncomeTaxPayment"/> vigentes del período. Una pérdida no genera impuesto. Cada tramo se
/// redondea a dos decimales y el impuesto total es la suma de los tramos.
///
/// Conexiones: lo invoca <c>ReportService</c> (Infrastructure/Reports) para el reporte "Estimación del
/// impuesto sobre la renta".
/// </remarks>
public sealed class IncomeTaxEstimate
{
    private const decimal PercentDivisor = 100m;

    private IncomeTaxEstimate(
        decimal grossIncome,
        decimal netIncome,
        decimal taxableIncome,
        IncomeTaxRegime regime,
        IReadOnlyList<IncomeTaxBracketLine> brackets,
        decimal estimatedTax,
        decimal withholdings,
        decimal payments)
    {
        GrossIncome = grossIncome;
        NetIncome = netIncome;
        TaxableIncome = taxableIncome;
        Regime = regime;
        Brackets = brackets;
        EstimatedTax = estimatedTax;
        Withholdings = withholdings;
        Payments = payments;
        Balance = DomainRules.RoundMoney(estimatedTax - withholdings - payments);
    }

    /// <summary>Renta bruta del período (total de ingresos); decide la forma de cálculo.</summary>
    public decimal GrossIncome { get; }

    /// <summary>Utilidad o pérdida del período.</summary>
    public decimal NetIncome { get; }

    /// <summary>Renta neta sobre la que se calcula el impuesto: la utilidad, o cero si hubo pérdida.</summary>
    public decimal TaxableIncome { get; }

    /// <summary>Forma de cálculo aplicada.</summary>
    public IncomeTaxRegime Regime { get; }

    /// <summary>Tramos aplicados, en orden; uno solo con la tarifa general.</summary>
    public IReadOnlyList<IncomeTaxBracketLine> Brackets { get; }

    /// <summary>Impuesto estimado del período, antes de retenciones y pagos.</summary>
    public decimal EstimatedTax { get; }

    /// <summary>Retenciones del 2 % del sector público acumuladas en el período.</summary>
    public decimal Withholdings { get; }

    /// <summary>Pagos vigentes hechos a Hacienda para el período.</summary>
    public decimal Payments { get; }

    /// <summary>Impuesto menos retenciones y pagos: positivo por pagar, negativo a favor.</summary>
    public decimal Balance { get; }

    /// <summary>Monto estimado por pagar (cero si el saldo es a favor).</summary>
    public decimal AmountDue => Balance > 0 ? Balance : 0m;

    /// <summary>Monto estimado a favor (cero si el saldo es por pagar).</summary>
    public decimal AmountInFavor => Balance < 0 ? -Balance : 0m;

    /// <summary>Calcula la estimación con la escala indicada.</summary>
    public static IncomeTaxEstimate Calculate(
        IncomeTaxScale scale,
        decimal grossIncome,
        decimal netIncome,
        decimal withholdings,
        decimal payments)
    {
        ArgumentNullException.ThrowIfNull(scale);

        decimal roundedGross = DomainRules.RoundMoney(grossIncome);
        decimal roundedNet = DomainRules.RoundMoney(netIncome);
        decimal roundedWithholdings = RequireNonNegative(withholdings, nameof(withholdings));
        decimal roundedPayments = RequireNonNegative(payments, nameof(payments));
        decimal taxableIncome = roundedNet > 0 ? roundedNet : 0m;

        IncomeTaxRegime regime = roundedGross > scale.GrossIncomeThreshold
            ? IncomeTaxRegime.General
            : IncomeTaxRegime.Reduced;

        IReadOnlyList<IncomeTaxBracketLine> brackets = regime == IncomeTaxRegime.General
            ? [CreateLine(0m, null, scale.GeneralRate, taxableIncome)]
            :
            [
                CreateLine(0m, scale.Bracket1UpperLimit, scale.Bracket1Rate, taxableIncome),
                CreateLine(scale.Bracket1UpperLimit, scale.Bracket2UpperLimit, scale.Bracket2Rate, taxableIncome),
                CreateLine(scale.Bracket2UpperLimit, scale.Bracket3UpperLimit, scale.Bracket3Rate, taxableIncome),
                CreateLine(scale.Bracket3UpperLimit, null, scale.Bracket4Rate, taxableIncome),
            ];

        decimal estimatedTax = brackets.Sum(line => line.Tax);

        return new IncomeTaxEstimate(
            roundedGross,
            roundedNet,
            taxableIncome,
            regime,
            brackets,
            estimatedTax,
            roundedWithholdings,
            roundedPayments);
    }

    /// <summary>
    /// Arma un tramo: la parte de <paramref name="taxableIncome"/> que excede <paramref name="from"/>,
    /// sin pasar de <paramref name="to"/>.
    /// </summary>
    private static IncomeTaxBracketLine CreateLine(decimal from, decimal? to, decimal rate, decimal taxableIncome)
    {
        decimal upper = to is null ? taxableIncome : Math.Min(taxableIncome, to.Value);
        decimal taxableAmount = upper > from ? upper - from : 0m;
        decimal tax = DomainRules.RoundMoney(taxableAmount * rate / PercentDivisor);

        return new IncomeTaxBracketLine(from, to, rate, taxableAmount, tax);
    }

    private static decimal RequireNonNegative(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);

        return rounded >= 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto no puede ser negativo.");
    }
}
