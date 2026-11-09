using SistemaFinanciero.Domain.VatSettlements;

namespace SistemaFinanciero.UnitTests.VatSettlements;

public sealed class VatSettlementTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly SettlementDate = new(2026, 11, 9);

    [Fact]
    public void Constructor_WithAPayment_StartsActiveWithTheGivenData()
    {
        Guid cashAccountId = Guid.NewGuid();

        VatSettlement settlement = new(
            Guid.NewGuid(),
            "LIQ-000001",
            2026,
            10,
            SettlementDate,
            vatPayableAmount: 500m,
            vatCreditApplied: 300m,
            paymentAmount: 200m,
            creditCarriedForward: 0m,
            cashAccountId,
            "PAGO-0001",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(2026, settlement.PeriodYear);
        Assert.Equal(10, settlement.PeriodMonth);
        Assert.Equal(500m, settlement.VatPayableAmount);
        Assert.Equal(300m, settlement.VatCreditApplied);
        Assert.Equal(200m, settlement.PaymentAmount);
        Assert.Equal(0m, settlement.CreditCarriedForward);
        Assert.Equal(cashAccountId, settlement.CashAccountId);
        Assert.Equal("PAGO-0001", settlement.Reference);
        Assert.Equal(VatSettlementStatus.Active, settlement.Status);
    }

    [Fact]
    public void Constructor_WithoutAPayment_AllowsNullCashAccountAndReference()
    {
        VatSettlement settlement = CreateSettlementWithoutPayment();

        Assert.Equal(0m, settlement.PaymentAmount);
        Assert.Null(settlement.CashAccountId);
        Assert.Null(settlement.Reference);
        Assert.Equal(100m, settlement.CreditCarriedForward);
    }

    [Fact]
    public void Constructor_RejectsAPaymentWithoutACashAccountOrReference()
    {
        Assert.Throws<ArgumentException>(() => new VatSettlement(
            Guid.NewGuid(),
            "LIQ-000001",
            2026,
            10,
            SettlementDate,
            vatPayableAmount: 500m,
            vatCreditApplied: 300m,
            paymentAmount: 200m,
            creditCarriedForward: 0m,
            cashAccountId: null,
            reference: null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsACashAccountOrReferenceWithoutAPayment()
    {
        Assert.Throws<ArgumentException>(() => new VatSettlement(
            Guid.NewGuid(),
            "LIQ-000001",
            2026,
            10,
            SettlementDate,
            vatPayableAmount: 300m,
            vatCreditApplied: 300m,
            paymentAmount: 0m,
            creditCarriedForward: 0m,
            cashAccountId: Guid.NewGuid(),
            reference: "PAGO-0001",
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsWhenThePayableAmountDoesNotMatchTheCreditAndPaymentSum()
    {
        Assert.Throws<ArgumentException>(() => new VatSettlement(
            Guid.NewGuid(),
            "LIQ-000001",
            2026,
            10,
            SettlementDate,
            vatPayableAmount: 500m,
            vatCreditApplied: 300m,
            paymentAmount: 100m,
            creditCarriedForward: 0m,
            cashAccountId: Guid.NewGuid(),
            reference: "PAGO-0001",
            CreatedAtUtc,
            CreatorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    public void Constructor_RejectsAMonthOutsideTheValidRange(int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new VatSettlement(
            Guid.NewGuid(),
            "LIQ-000001",
            2026,
            month,
            SettlementDate,
            vatPayableAmount: 0m,
            vatCreditApplied: 0m,
            paymentAmount: 0m,
            creditCarriedForward: 0m,
            cashAccountId: null,
            reference: null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAudit()
    {
        VatSettlement settlement = CreateSettlementWithoutPayment();
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(1);

        settlement.Cancel("  Liquidación registrada por error  ", cancelledAtUtc, CreatorId);

        Assert.Equal(VatSettlementStatus.Cancelled, settlement.Status);
        Assert.Equal("Liquidación registrada por error", settlement.CancellationReason);
        Assert.Equal(cancelledAtUtc, settlement.CancelledAtUtc);
        Assert.Equal(CreatorId, settlement.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledSettlement()
    {
        VatSettlement settlement = CreateSettlementWithoutPayment();
        settlement.Cancel("Motivo", CreatedAtUtc.AddDays(1), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            settlement.Cancel("Otra vez", CreatedAtUtc.AddDays(2), CreatorId));
    }

    private static VatSettlement CreateSettlementWithoutPayment() => new(
        Guid.NewGuid(),
        "LIQ-000001",
        2026,
        10,
        SettlementDate,
        vatPayableAmount: 100m,
        vatCreditApplied: 100m,
        paymentAmount: 0m,
        creditCarriedForward: 100m,
        cashAccountId: null,
        reference: null,
        CreatedAtUtc,
        CreatorId);
}
