using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceLinePositionTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private int minutes;

    [Fact]
    public void ALineThatBelongsToNoInvoiceHasNoPosition()
    {
        InvoiceLine line = CreateLine("A");

        Assert.Equal(0, line.Position);
    }

    [Fact]
    public void AddLine_AssignsConsecutivePositionsInTheOrderOfCapture()
    {
        Invoice invoice = CreateInvoiceWith("A", "B", "C");

        Assert.Equal([1, 2, 3], invoice.Lines.Select(line => line.Position));
        Assert.Equal(["A", "B", "C"], invoice.Lines.Select(line => line.Description));
    }

    [Fact]
    public void ReplaceLine_GivesTheReplacementThePositionOfTheReplacedLine()
    {
        Invoice invoice = CreateInvoiceWith("A", "B", "C");
        Guid middleId = invoice.Lines.ElementAt(1).Id;

        invoice.ReplaceLine(middleId, CreateLine("B2"), Next(), UserId);

        Assert.Equal(["A", "B2", "C"], invoice.Lines.Select(line => line.Description));
        Assert.Equal([1, 2, 3], invoice.Lines.Select(line => line.Position));
    }

    [Fact]
    public void RemoveLine_RenumbersTheFollowingLinesFromTheMiddle()
    {
        Invoice invoice = CreateInvoiceWith("A", "B", "C", "D");
        Guid middleId = invoice.Lines.ElementAt(1).Id;

        invoice.RemoveLine(middleId, Next(), UserId);

        Assert.Equal(["A", "C", "D"], invoice.Lines.Select(line => line.Description));
        Assert.Equal([1, 2, 3], invoice.Lines.Select(line => line.Position));
    }

    [Fact]
    public void RemoveLine_RenumbersEveryLineWhenTheFirstOneIsRemoved()
    {
        Invoice invoice = CreateInvoiceWith("A", "B", "C");
        Guid firstId = invoice.Lines.First().Id;

        invoice.RemoveLine(firstId, Next(), UserId);

        Assert.Equal(["B", "C"], invoice.Lines.Select(line => line.Description));
        Assert.Equal([1, 2], invoice.Lines.Select(line => line.Position));
    }

    [Fact]
    public void RemoveLine_LeavesThePositionsUntouchedWhenTheLastOneIsRemoved()
    {
        Invoice invoice = CreateInvoiceWith("A", "B", "C");
        Guid lastId = invoice.Lines.Last().Id;

        invoice.RemoveLine(lastId, Next(), UserId);

        Assert.Equal(["A", "B"], invoice.Lines.Select(line => line.Description));
        Assert.Equal([1, 2], invoice.Lines.Select(line => line.Position));
    }

    [Fact]
    public void AddLine_ContinuesTheSequenceAfterALineWasRemoved()
    {
        Invoice invoice = CreateInvoiceWith("A", "B", "C");
        invoice.RemoveLine(invoice.Lines.First().Id, Next(), UserId);

        invoice.AddLine(CreateLine("D"), Next(), UserId);

        Assert.Equal(["B", "C", "D"], invoice.Lines.Select(line => line.Description));
        Assert.Equal([1, 2, 3], invoice.Lines.Select(line => line.Position));
    }

    [Fact]
    public void RemovingTheOnlyLineAndAddingAnotherStartsAtOneAgain()
    {
        Invoice invoice = CreateInvoiceWith("A");
        invoice.RemoveLine(invoice.Lines.Single().Id, Next(), UserId);

        invoice.AddLine(CreateLine("B"), Next(), UserId);

        Assert.Equal(1, invoice.Lines.Single().Position);
    }

    private Invoice CreateInvoiceWith(params string[] descriptions)
    {
        Invoice invoice = new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2026, 9, 10),
            CurrencyCode.CRC,
            PaymentTerm.Cash,
            dueDate: null,
            CreatedAtUtc,
            UserId);

        foreach (string description in descriptions)
        {
            invoice.AddLine(CreateLine(description), Next(), UserId);
        }

        return invoice;
    }

    // Cada operación usa un instante posterior al anterior para respetar la auditoría creciente.
    private DateTimeOffset Next() => CreatedAtUtc.AddMinutes(++minutes);

    private static InvoiceLine CreateLine(string description) =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            description,
            "hora",
            quantity: 1m,
            unitPrice: 100m,
            discountAmount: 0m,
            taxes: null);
}
