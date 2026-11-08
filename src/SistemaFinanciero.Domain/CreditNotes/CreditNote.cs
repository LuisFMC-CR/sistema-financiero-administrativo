using SistemaFinanciero.Domain.Common;

namespace SistemaFinanciero.Domain.CreditNotes;

/// <summary>
/// Nota de crédito interna por el total de una factura. Su registro anula la factura y liquida el
/// saldo pendiente o devuelve al cliente el efectivo cobrado.
/// </summary>
public sealed class CreditNote
{
    public const int CodeMaxLength = 30;
    public const int ReasonMaxLength = 300;
    public const int RefundReferenceMaxLength = 100;
    public const int ElectronicCreditNoteKeyLength = 50;
    public const int ElectronicCreditNoteNumberLength = 20;

    private readonly List<CreditNoteLine> lines = [];

    private CreditNote()
    {
    }

    public CreditNote(
        Guid id,
        string code,
        Guid invoiceId,
        DateOnly issueDate,
        string reason,
        decimal appliedToBalance,
        decimal refundedAmount,
        decimal withholdingReversedAmount,
        Guid? cashAccountId,
        string? refundReference,
        string electronicCreditNoteKey,
        string electronicCreditNoteNumber,
        IReadOnlyList<CreditNoteLine> creditNoteLines,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        ArgumentNullException.ThrowIfNull(creditNoteLines);

        Id = DomainRules.RequiredId(id, nameof(id));
        Code = DomainRules.NormalizedCode(code, CodeMaxLength, nameof(code));
        InvoiceId = DomainRules.RequiredId(invoiceId, nameof(invoiceId));
        IssueDate = issueDate != default
            ? issueDate
            : throw new ArgumentException("La fecha de la nota de crédito es obligatoria.", nameof(issueDate));
        Reason = DomainRules.RequiredText(reason, ReasonMaxLength, nameof(reason));
        AppliedToBalance = RequireNonNegative(appliedToBalance, nameof(appliedToBalance));
        RefundedAmount = RequireNonNegative(refundedAmount, nameof(refundedAmount));
        WithholdingReversedAmount = RequireNonNegative(withholdingReversedAmount, nameof(withholdingReversedAmount));
        CashAccountId = DomainRules.OptionalId(cashAccountId, nameof(cashAccountId));
        RefundReference = NormalizeOptionalText(refundReference, RefundReferenceMaxLength, nameof(refundReference));
        ElectronicCreditNoteKey = DomainRules.RequiredDigits(
            electronicCreditNoteKey, ElectronicCreditNoteKeyLength, nameof(electronicCreditNoteKey));
        ElectronicCreditNoteNumber = DomainRules.RequiredDigits(
            electronicCreditNoteNumber, ElectronicCreditNoteNumberLength, nameof(electronicCreditNoteNumber));
        CreatedAtUtc = DomainRules.RequiredUtcTimestamp(createdAtUtc, nameof(createdAtUtc));
        CreatedByUserId = DomainRules.RequiredId(createdByUserId, nameof(createdByUserId));
        UpdatedAtUtc = CreatedAtUtc;
        UpdatedByUserId = CreatedByUserId;
        Status = CreditNoteStatus.Active;

        if (RefundedAmount > 0 && (CashAccountId is null || RefundReference is null))
        {
            throw new ArgumentException("La cuenta de efectivo y la referencia son obligatorias para una devolución.");
        }

        if (RefundedAmount == 0 && (CashAccountId is not null || RefundReference is not null))
        {
            throw new ArgumentException("No se debe indicar cuenta ni referencia cuando no existe devolución.");
        }

        if (creditNoteLines.Count == 0)
        {
            throw new ArgumentException("La nota debe conservar al menos una línea de la factura.", nameof(creditNoteLines));
        }

        if (creditNoteLines.Select(line => line.InvoiceLineId).Distinct().Count() != creditNoteLines.Count)
        {
            throw new ArgumentException("La nota no puede repetir líneas de la factura.", nameof(creditNoteLines));
        }

        NetAmount = DomainRules.RoundMoney(creditNoteLines.Sum(line => line.NetAmount));
        TaxAmount = DomainRules.RoundMoney(creditNoteLines.Sum(line => line.TaxAmount));
        TotalAmount = DomainRules.RoundMoney(NetAmount + TaxAmount);

        if (TotalAmount != DomainRules.RoundMoney(AppliedToBalance + RefundedAmount + WithholdingReversedAmount))
        {
            throw new ArgumentException(
                "El total de la nota debe coincidir con el saldo aplicado, el efectivo devuelto y las retenciones revertidas.");
        }

        lines.AddRange(creditNoteLines);
    }

    public Guid Id { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public Guid InvoiceId { get; private set; }

    public DateOnly IssueDate { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public decimal NetAmount { get; private set; }

    public decimal TaxAmount { get; private set; }

    public decimal TotalAmount { get; private set; }

    public decimal AppliedToBalance { get; private set; }

    public decimal RefundedAmount { get; private set; }

    public decimal WithholdingReversedAmount { get; private set; }

    public Guid? CashAccountId { get; private set; }

    public string? RefundReference { get; private set; }

    public string? ElectronicCreditNoteKey { get; private set; }

    public string? ElectronicCreditNoteNumber { get; private set; }

    public CreditNoteStatus Status { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<CreditNoteLine> Lines => lines.AsReadOnly();

    private static decimal RequireNonNegative(decimal value, string parameterName)
    {
        decimal rounded = DomainRules.RoundMoney(value);
        return rounded >= 0
            ? rounded
            : throw new ArgumentOutOfRangeException(parameterName, value, "El monto no puede ser negativo.");
    }

    private static string? NormalizeOptionalText(string? value, int maxLength, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : DomainRules.RequiredText(value, maxLength, parameterName);
    }
}
