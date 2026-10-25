using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Receivables;

namespace SistemaFinanciero.UnitTests.Receivables;

public sealed class ReceivableTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly DueDate = new(2026, 10, 31);

    [Fact]
    public void Constructor_StartsPendingWithTheFullBalance()
    {
        Receivable receivable = CreateReceivable(1000m);

        Assert.Equal(ReceivableStatus.Pending, receivable.Status);
        Assert.Equal(1000m, receivable.OriginalAmount);
        Assert.Equal(1000m, receivable.Balance);
        Assert.Equal(DueDate, receivable.DueDate);
    }

    [Fact]
    public void ApplyPayment_ReducesTheBalanceAndStaysPartiallyPaid()
    {
        Receivable receivable = CreateReceivable(1000m);
        DateTimeOffset paidAtUtc = CreatedAtUtc.AddDays(5);

        receivable.ApplyPayment(400m, paidAtUtc, CreatorId);

        Assert.Equal(600m, receivable.Balance);
        Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);
        Assert.Equal(paidAtUtc, receivable.UpdatedAtUtc);
    }

    [Fact]
    public void ApplyPayment_SettlesTheBalanceWhenItReachesZero()
    {
        Receivable receivable = CreateReceivable(1000m);

        receivable.ApplyPayment(1000m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Equal(0m, receivable.Balance);
        Assert.Equal(ReceivableStatus.Paid, receivable.Status);
    }

    [Fact]
    public void ApplyPayment_RejectsAnAmountGreaterThanTheBalance()
    {
        Receivable receivable = CreateReceivable(1000m);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.ApplyPayment(1000.01m, CreatedAtUtc.AddDays(5), CreatorId));

        Assert.Equal(1000m, receivable.Balance);
        Assert.Equal(ReceivableStatus.Pending, receivable.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ApplyPayment_RejectsAnAmountThatIsNotPositive(decimal amount)
    {
        Receivable receivable = CreateReceivable(1000m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            receivable.ApplyPayment(amount, CreatedAtUtc.AddDays(5), CreatorId));
    }

    [Fact]
    public void RevertPayment_RestoresTheBalanceAndGoesBackToPending()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        receivable.RevertPayment(400m, CreatedAtUtc.AddDays(6), CreatorId);

        Assert.Equal(1000m, receivable.Balance);
        Assert.Equal(ReceivableStatus.Pending, receivable.Status);
    }

    [Fact]
    public void RevertPayment_LeavesItPartiallyPaidWhenAnotherPaymentStillApplies()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);
        receivable.ApplyPayment(200m, CreatedAtUtc.AddDays(6), CreatorId);

        receivable.RevertPayment(200m, CreatedAtUtc.AddDays(7), CreatorId);

        Assert.Equal(600m, receivable.Balance);
        Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);
    }

    [Fact]
    public void RevertPayment_RejectsRestoringMoreThanTheOriginalAmount()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.RevertPayment(400.01m, CreatedAtUtc.AddDays(6), CreatorId));
    }

    [Fact]
    public void Cancel_MarksItCancelledWhenNoPaymentWasApplied()
    {
        Receivable receivable = CreateReceivable(1000m);
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(2);

        receivable.Cancel(cancelledAtUtc, CreatorId);

        Assert.Equal(ReceivableStatus.Cancelled, receivable.Status);
        Assert.Equal(cancelledAtUtc, receivable.CancelledAtUtc);
        Assert.Equal(CreatorId, receivable.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsACancellationWithActivePayments()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.Cancel(CreatedAtUtc.AddDays(6), CreatorId));

        Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledReceivable()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.Cancel(CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.Cancel(CreatedAtUtc.AddDays(3), CreatorId));
    }

    [Fact]
    public void Adjust_WithIncrease_RestoresBalanceAndStatus()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.ApplyPayment(400m, CreatedAtUtc.AddDays(5), CreatorId);

        receivable.Adjust(400m, BalanceAdjustmentDirection.Increase, CreatedAtUtc.AddDays(6), CreatorId);

        Assert.Equal(1000m, receivable.Balance);
        Assert.Equal(ReceivableStatus.Pending, receivable.Status);
    }

    [Fact]
    public void Adjust_WithDecrease_ReducesBalanceAndStaysPartiallyPaid()
    {
        Receivable receivable = CreateReceivable(1000m);

        receivable.Adjust(300m, BalanceAdjustmentDirection.Decrease, CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Equal(700m, receivable.Balance);
        Assert.Equal(ReceivableStatus.PartiallyPaid, receivable.Status);
    }

    [Fact]
    public void Adjust_RejectsADecreaseThatWouldLeaveTheBalanceNegative()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.ApplyPayment(800m, CreatedAtUtc.AddDays(5), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.Adjust(200.01m, BalanceAdjustmentDirection.Decrease, CreatedAtUtc.AddDays(6), CreatorId));

        Assert.Equal(200m, receivable.Balance);
    }

    [Fact]
    public void Adjust_RejectsAnIncreaseThatWouldExceedTheOriginalAmount()
    {
        Receivable receivable = CreateReceivable(1000m);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.Adjust(0.01m, BalanceAdjustmentDirection.Increase, CreatedAtUtc.AddDays(2), CreatorId));

        Assert.Equal(1000m, receivable.Balance);
    }

    [Fact]
    public void Adjust_RejectsACancelledReceivable()
    {
        Receivable receivable = CreateReceivable(1000m);
        receivable.Cancel(CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            receivable.Adjust(100m, BalanceAdjustmentDirection.Decrease, CreatedAtUtc.AddDays(3), CreatorId));
    }

    private static Receivable CreateReceivable(decimal originalAmount) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        originalAmount,
        DueDate,
        CreatedAtUtc,
        CreatorId);
}
