using SistemaFinanciero.Domain.Common;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Expenses;

public sealed class ExpenseTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly IssueDate = new(2026, 10, 1);

    [Fact]
    public void Constructor_WithCashTerm_HasNoDueDateAndCalculatesTaxAndTotal()
    {
        Expense expense = CreateExpense(PaymentTerm.Cash, netAmount: 1000m, taxRate: 13m);

        Assert.Equal(ExpenseStatus.Draft, expense.Status);
        Assert.Null(expense.DueDate);
        Assert.Equal(1000m, expense.NetAmount);
        Assert.Equal(130m, expense.TaxAmount);
        Assert.Equal(1130m, expense.TotalAmount);
    }

    [Fact]
    public void Constructor_WithCreditTerm_CalculatesDueDateFromIssueDateAndTerm()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);

        Assert.Equal(IssueDate.AddDays(30), expense.DueDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsAnAmountThatIsNotPositive(decimal netAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateExpense(PaymentTerm.Cash, netAmount, 13m));
    }

    [Fact]
    public void UpdateHeader_RecalculatesTaxAndDueDateWhileDraft()
    {
        Expense expense = CreateExpense(PaymentTerm.Cash, netAmount: 1000m, taxRate: 13m);
        DateTimeOffset updatedAtUtc = CreatedAtUtc.AddDays(1);

        expense.UpdateHeader(
            expense.SupplierId,
            expense.CategoryId,
            "Compra corregida",
            IssueDate,
            PaymentTerm.Credit60,
            2000m,
            taxTypeId: null,
            "IVA-EXENTO",
            "IVA exento",
            0m,
            PaymentMethod.BankTransfer,
            updatedAtUtc,
            CreatorId);

        Assert.Equal("Compra corregida", expense.Description);
        Assert.Equal(IssueDate.AddDays(60), expense.DueDate);
        Assert.Equal(2000m, expense.NetAmount);
        Assert.Equal(0m, expense.TaxAmount);
        Assert.Equal(2000m, expense.TotalAmount);
        Assert.Equal(PaymentMethod.BankTransfer, expense.PaymentMethod);
        Assert.Equal(updatedAtUtc, expense.UpdatedAtUtc);
    }

    [Fact]
    public void UpdateHeader_RejectsModifyingAConfirmedExpense()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);
        expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, cashAccountId: null, reference: null);

        Assert.Throws<InvalidOperationException>(() => expense.UpdateHeader(
            expense.SupplierId,
            expense.CategoryId,
            expense.Description,
            IssueDate,
            PaymentTerm.Credit30,
            1000m,
            null,
            expense.TaxCode,
            expense.TaxName,
            13m,
            expense.PaymentMethod,
            CreatedAtUtc.AddDays(2),
            CreatorId));
    }

    [Fact]
    public void Confirm_Cash_RequiresCashAccountAndReference()
    {
        Expense expense = CreateExpense(PaymentTerm.Cash, netAmount: 1000m, taxRate: 13m);

        Assert.Throws<ArgumentException>(() =>
            expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, cashAccountId: null, reference: "REF-1"));
        Assert.Throws<ArgumentException>(() =>
            expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, cashAccountId: Guid.NewGuid(), reference: null));
    }

    [Fact]
    public void Confirm_Cash_SetsCashAccountReferenceAndConfirmedStatus()
    {
        Expense expense = CreateExpense(PaymentTerm.Cash, netAmount: 1000m, taxRate: 13m);
        Guid cashAccountId = Guid.NewGuid();
        DateTimeOffset confirmedAtUtc = CreatedAtUtc.AddDays(1);

        expense.Confirm(confirmedAtUtc, CreatorId, cashAccountId, "REF-1");

        Assert.Equal(ExpenseStatus.Confirmed, expense.Status);
        Assert.Equal(cashAccountId, expense.CashAccountId);
        Assert.Equal("REF-1", expense.Reference);
        Assert.Equal(confirmedAtUtc, expense.ConfirmedAtUtc);
        Assert.Equal(CreatorId, expense.ConfirmedByUserId);
    }

    [Fact]
    public void Confirm_Credit_RejectsACashAccountOrAReference()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);

        Assert.Throws<ArgumentException>(() =>
            expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, Guid.NewGuid(), reference: null));
        Assert.Throws<ArgumentException>(() =>
            expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, cashAccountId: null, reference: "REF-1"));
    }

    [Fact]
    public void Confirm_Credit_SetsConfirmedStatusWithoutCashData()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);

        expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, cashAccountId: null, reference: null);

        Assert.Equal(ExpenseStatus.Confirmed, expense.Status);
        Assert.Null(expense.CashAccountId);
        Assert.Null(expense.Reference);
    }

    [Fact]
    public void Confirm_RejectsAnAlreadyConfirmedExpense()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);
        expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, null, null);

        Assert.Throws<InvalidOperationException>(() =>
            expense.Confirm(CreatedAtUtc.AddDays(2), CreatorId, null, null));
    }

    [Fact]
    public void Cancel_MarksItCancelledWhenConfirmed()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);
        expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, null, null);
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(2);

        expense.Cancel("Compra duplicada", cancelledAtUtc, CreatorId);

        Assert.Equal(ExpenseStatus.Cancelled, expense.Status);
        Assert.Equal("Compra duplicada", expense.CancellationReason);
        Assert.Equal(cancelledAtUtc, expense.CancelledAtUtc);
        Assert.Equal(CreatorId, expense.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsADraftExpense()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);

        Assert.Throws<InvalidOperationException>(() =>
            expense.Cancel("Motivo", CreatedAtUtc.AddDays(1), CreatorId));
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledExpense()
    {
        Expense expense = CreateExpense(PaymentTerm.Credit30, netAmount: 1000m, taxRate: 13m);
        expense.Confirm(CreatedAtUtc.AddDays(1), CreatorId, null, null);
        expense.Cancel("Primera anulación", CreatedAtUtc.AddDays(2), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            expense.Cancel("Segunda anulación", CreatedAtUtc.AddDays(3), CreatorId));
    }

    private static Expense CreateExpense(PaymentTerm paymentTerm, decimal netAmount, decimal taxRate) => new(
        Guid.NewGuid(),
        "GAS-001",
        Guid.NewGuid(),
        Guid.NewGuid(),
        "Compra de prueba",
        IssueDate,
        paymentTerm,
        netAmount,
        Guid.NewGuid(),
        "IVA",
        "Impuesto al valor agregado",
        taxRate,
        PaymentMethod.Cash,
        CreatedAtUtc,
        CreatorId);
}
