using SistemaFinanciero.Domain.Audit;

namespace SistemaFinanciero.UnitTests.Audit;

public sealed class OperationLogEntryTests
{
    private static readonly DateTimeOffset PerformedAtUtc =
        new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid ActorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Constructor_KeepsTheConfirmationWithoutAReason()
    {
        Guid invoiceId = Guid.NewGuid();

        OperationLogEntry entry = new(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            invoiceId,
            OperationLogAction.Confirmed,
            reason: null,
            PerformedAtUtc,
            ActorId);

        Assert.Equal(OperationLogEntityType.Invoice, entry.EntityType);
        Assert.Equal(invoiceId, entry.EntityId);
        Assert.Equal(OperationLogAction.Confirmed, entry.Action);
        Assert.Null(entry.Reason);
        Assert.Equal(PerformedAtUtc, entry.PerformedAtUtc);
        Assert.Equal(ActorId, entry.PerformedByUserId);
    }

    [Fact]
    public void Constructor_KeepsTheCancellationReason()
    {
        OperationLogEntry entry = new(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            OperationLogAction.Cancelled,
            "Error en el monto facturado.",
            PerformedAtUtc,
            ActorId);

        Assert.Equal(OperationLogAction.Cancelled, entry.Action);
        Assert.Equal("Error en el monto facturado.", entry.Reason);
    }

    [Fact]
    public void Constructor_TrimsTheReason()
    {
        OperationLogEntry entry = new(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            OperationLogAction.Cancelled,
            "  Motivo con espacios.  ",
            PerformedAtUtc,
            ActorId);

        Assert.Equal("Motivo con espacios.", entry.Reason);
    }

    [Fact]
    public void Constructor_RejectsAnEmptyId()
    {
        Assert.Throws<ArgumentException>(() => new OperationLogEntry(
            Guid.Empty,
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            OperationLogAction.Confirmed,
            null,
            PerformedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsAnEmptyEntityId()
    {
        Assert.Throws<ArgumentException>(() => new OperationLogEntry(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.Empty,
            OperationLogAction.Confirmed,
            null,
            PerformedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedEntityType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperationLogEntry(
            Guid.NewGuid(),
            (OperationLogEntityType)999,
            Guid.NewGuid(),
            OperationLogAction.Confirmed,
            null,
            PerformedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedAction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OperationLogEntry(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            (OperationLogAction)999,
            null,
            PerformedAtUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RequiresAnActor()
    {
        Assert.Throws<ArgumentException>(() => new OperationLogEntry(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            OperationLogAction.Confirmed,
            null,
            PerformedAtUtc,
            Guid.Empty));
    }

    [Fact]
    public void Constructor_RequiresAnUtcTimestamp()
    {
        DateTimeOffset nonUtc = new(2026, 10, 22, 12, 0, 0, TimeSpan.FromHours(-6));

        Assert.Throws<ArgumentException>(() => new OperationLogEntry(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            OperationLogAction.Confirmed,
            null,
            nonUtc,
            ActorId));
    }

    [Fact]
    public void Constructor_RejectsAReasonLongerThanTheMaximum()
    {
        string tooLong = new('a', OperationLogEntry.ReasonMaxLength + 1);

        Assert.Throws<ArgumentException>(() => new OperationLogEntry(
            Guid.NewGuid(),
            OperationLogEntityType.Invoice,
            Guid.NewGuid(),
            OperationLogAction.Cancelled,
            tooLong,
            PerformedAtUtc,
            ActorId));
    }
}
