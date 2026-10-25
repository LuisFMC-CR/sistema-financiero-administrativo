using SistemaFinanciero.Domain.Receivables;

namespace SistemaFinanciero.UnitTests.Receivables;

public sealed class ReceivablePaymentTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly PaymentDate = new(2026, 10, 24);

    [Fact]
    public void Constructor_StartsActiveWithTheGivenData()
    {
        Guid receivableId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid cashAccountId = Guid.NewGuid();

        ReceivablePayment payment = new(
            Guid.NewGuid(),
            receivableId,
            groupId,
            400m,
            cashAccountId,
            "AB-0001",
            PaymentDate,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(receivableId, payment.ReceivableId);
        Assert.Equal(groupId, payment.GroupId);
        Assert.Equal(400m, payment.Amount);
        Assert.Equal(cashAccountId, payment.CashAccountId);
        Assert.Equal("AB-0001", payment.Reference);
        Assert.Equal(PaymentDate, payment.PaymentDate);
        Assert.Equal(ReceivablePaymentStatus.Active, payment.Status);
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
        ReceivablePayment payment = CreatePayment();
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(1);

        payment.Cancel("  Abono duplicado por error  ", cancelledAtUtc, CreatorId);

        Assert.Equal(ReceivablePaymentStatus.Cancelled, payment.Status);
        Assert.Equal("Abono duplicado por error", payment.CancellationReason);
        Assert.Equal(cancelledAtUtc, payment.CancelledAtUtc);
        Assert.Equal(CreatorId, payment.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledPayment()
    {
        ReceivablePayment payment = CreatePayment();
        payment.Cancel("Motivo", CreatedAtUtc.AddDays(1), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            payment.Cancel("Otra vez", CreatedAtUtc.AddDays(2), CreatorId));
    }

    private static ReceivablePayment CreatePayment(decimal amount = 400m, string reference = "AB-0001") => new(
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
