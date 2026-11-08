using SistemaFinanciero.Domain.CashTransfers;

namespace SistemaFinanciero.UnitTests.CashTransfers;

public sealed class CashTransferTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateOnly TransferDate = new(2026, 11, 8);

    [Fact]
    public void Constructor_StartsActiveWithTheGivenData()
    {
        Guid sourceId = Guid.NewGuid();
        Guid destinationId = Guid.NewGuid();

        CashTransfer transfer = new(
            Guid.NewGuid(),
            "TRA-000001",
            sourceId,
            destinationId,
            500m,
            "DEP-0001",
            "Depósito de caja chica al banco",
            TransferDate,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(sourceId, transfer.SourceCashAccountId);
        Assert.Equal(destinationId, transfer.DestinationCashAccountId);
        Assert.Equal(500m, transfer.Amount);
        Assert.Equal("DEP-0001", transfer.Reference);
        Assert.Equal("Depósito de caja chica al banco", transfer.Description);
        Assert.Equal(TransferDate, transfer.TransferDate);
        Assert.Equal(CashTransferStatus.Active, transfer.Status);
    }

    [Fact]
    public void Constructor_RejectsTheSameSourceAndDestinationAccount()
    {
        Guid accountId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new CashTransfer(
            Guid.NewGuid(),
            "TRA-000001",
            accountId,
            accountId,
            500m,
            "DEP-0001",
            "Depósito",
            TransferDate,
            CreatedAtUtc,
            CreatorId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsAnAmountThatIsNotPositive(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTransfer(amount: amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RequiresAReference(string reference)
    {
        Assert.Throws<ArgumentException>(() => CreateTransfer(reference: reference));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RequiresADescription(string description)
    {
        Assert.Throws<ArgumentException>(() => CreateTransfer(description: description));
    }

    [Fact]
    public void Cancel_KeepsTheReasonAndTheAudit()
    {
        CashTransfer transfer = CreateTransfer();
        DateTimeOffset cancelledAtUtc = CreatedAtUtc.AddDays(1);

        transfer.Cancel("  Traspaso registrado por error  ", cancelledAtUtc, CreatorId);

        Assert.Equal(CashTransferStatus.Cancelled, transfer.Status);
        Assert.Equal("Traspaso registrado por error", transfer.CancellationReason);
        Assert.Equal(cancelledAtUtc, transfer.CancelledAtUtc);
        Assert.Equal(CreatorId, transfer.CancelledByUserId);
    }

    [Fact]
    public void Cancel_RejectsAnAlreadyCancelledTransfer()
    {
        CashTransfer transfer = CreateTransfer();
        transfer.Cancel("Motivo", CreatedAtUtc.AddDays(1), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            transfer.Cancel("Otra vez", CreatedAtUtc.AddDays(2), CreatorId));
    }

    private static CashTransfer CreateTransfer(
        decimal amount = 500m,
        string reference = "DEP-0001",
        string description = "Depósito de caja chica al banco") => new(
        Guid.NewGuid(),
        "TRA-000001",
        Guid.NewGuid(),
        Guid.NewGuid(),
        amount,
        reference,
        description,
        TransferDate,
        CreatedAtUtc,
        CreatorId);
}
