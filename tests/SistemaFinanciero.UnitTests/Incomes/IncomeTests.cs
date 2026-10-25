using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Incomes;

namespace SistemaFinanciero.UnitTests.Incomes;

public sealed class IncomeTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly IncomeDate = new(2026, 10, 1);

    [Fact]
    public void FromInvoice_KeepsTheInvoiceOriginAndItsData()
    {
        Guid invoiceId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        Guid cashAccountId = Guid.NewGuid();

        Income income = Income.FromInvoice(
            Guid.NewGuid(),
            "ING-001",
            invoiceId,
            categoryId,
            cashAccountId,
            226m,
            IncomeDate,
            PaymentMethod.Cash,
            "F-0001",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(IncomeOrigin.InvoiceCash, income.Origin);
        Assert.Equal(invoiceId, income.InvoiceId);
        Assert.Null(income.ReceivablePaymentId);
        Assert.Equal(categoryId, income.CategoryId);
        Assert.Equal(cashAccountId, income.CashAccountId);
        Assert.Equal(226m, income.Amount);
        Assert.Equal(PaymentMethod.Cash, income.PaymentMethod);
        Assert.Equal("F-0001", income.Reference);
        Assert.Equal(IncomeStatus.Active, income.Status);
    }

    [Fact]
    public void FromReceivablePayment_KeepsTheReceivablePaymentOriginAndNoPaymentMethod()
    {
        Guid receivablePaymentId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        Guid cashAccountId = Guid.NewGuid();

        Income income = Income.FromReceivablePayment(
            Guid.NewGuid(),
            "ING-001",
            receivablePaymentId,
            categoryId,
            cashAccountId,
            450m,
            IncomeDate,
            "AB-0001",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(IncomeOrigin.ReceivablePayment, income.Origin);
        Assert.Equal(receivablePaymentId, income.ReceivablePaymentId);
        Assert.Null(income.InvoiceId);
        Assert.Null(income.PaymentMethod);
        Assert.Equal("AB-0001", income.Reference);
        Assert.Equal(450m, income.Amount);
    }

    [Fact]
    public void CreateManual_KeepsNoInvoiceNorReceivablePaymentAndRequiresTheDescription()
    {
        Guid categoryId = Guid.NewGuid();
        Guid cashAccountId = Guid.NewGuid();

        Income income = Income.CreateManual(
            Guid.NewGuid(),
            "ING-001",
            categoryId,
            cashAccountId,
            50_000m,
            IncomeDate,
            PaymentMethod.BankTransfer,
            "DEP-0099",
            "Reintegro de un proveedor por un pago duplicado",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(IncomeOrigin.Manual, income.Origin);
        Assert.Null(income.InvoiceId);
        Assert.Null(income.ReceivablePaymentId);
        Assert.Equal(PaymentMethod.BankTransfer, income.PaymentMethod);
        Assert.Equal("DEP-0099", income.Reference);
        Assert.Equal("Reintegro de un proveedor por un pago duplicado", income.Description);
    }

    [Fact]
    public void CreateManual_AllowsAMissingReference()
    {
        Income income = Income.CreateManual(
            Guid.NewGuid(),
            "ING-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            50_000m,
            IncomeDate,
            PaymentMethod.BankTransfer,
            reference: null,
            "Interés acreditado por el banco",
            CreatedAtUtc,
            CreatorId);

        Assert.Null(income.Reference);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateManual_RequiresADescription(string? description)
    {
        Assert.Throws<ArgumentException>(() => Income.CreateManual(
            Guid.NewGuid(),
            "ING-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            50_000m,
            IncomeDate,
            PaymentMethod.BankTransfer,
            "DEP-0099",
            description!,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void FromInvoice_RejectsAnUndefinedPaymentMethod()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Income.FromInvoice(
            Guid.NewGuid(),
            "ING-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            226m,
            IncomeDate,
            paymentMethod: (PaymentMethod)999,
            reference: "F-0001",
            CreatedAtUtc,
            CreatorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromInvoice_RejectsAnAmountThatIsNotPositive(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Income.FromInvoice(
            Guid.NewGuid(),
            "ING-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            amount,
            IncomeDate,
            PaymentMethod.Cash,
            "F-0001",
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void FromInvoice_RejectsADefaultIncomeDate()
    {
        Assert.Throws<ArgumentException>(() => Income.FromInvoice(
            Guid.NewGuid(),
            "ING-001",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            226m,
            incomeDate: default,
            PaymentMethod.Cash,
            "F-0001",
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAudit()
    {
        Income income = CreateInvoiceIncome();
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(1);

        income.Cancel("  Factura anulada  ", cancelledAtUtc, CreatorId);

        Assert.Equal(IncomeStatus.Cancelled, income.Status);
        Assert.Equal("Factura anulada", income.CancellationReason);
        Assert.Equal(cancelledAtUtc, income.CancelledAtUtc);
        Assert.Equal(CreatorId, income.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledIncome()
    {
        Income income = CreateInvoiceIncome();
        income.Cancel("Factura anulada", CreatedAtUtc.AddDays(1), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            income.Cancel("Otra vez", CreatedAtUtc.AddDays(2), CreatorId));
    }

    private static Income CreateInvoiceIncome() => Income.FromInvoice(
        Guid.NewGuid(),
        "ING-001",
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        226m,
        IncomeDate,
        PaymentMethod.Cash,
        "F-0001",
        CreatedAtUtc,
        CreatorId);
}
