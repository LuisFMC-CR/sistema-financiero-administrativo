using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.CreditNotes;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Web.Models.CreditNotes;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.Web.Controllers;

[Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
[Route("notas-de-credito")]
public sealed class CreditNotesController(
    ICreditNoteService service,
    ILedgerAccountService ledgerAccountService,
    ISystemParametersService systemParametersService,
    TimeProvider timeProvider) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        string? normalizedSearch = NormalizeSearch(search);
        PagedResult<CreditNoteInvoiceOption> eligibleInvoices =
            await service.SearchEligibleInvoicesAsync(normalizedSearch, page, cancellationToken: cancellationToken);
        CreditNoteQuery creditNoteQuery = long.TryParse(normalizedSearch, out long invoiceNumber)
            ? new CreditNoteQuery(InvoiceNumber: invoiceNumber, Page: page)
            : new CreditNoteQuery(CustomerName: normalizedSearch, Page: page);
        PagedResult<CreditNoteListItem> creditNotes =
            await service.SearchAsync(creditNoteQuery, cancellationToken);

        return View(new CreditNoteIndexViewModel(eligibleInvoices, creditNotes, normalizedSearch));
    }

    [HttpGet("{invoiceId:guid}/registrar")]
    public async Task<IActionResult> Register(Guid invoiceId, CancellationToken cancellationToken)
    {
        CreditNoteInvoiceOption? invoice = await service.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        CreditNoteRegisterViewModel model = new()
        {
            Invoice = invoice,
            IssueDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime),
            InvoiceVersion = invoice.InvoiceVersion,
        };
        await PopulateCashAccountOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost("{invoiceId:guid}/registrar")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(
        Guid invoiceId,
        CreditNoteRegisterViewModel model,
        CancellationToken cancellationToken)
    {
        CreditNoteInvoiceOption? invoice = await service.GetInvoiceAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return NotFound();
        }

        model.Invoice = invoice;
        if (!ModelState.IsValid)
        {
            await PopulateCashAccountOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CreditNoteRegistrationResult result = await service.RegisterAsync(
            new RegisterCreditNoteCommand(
                invoiceId,
                model.IssueDate,
                model.Reason,
                model.CashAccountId,
                model.RefundReference,
                model.InvoiceVersion),
            actorId,
            User.IsInRole(SystemRoles.Management),
            cancellationToken);

        if (result.Operation.IsSuccess && result.CreditNoteId is Guid creditNoteId)
        {
            SetSuccessMessage("La nota de crédito fue registrada y la factura quedó anulada.");
            if (result.DuplicateReferenceWarning)
            {
                TempData["WarningMessage"] =
                    "La referencia ya se utilizó en esta cuenta de efectivo. La devolución se registró de todas formas.";
            }

            return RedirectToAction(nameof(Details), new { id = creditNoteId });
        }

        if (result.Operation.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            model.InvoiceVersion = invoice.InvoiceVersion;
            ModelState.Remove(nameof(model.InvoiceVersion));
        }

        await PopulateCashAccountOptionsAsync(model, cancellationToken);
        return FormFailure(result.Operation, model, nameof(Register));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        CreditNoteModel? note = await service.GetAsync(id, cancellationToken);
        return note is null ? NotFound() : View(note);
    }

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        CreditNoteModel? note = await service.GetAsync(id, cancellationToken);
        if (note is null)
        {
            return NotFound();
        }

        SystemParametersModel? companyData = await systemParametersService.GetAsync(cancellationToken);
        byte[] pdf = new CreditNotePdfDocument(note, companyData).GeneratePdf();
        return File(pdf, "application/pdf", $"nota-credito-{note.Code.ToLowerInvariant()}.pdf");
    }

    private async Task PopulateCashAccountOptionsAsync(
        CreditNoteRegisterViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> accounts =
            await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountOptions = accounts
            .Select(account => new SelectListItem(
                $"{account.Code} — {account.Name}",
                account.Id.ToString(),
                account.Id == model.CashAccountId))
            .ToArray();
    }
}
