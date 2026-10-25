using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Payables;

namespace SistemaFinanciero.UnitTests.Payables;

public sealed class PayableTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly DueDate = new(2026, 10, 31);

    [Fact]
    public void Constructor_StartsPendingWithTheFullBalance()
    {
        Payable payable = CreatePayable(1000m);

        Assert.Equal(PayableStatus.Pending, payable.Status);
        Assert.Equal(1000m, payable.OriginalAmount);
        Assert.Equal(1000m, payable.Balance);
        Assert.Equal(DueDate, payable.DueDate);
    }

    [Fact]
    public void ApplyPayment_ReducesTheBalanceAndStaysPartiallyPaid()
    {
        Payable payable = CreatePayable(1000m);
        DateTimeOffset paidAtUtc = CreatedAtUtc.AddDays(5);

        payable.ApplyPayment(400m, paidAtUtc, CreatorId);

        Assert.Equal(600m, payable.Balance);
        Assert.Equal(PayableStatus.PartiallyPaid, payable.Status);
        Assert.Equal(paidAtUtc, payable.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyPayment_SettlesTheBalanceWhenItReachesZero()
    {
        Payable payable = CreatePayable(1000m);

        payable.ApplyPayment(1000m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Equal(0m, payable.Balance);
        Assert.Equal(PayableStatus.Paid, payable.Status);
    }

    [Fact]
    public void ApplyPayment_RejectsAnAmountGreaterThanTheBalance()
    {
        Payable payable = CreatePayable(1000m);

        Assert.Throws<InvalidOperationException>(() =>
            payable.ApplyPayment(1000.01m, CreatedAtUtc.AddDays(5), CreatorId));

        Assert.Equal(1000m, payable.Balance);
        Assert.Equal(PayableStatus.Pending, payable.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyPayment_RejectsAnAmountThatIsNotPositive(decimal amount)
    {
        Payable payable = CreatePayable(1000m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            payable.ApplyPayment(amount, CreatedAtUtc.AddDays(5), CreatorId));
    }

    [Fact]
    public void RevertPayment_RestoresTheBalanceAndGoesBackToPending()
    {
        Payable payable = CreatePayable(1000m);
        payable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        payable.RevertPayment(400m, CreatedAtUtc.AddDays(6), CreatorId);

        Assert.Equal(1000m, payable.Balance);
        Assert.Equal(PayableStatus.Pending, payable.Status);
    }

    [Fact]
    public void RevertPayment_LeavesItPartiallyPaidWhenAnotherPaymentStillApplies()
    {
        Payable payable = CreatePayable(1000m);
        payable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);
        payable.ApplyPayment(200m, CreatedAtUtc.AddDays(6), CreatorId);

        payable.RevertPayment(200m, CreatedAtUtc.AddDays(7), CreatorId);

        Assert.Equal(600m, payable.Balance);
        Assert.Equal(PayableStatus.PartiallyPaid, payable.Status);
    }

    [Fact]
    public void RevertPayment_RejectsRestoringMoreThanTheOriginalAmount()
    {
        Payable payable = CreatePayable(1000m);
        payable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payable.RevertPayment(400.01m, CreatedAtUtc.AddDays(6), CreatorId));
    }

    [Fact]
    public void Cancel_MarksItCancelledWhenNoPaymentWasApplied()
    {
        Payable payable = CreatePayable(1000m);
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(2);

        payable.Cancel(cancelledAtUtc, CreatorId);

        Assert.Equal(PayableStatus.Cancelled, payable.Status);
        Assert.Equal(cancelledAtUtc, payable.CancelledAtUtc);
        Assert.Equal(CreatorId, payable.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsACancellationWithActivePayments()
    {
        Payable payable = CreatePayable(1000m);
        payable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payable.Cancel(CreatedAtUtc.AddDays(6), CreatorId));

        Assert.Equal(PayableStatus.PartiallyPaid, payable.Status);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledPayable()
    {
        Payable payable = CreatePayable(1000m);
        payable.Cancel(CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payable.Cancel(CreatedAtUtc.AddDays(3), CreatorId));
    }

    [Fact]
    public void Adjust_WithIncrease_RestoresBalanceAndStatus()
    {
        Payable payable = CreatePayable(1000m);
        payable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        payable.Adjust(400m, BalanceAdjustmentDirection.Increase, CreatedAtUtc.AddDays(6), CreatorId);

        Assert.Equal(1000m, payable.Balance);
        Assert.Equal(PayableStatus.Pending, payable.Status);
    }

    [Fact]
    public void Adjust_WithDecrease_ReducesBalanceAndStaysPartiallyPaid()
    {
        Payable payable = CreatePayable(1000m);

        payable.Adjust(300m, BalanceAdjustmentDirection.Decrease, CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Equal(700m, payable.Balance);
        Assert.Equal(PayableStatus.PartiallyPaid, payable.Status);
    }

    [Fact]
    public void Adjust_RejectsADecreaseThatWouldLeaveTheBalanceNegative()
    {
        Payable payable = CreatePayable(1000m);
        payable.ApplyPayment(800m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payable.Adjust(200.01m, BalanceAdjustmentDirection.Decrease, CreatedAtUtc.AddDays(6), CreatorId));

        Assert.Equal(200m, payable.Balance);
    }

    [Fact]
    public void Adjust_RejectsAnIncreaseThatWouldExceedTheOriginalAmount()
    {
        Payable payable = CreatePayable(1000m);

        Assert.Throws<InvalidOperationException>(() =>
            payable.Adjust(0.01m, BalanceAdjustmentDirection.Increase, CreatedAtUtc.AddDays(2), CreatorId));

        Assert.Equal(1000m, payable.Balance);
    }

    [Fact]
    public void Adjust_RejectsACancelledPayable()
    {
        Payable payable = CreatePayable(1000m);
        payable.Cancel(CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payable.Adjust(100m, BalanceAdjustmentDirection.Decrease, CreatedAtUtc.AddDays(3), CreatorId));
    }

    private static Payable CreatePayable(decimal originalAmount) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        originalAmount,
        DueDate,
        CreatedAtUtc,
        CreatorId);
}
