using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Invoices;

public sealed class InvoiceDraftEditingTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EditedAtUtc = CreatedAtUtc.AddMinutes(10);
    private static readonly DateOnly IssueDate = new(2026, 9, 10);
    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void UpdateHeader_ChangesTheHeaderAndTheAudit()
    {
        Invoice invoice = CreateInvoice();
        Guid newCustomerId = Guid.NewGuid();
        DateOnly newIssueDate = IssueDate.AddDays(-2);
        DateOnly dueDate = newIssueDate.AddDays(30);

        invoice.UpdateHeader(
            newCustomerId,
            newIssueDate,
            CurrencyCode.USD,
            PaymentTerm.Credit,
            dueDate,
            EditedAtUtc,
            EditorId);

        Assert.Equal(newCustomerId, invoice.CustomerId);
        Assert.Equal(newIssueDate, invoice.IssueDate);
        Assert.Equal(CurrencyCode.USD, invoice.Currency);
        Assert.Equal(PaymentTerm.Credit, invoice.PaymentTerm);
        Assert.Equal(dueDate, invoice.DueDate);
        Assert.Equal(EditedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(EditorId, invoice.UpdatedByUserId);
        Assert.Equal(CreatorId, invoice.CreatedByUserId);
    }

    [Fact]
    public void UpdateHeader_ClearsTheDueDateWhenTheInvoiceBecomesCash()
    {
        Invoice invoice = CreateInvoice(PaymentTerm.Credit, IssueDate.AddDays(30));

        invoice.UpdateHeader(
            invoice.CustomerId,
            IssueDate,
            CurrencyCode.CRC,
            PaymentTerm.Cash,
            dueDate: null,
            EditedAtUtc,
            EditorId);

        Assert.Equal(PaymentTerm.Cash, invoice.PaymentTerm);
        Assert.Null(invoice.DueDate);
    }

    [Fact]
    public void UpdateHeader_ValidatesTheDueDateAgainstTheNewIssueDate()
    {
        Invoice invoice = CreateInvoice(PaymentTerm.Credit, IssueDate.AddDays(5));

        // El vencimiento anterior es válido para la emisión original, pero no para la nueva.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            invoice.UpdateHeader(
                invoice.CustomerId,
                IssueDate.AddDays(10),
                CurrencyCode.CRC,
                PaymentTerm.Credit,
                IssueDate.AddDays(5),
                EditedAtUtc,
                EditorId));
    }

    [Fact]
    public void UpdateHeader_LeavesTheInvoiceUntouchedWhenTheNewDataIsInvalid()
    {
        Invoice invoice = CreateInvoice();
        Guid originalCustomerId = invoice.CustomerId;

        Assert.Throws<ArgumentException>(() =>
            invoice.UpdateHeader(
                Guid.NewGuid(),
                IssueDate.AddDays(1),
                CurrencyCode.USD,
                PaymentTerm.Credit,
                dueDate: null,
                EditedAtUtc,
                EditorId));

        Assert.Equal(originalCustomerId, invoice.CustomerId);
        Assert.Equal(IssueDate, invoice.IssueDate);
        Assert.Equal(CurrencyCode.CRC, invoice.Currency);
        Assert.Equal(PaymentTerm.Cash, invoice.PaymentTerm);
        Assert.Equal(CreatedAtUtc, invoice.UpdatedAtUtc);
        Assert.Equal(CreatorId, invoice.UpdatedByUserId);
    }

    [Fact]
    public void UpdateHeader_AllowsChangingTheCurrencyWhileThereAreNoLines()
    {
        Invoice invoice = CreateInvoice();

        invoice.UpdateHeader(
            invoice.CustomerId,
            IssueDate,
            CurrencyCode.USD,
            PaymentTerm.Cash,
            dueDate: null,
            EditedAtUtc,
            EditorId);

        Assert.Equal(CurrencyCode.USD, invoice.Currency);
    }

    [Fact]
    public void UpdateHeader_RejectsChangingTheCurrencyOnceThereAreLines()
    {
        Invoice invoice = CreateInvoice();
        invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 100m), EditedAtUtc, CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.UpdateHeader(
                invoice.CustomerId,
                IssueDate,
                CurrencyCode.USD,
                PaymentTerm.Cash,
                dueDate: null,
                EditedAtUtc.AddMinutes(1),
                EditorId));

        Assert.Equal(CurrencyCode.CRC, invoice.Currency);
    }

    [Fact]
    public void UpdateHeader_AllowsOtherChangesWhenThereAreLinesAndTheCurrencyIsTheSame()
    {
        Invoice invoice = CreateInvoice();
        invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 100m), EditedAtUtc, CreatorId);
        DateOnly newIssueDate = IssueDate.AddDays(1);

        invoice.UpdateHeader(
            invoice.CustomerId,
            newIssueDate,
            CurrencyCode.CRC,
            PaymentTerm.Cash,
            dueDate: null,
            EditedAtUtc.AddMinutes(1),
            EditorId);

        Assert.Equal(newIssueDate, invoice.IssueDate);
    }

    [Fact]
    public void ReplaceLine_SwapsTheLineInTheSamePositionAndRecalculatesTheTotals()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine first = CreateLine(quantity: 1m, unitPrice: 100m);
        InvoiceLine second = CreateLine(quantity: 2m, unitPrice: 50m);
        invoice.AddLine(first, EditedAtUtc, CreatorId);
        invoice.AddLine(second, EditedAtUtc, CreatorId);
        InvoiceLine replacement = CreateLine(quantity: 1m, unitPrice: 300m, taxes: [Vat(13m)]);

        invoice.ReplaceLine(first.Id, replacement, EditedAtUtc.AddMinutes(1), EditorId);

        Assert.Equal([replacement.Id, second.Id], invoice.Lines.Select(line => line.Id));
        Assert.Equal(400m, invoice.NetAmount);
        Assert.Equal(39m, invoice.TaxAmount);
        Assert.Equal(439m, invoice.TotalAmount);
        Assert.Equal(EditorId, invoice.UpdatedByUserId);
    }

    [Fact]
    public void ReplaceLine_RejectsALineThatIsNotPartOfTheInvoice()
    {
        Invoice invoice = CreateInvoice();
        invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 100m), EditedAtUtc, CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.ReplaceLine(
                Guid.NewGuid(),
                CreateLine(quantity: 1m, unitPrice: 10m),
                EditedAtUtc.AddMinutes(1),
                EditorId));
    }

    [Fact]
    public void ReplaceLine_RejectsAReplacementWhoseIdAlreadyExists()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine first = CreateLine(quantity: 1m, unitPrice: 100m);
        invoice.AddLine(first, EditedAtUtc, CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.ReplaceLine(first.Id, first, EditedAtUtc.AddMinutes(1), EditorId));
    }

    [Fact]
    public void RemoveLine_DeletesTheLineAndRecalculatesTheTotals()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine first = CreateLine(quantity: 1m, unitPrice: 100m, taxes: [Vat(13m)]);
        InvoiceLine second = CreateLine(quantity: 2m, unitPrice: 50m);
        invoice.AddLine(first, EditedAtUtc, CreatorId);
        invoice.AddLine(second, EditedAtUtc, CreatorId);

        invoice.RemoveLine(first.Id, EditedAtUtc.AddMinutes(1), EditorId);

        Assert.Equal([second.Id], invoice.Lines.Select(line => line.Id));
        Assert.Equal(100m, invoice.GrossAmount);
        Assert.Equal(0m, invoice.TaxAmount);
        Assert.Equal(100m, invoice.TotalAmount);
    }

    [Fact]
    public void RemoveLine_RejectsALineThatIsNotPartOfTheInvoice()
    {
        Invoice invoice = CreateInvoice();

        Assert.Throws<InvalidOperationException>(() =>
            invoice.RemoveLine(Guid.NewGuid(), EditedAtUtc, EditorId));
    }

    [Fact]
    public void RemoveLine_LeavesTheInvoiceEmptyAndNotConfirmable()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 100m);
        invoice.AddLine(line, EditedAtUtc, CreatorId);

        invoice.RemoveLine(line.Id, EditedAtUtc.AddMinutes(1), EditorId);

        Assert.Empty(invoice.Lines);
        Assert.Equal(0m, invoice.TotalAmount);
        Assert.Throws<InvalidOperationException>(() =>
            invoice.Confirm(null, EditedAtUtc.AddMinutes(2), EditorId));
    }

    [Fact]
    public void AddLine_RejectsARepeatedLineId()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 100m);
        invoice.AddLine(line, EditedAtUtc, CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.AddLine(line, EditedAtUtc.AddMinutes(1), CreatorId));
    }

    [Fact]
    public void AnEditWithAnEmptyUserLeavesTheLinesUntouched()
    {
        Invoice invoice = CreateInvoice();

        Assert.Throws<ArgumentException>(() =>
            invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 100m), EditedAtUtc, Guid.Empty));

        Assert.Empty(invoice.Lines);
        Assert.Equal(CreatedAtUtc, invoice.UpdatedAtUtc);
    }

    [Fact]
    public void ConfirmedInvoice_RejectsEveryDraftEdit()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 100m);
        invoice.AddLine(line, EditedAtUtc, CreatorId);
        invoice.Confirm(null, EditedAtUtc.AddMinutes(1), CreatorId);
        DateTimeOffset later = EditedAtUtc.AddMinutes(2);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.UpdateHeader(
                invoice.CustomerId,
                IssueDate,
                CurrencyCode.CRC,
                PaymentTerm.Cash,
                dueDate: null,
                later,
                EditorId));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.AddLine(CreateLine(quantity: 1m, unitPrice: 10m), later, EditorId));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.ReplaceLine(line.Id, CreateLine(quantity: 1m, unitPrice: 10m), later, EditorId));
        Assert.Throws<InvalidOperationException>(() =>
            invoice.RemoveLine(line.Id, later, EditorId));

        Assert.Single(invoice.Lines);
        Assert.Equal(100m, invoice.TotalAmount);
    }

    [Fact]
    public void CancelledInvoice_RejectsDraftEdits()
    {
        Invoice invoice = CreateInvoice();
        InvoiceLine line = CreateLine(quantity: 1m, unitPrice: 100m);
        invoice.AddLine(line, EditedAtUtc, CreatorId);
        invoice.Confirm(null, EditedAtUtc.AddMinutes(1), CreatorId);
        invoice.Cancel("Error de digitación", EditedAtUtc.AddMinutes(2), CreatorId);

        Assert.Throws<InvalidOperationException>(() =>
            invoice.RemoveLine(line.Id, EditedAtUtc.AddMinutes(3), EditorId));
    }

    private static Invoice CreateInvoice(PaymentTerm paymentTerm = PaymentTerm.Cash, DateOnly? dueDate = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            IssueDate,
            CurrencyCode.CRC,
            paymentTerm,
            dueDate,
            CreatedAtUtc,
            CreatorId);

    private static InvoiceLine CreateLine(
        decimal quantity,
        decimal unitPrice,
        IEnumerable<InvoiceTaxSpecification>? taxes = null) =>
        new(
            Guid.NewGuid(),
            catalogItemId: null,
            "Servicio de soporte",
            "hora",
            quantity,
            unitPrice,
            discountAmount: 0m,
            taxes);

    private static InvoiceTaxSpecification Vat(decimal rate) =>
        new(Guid.NewGuid(), "IVA", "Impuesto al valor agregado", TaxCalculationType.Percentage, rate);
}
