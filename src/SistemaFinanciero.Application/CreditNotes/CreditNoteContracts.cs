using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Domain.CreditNotes;

namespace SistemaFinanciero.Application.CreditNotes;

public sealed record CreditNoteInvoiceOption(
    Guid InvoiceId,
    string InvoiceVersion,
    long InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    DateOnly? DueDate,
    bool IsCashInvoice,
    decimal InvoiceTotal,
    decimal ReceivableBalance,
    decimal CashReceived,
    decimal WithholdingAmount,
    bool HasBalanceAdjustments);

public sealed record CreditNoteQuery(
    string? Code = null,
    string? CustomerName = null,
    long? InvoiceNumber = null,
    DateOnly? IssueDate = null,
    CreditNoteStatus? Status = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CreditNoteListItem(
    Guid Id,
    string Code,
    long InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    decimal TotalAmount,
    decimal RefundedAmount,
    CreditNoteStatus Status);

public sealed record CreditNoteLineModel(
    Guid InvoiceLineId,
    int Position,
    string Description,
    string UnitOfMeasure,
    decimal Quantity,
    decimal NetAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount);

public sealed record CreditNoteModel(
    Guid Id,
    string Code,
    Guid InvoiceId,
    long InvoiceNumber,
    string CustomerName,
    DateOnly IssueDate,
    string Reason,
    decimal NetAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal AppliedToBalance,
    decimal RefundedAmount,
    decimal WithholdingReversedAmount,
    string? CashAccountName,
    string? RefundReference,
    string? ElectronicCreditNoteKey,
    string? ElectronicCreditNoteNumber,
    CreditNoteStatus Status,
    DateTimeOffset CreatedAtUtc,
    string CreatedByFullName,
    IReadOnlyList<CreditNoteLineModel> Lines,
    string Version);

public sealed record RegisterCreditNoteCommand(
    Guid InvoiceId,
    DateOnly IssueDate,
    string Reason,
    Guid? CashAccountId,
    string? RefundReference,
    string InvoiceVersion);

public sealed record CreditNoteRegistrationResult(
    CatalogOperationResult Operation,
    Guid? CreditNoteId,
    bool DuplicateReferenceWarning = false);

/// <summary>Casos de uso de consulta y registro de notas de crédito internas.</summary>
public interface ICreditNoteService
{
    Task<PagedResult<CreditNoteInvoiceOption>> SearchEligibleInvoicesAsync(
        string? invoiceOrCustomer,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<PagedResult<CreditNoteListItem>> SearchAsync(
        CreditNoteQuery query,
        CancellationToken cancellationToken = default);

    Task<CreditNoteInvoiceOption?> GetInvoiceAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default);

    Task<CreditNoteModel?> GetAsync(
        Guid creditNoteId,
        CancellationToken cancellationToken = default);

    Task<CreditNoteRegistrationResult> RegisterAsync(
        RegisterCreditNoteCommand command,
        Guid actorId,
        bool actorIsManagement,
        CancellationToken cancellationToken = default);
}
