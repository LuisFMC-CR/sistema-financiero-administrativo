using SistemaFinanciero.Domain.IncomeTax;

namespace SistemaFinanciero.UnitTests.IncomeTax;

/// <summary>
/// RF-99: estimación del impuesto sobre la renta con la escala 2026 (tramos de ₡5.621.000,
/// ₡8.433.000 y ₡11.243.000 al 5, 10, 15 y 20 %; tarifa general del 30 % si la renta bruta supera
/// ₡119.174.000). Los montos esperados se calcularon a mano.
/// </summary>
public sealed class IncomeTaxEstimateTests
{
    private static readonly IncomeTaxScale Scale = IncomeTaxScale.Default;

    [Fact]
    public void Calculate_UtilityWithinTheFirstBracket_AppliesOnlyFivePercent()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 40_000_000m, 3_000_000m, 0m, 0m);

        Assert.Equal(IncomeTaxRegime.Reduced, estimate.Regime);
        Assert.Equal(150_000m, estimate.EstimatedTax);
        Assert.Equal([150_000m, 0m, 0m, 0m], estimate.Brackets.Select(line => line.Tax));
    }

    [Fact]
    public void Calculate_UtilityInTheThirdBracket_AppliesEachRateOnlyToItsExcess()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 50_000_000m, 10_000_000m, 0m, 0m);

        // 5.621.000 × 5 % + 2.812.000 × 10 % + 1.567.000 × 15 %
        Assert.Equal([281_050m, 281_200m, 235_050m, 0m], estimate.Brackets.Select(line => line.Tax));
        Assert.Equal([5_621_000m, 2_812_000m, 1_567_000m, 0m], estimate.Brackets.Select(line => line.TaxableAmount));
        Assert.Equal(797_300m, estimate.EstimatedTax);
    }

    [Fact]
    public void Calculate_UtilityAboveTheLastLimit_AppliesTwentyPercentWithoutCap()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 80_000_000m, 20_000_000m, 0m, 0m);

        // 281.050 + 281.200 + 2.810.000 × 15 % + 8.757.000 × 20 %
        Assert.Equal([281_050m, 281_200m, 421_500m, 1_751_400m], estimate.Brackets.Select(line => line.Tax));
        Assert.Null(estimate.Brackets[^1].To);
        Assert.Equal(2_735_150m, estimate.EstimatedTax);
    }

    [Fact]
    public void Calculate_GrossIncomeAboveTheThreshold_AppliesTheGeneralRateToTheWholeUtility()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 120_000_000m, 20_000_000m, 0m, 0m);

        Assert.Equal(IncomeTaxRegime.General, estimate.Regime);
        IncomeTaxBracketLine line = Assert.Single(estimate.Brackets);
        Assert.Equal(30m, line.Rate);
        Assert.Equal(6_000_000m, estimate.EstimatedTax);
    }

    [Fact]
    public void Calculate_GrossIncomeEqualToTheThreshold_StillUsesTheReducedBrackets()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 119_174_000m, 3_000_000m, 0m, 0m);

        Assert.Equal(IncomeTaxRegime.Reduced, estimate.Regime);
        Assert.Equal(150_000m, estimate.EstimatedTax);
    }

    [Fact]
    public void Calculate_Loss_HasNoTaxAndWithholdingsBecomeAnAmountInFavor()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 30_000_000m, -500_000m, 100_000m, 0m);

        Assert.Equal(-500_000m, estimate.NetIncome);
        Assert.Equal(0m, estimate.TaxableIncome);
        Assert.Equal(0m, estimate.EstimatedTax);
        Assert.Equal(-100_000m, estimate.Balance);
        Assert.Equal(0m, estimate.AmountDue);
        Assert.Equal(100_000m, estimate.AmountInFavor);
    }

    [Fact]
    public void Calculate_SubtractsWithholdingsAndPaymentsFromTheTax()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 50_000_000m, 10_000_000m, 200_000m, 300_000m);

        Assert.Equal(797_300m, estimate.EstimatedTax);
        Assert.Equal(297_300m, estimate.Balance);
        Assert.Equal(297_300m, estimate.AmountDue);
        Assert.Equal(0m, estimate.AmountInFavor);
    }

    [Fact]
    public void Calculate_PaymentsAboveTheTax_LeaveAnAmountInFavor()
    {
        IncomeTaxEstimate estimate = IncomeTaxEstimate.Calculate(Scale, 40_000_000m, 3_000_000m, 50_000m, 200_000m);

        Assert.Equal(-100_000m, estimate.Balance);
        Assert.Equal(100_000m, estimate.AmountInFavor);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    public void Calculate_RejectsNegativeWithholdingsOrPayments(decimal withholdings, decimal payments)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IncomeTaxEstimate.Calculate(Scale, 1m, 1m, withholdings, payments));
    }
}
