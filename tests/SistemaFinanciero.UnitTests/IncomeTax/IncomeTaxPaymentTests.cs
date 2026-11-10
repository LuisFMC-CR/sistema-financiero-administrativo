using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.IncomeTax;

namespace SistemaFinanciero.UnitTests.IncomeTax;

/// <summary>RF-99: pago de impuesto sobre la renta hecho a Hacienda.</summary>
public sealed class IncomeTaxPaymentTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ManagerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateOnly PeriodStart = new(2026, 1, 1);
    private static readonly DateOnly PeriodEnd = new(2026, 12, 31);

    [Fact]
    public void Constructor_StartsActiveWithTheGivenData()
    {
        Guid cashAccountId = Guid.NewGuid();

        IncomeTaxPayment payment = new(
            Guid.NewGuid(),
            "PIR-000001",
            PeriodStart,
            PeriodEnd,
            cashAccountId,
            PaymentMethod.BankTransfer,
            150_000.005m,
            "REC-HAC-123",
            "Pago parcial de junio",
            new DateOnly(2026, 6, 30),
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(IncomeTaxPaymentStatus.Active, payment.Status);
        Assert.Equal(150_000.01m, payment.Amount);
        Assert.Equal(cashAccountId, payment.CashAccountId);
        Assert.Equal(PeriodStart, payment.FiscalPeriodStartDate);
        Assert.Equal(PeriodEnd, payment.FiscalPeriodEndDate);
    }

    [Fact]
    public void Constructor_AllowsAPaymentAfterThePeriodEnds()
    {
        // El pago de la declaración se hace cuando el período ya terminó.
        IncomeTaxPayment payment = CreatePayment(paymentDate: new DateOnly(2027, 3, 15));

        Assert.Equal(new DateOnly(2027, 3, 15), payment.PaymentDate);
    }

    [Fact]
    public void Constructor_RejectsAPaymentBeforeThePeriodStarts()
    {
        Assert.Throws<ArgumentException>(() => CreatePayment(paymentDate: new DateOnly(2025, 12, 31)));
    }

    [Fact]
    public void Constructor_RejectsAPeriodThatIsNotTwelveMonths()
    {
        Assert.Throws<ArgumentException>(() => CreatePayment(periodEnd: new DateOnly(2026, 6, 30)));
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
    public void Cancel_MarksThePaymentAsCancelledWithItsReason()
    {
        IncomeTaxPayment payment = CreatePayment();
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddHours(1);

        payment.Cancel("Monto digitado por error", cancelledAtUtc, ManagerId);

        Assert.Equal(IncomeTaxPaymentStatus.Cancelled, payment.Status);
        Assert.Equal("Monto digitado por error", payment.CancellationReason);
        Assert.Equal(cancelledAtUtc, payment.CancelledAtUtc);
        Assert.Equal(ManagerId, payment.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledPayment()
    {
        IncomeTaxPayment payment = CreatePayment();
        payment.Cancel("Error", CreatedAtUtc.AddHours(1), ManagerId);

        Assert.Throws<InvalidOperationException>(() => payment.Cancel("Otra vez", CreatedAtUtc.AddHours(2), ManagerId));
    }

    [Fact]
    public void Cancel_WithoutReason_KeepsThePaymentActive()
    {
        IncomeTaxPayment payment = CreatePayment();

        Assert.Throws<ArgumentException>(() => payment.Cancel(" ", CreatedAtUtc.AddHours(1), ManagerId));
        Assert.Equal(IncomeTaxPaymentStatus.Active, payment.Status);
    }

    private static IncomeTaxPayment CreatePayment(
        DateOnly? paymentDate = null,
        DateOnly? periodEnd = null,
        decimal amount = 150_000m,
        string reference = "REC-HAC-123") =>
        new(
            Guid.NewGuid(),
            "PIR-000001",
            PeriodStart,
            periodEnd ?? PeriodEnd,
            Guid.NewGuid(),
            PaymentMethod.BankTransfer,
            amount,
            reference,
            null,
            paymentDate ?? new DateOnly(2026, 6, 30),
            CreatedAtUtc,
            CreatorId);
}
