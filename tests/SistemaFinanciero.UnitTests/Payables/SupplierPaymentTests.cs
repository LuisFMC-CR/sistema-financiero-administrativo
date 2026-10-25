using SistemaFinanciero.Domain.Payables;

namespace SistemaFinanciero.UnitTests.Payables;

public sealed class SupplierPaymentTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly PaymentDate = new(2026, 10, 25);

    [Fact]
    public void Constructor_StartsActiveWithTheGivenData()
    {
        Guid payableId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid cashAccountId = Guid.NewGuid();

        SupplierPayment payment = new(
            Guid.NewGuid(),
            payableId,
            groupId,
            400m,
            cashAccountId,
            "PAG-0001",
            PaymentDate,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(payableId, payment.PayableId);
        Assert.Equal(groupId, payment.GroupId);
        Assert.Equal(400m, payment.Amount);
        Assert.Equal(cashAccountId, payment.CashAccountId);
        Assert.Equal("PAG-0001", payment.Reference);
        Assert.Equal(PaymentDate, payment.PaymentDate);
        Assert.Equal(SupplierPaymentStatus.Active, payment.Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsAnAmountThatIsNotPositive(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePayment(amount: amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RequiresAReference(string reference)
    {
        Assert.Throws<ArgumentException>(() => CreatePayment(reference: reference));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAudit()
    {
        SupplierPayment payment = CreatePayment();
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(1);

        payment.Cancel("  Pago duplicado por error  ", cancelledAtUtc, CreatorId);

        Assert.Equal(SupplierPaymentStatus.Cancelled, payment.Status);
        Assert.Equal("Pago duplicado por error", payment.CancellationReason);
        Assert.Equal(cancelledAtUtc, payment.CancelledAtUtc);
        Assert.Equal(CreatorId, payment.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledPayment()
    {
        SupplierPayment payment = CreatePayment();
        payment.Cancel("Motivo", CreatedAtUtc.AddDays(1), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payment.Cancel("Otra vez", CreatedAtUtc.AddDays(2), CreatorId));
    }

    private static SupplierPayment CreatePayment(decimal amount = 400m, string reference = "PAG-0001") => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        amount,
        Guid.NewGuid(),
        reference,
        PaymentDate,
        CreatedAtUtc,
        CreatorId);
}
