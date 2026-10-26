using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.Customers;
using SistemaFinanciero.Application.Catalogs.Items;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Invoices;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Models.Invoices;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el ciclo de vida de la factura administrativa: borrador, confirmación y anulación.</summary>
/// <remarks>
/// Conexiones: usa <c>IInvoiceService</c> (Application) para la factura, <c>ICustomerCatalogService</c>
/// para el selector de cliente, <c>ICatalogItemService</c> para el selector de artículo de una línea,
/// <c>ITaxTypeService</c> para el selector de tarifa de IVA, y <c>ILedgerAccountService</c> /
/// <c>IFinancialCategoryService</c> para los selectores de cuenta de efectivo y categoría que pide
/// confirmar una factura de contado (regla 10). Un Asistente solo ve y edita las facturas que él mismo
/// creó (<see cref="OwnerRestriction"/>); Finanzas y Gerencia ven y, según su política, editan o
/// resuelven cualquiera. Las políticas de <c>SystemPolicies</c> a nivel de acción son siempre un
/// subconjunto de la política de clase (<c>ViewFinancialTransactions</c>), así que combinarlas con
/// <c>[Authorize]</c> adicional en cada acción restringe correctamente sin contradicciones.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialTransactions)]
[Route("facturacion/facturas")]
public sealed class InvoicesController(
    IInvoiceService service,
    ICustomerCatalogService customerService,
    ICatalogItemService catalogItemService,
    ITaxTypeService taxTypeService,
    ILedgerAccountService ledgerAccountService,
    IFinancialCategoryService financialCategoryService,
    IAuthorizationService authorizationService,
    ISystemParametersService systemParametersService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        InvoiceStatusFilter status = InvoiceStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        InvoiceQuery query = new(NormalizeSearch(search), NormalizeStatus(status), OwnerRestriction(), page: page);
        PagedResult<InvoiceSummaryModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new InvoiceIndexViewModel(results, query.Search, query.Status));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("crear")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        InvoiceCreateViewModel model = new();
        await PopulateCustomerOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        InvoiceCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCustomerOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        InvoiceCreationResult result = await service.CreateAsync(
            new CreateInvoiceCommand(model.CustomerId!.Value, model.IssueDate, model.PaymentTerm),
            actorId,
            cancellationToken);

        if (result.Operation.IsSuccess && result.InvoiceId is Guid newId)
        {
            SetSuccessMessage("La factura fue creada como borrador. Ahora puede agregarle líneas.");
            return RedirectToAction(nameof(Details), new { id = newId });
        }

        await PopulateCustomerOptionsAsync(model, cancellationToken);
        return FormFailure(result.Operation, model, nameof(Create));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        InvoiceModel? invoice = await service.GetAsync(id, OwnerRestriction(), cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        bool isOwnDraft = invoice.CreatedByUserId == actorId;
        bool canManageDrafts = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.CreateFinancialDrafts)).Succeeded;
        bool canConfirm = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.ConfirmFinancialTransactions)).Succeeded;
        bool canCancel = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;

        bool isDraft = invoice.Status == InvoiceStatus.Draft;
        bool canEditDraft = isDraft && canManageDrafts && (!User.IsInRole(SystemRoles.Assistant) || isOwnDraft);

        return View(new InvoiceDetailsViewModel(
            invoice,
            canEditDraft,
            canConfirm && isDraft,
            canCancel && invoice.Status == InvoiceStatus.Confirmed));
    }

    /// <summary>
    /// Exporta la factura interna en PDF (regla 15, RF-37). Solo confirmadas o anuladas: un borrador
    /// todavía no tiene número ni totales definitivos (regla 10).
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, OwnerRestriction(), cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status == InvoiceStatus.Draft)
        {
            TempData["ErrorMessage"] = "Solo se exportan a PDF facturas confirmadas o anuladas.";
            return RedirectToAction(nameof(Details), new { id });
        }

        SystemParametersModel? companyData = await systemParametersService.GetAsync(cancellationToken);
        byte[] pdf = new InvoicePdfDocument(invoice, companyData).GeneratePdf();
        string fileName = invoice.Number is long number
            ? $"factura-{number}.pdf"
            : "factura.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("{id:guid}/encabezado")]
    public async Task<IActionResult> EditHeader(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, OwnerRestriction(), cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        InvoiceHeaderEditViewModel model = new()
        {
            CustomerId = invoice.CustomerId,
            IssueDate = invoice.IssueDate,
            PaymentTerm = invoice.PaymentTerm,
            Version = invoice.Version,
        };
        await PopulateCustomerOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("{id:guid}/encabezado")]
    public async Task<IActionResult> EditHeader(
        Guid id,
        InvoiceHeaderEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCustomerOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateHeaderAsync(
            id,
            new UpdateInvoiceHeaderCommand(model.CustomerId!.Value, model.IssueDate, model.PaymentTerm),
            model.Version,
            actorId,
            OwnerRestriction(),
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El encabezado de la factura fue actualizado correctamente.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            InvoiceModel? latest = await service.GetAsync(id, OwnerRestriction(), cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        await PopulateCustomerOptionsAsync(model, cancellationToken);
        return FormFailure(result, model, nameof(EditHeader));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("{id:guid}/lineas/agregar")]
    public async Task<IActionResult> AddLine(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, OwnerRestriction(), cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        InvoiceLineInputViewModel model = new() { Version = invoice.Version };
        await PopulateLineOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("{id:guid}/lineas/agregar")]
    public async Task<IActionResult> AddLine(
        Guid id,
        InvoiceLineInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLineOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.AddLineAsync(
            id,
            ToLineCommand(model),
            model.Version,
            actorId,
            OwnerRestriction(),
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La línea fue agregada a la factura.");
            return RedirectToAction(nameof(Details), new { id });
        }

        await RefreshLineVersionAsync(id, model, cancellationToken);
        await PopulateLineOptionsAsync(model, cancellationToken);
        return FormFailure(result, model, nameof(AddLine));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("{id:guid}/lineas/{lineId:guid}/editar")]
    public async Task<IActionResult> EditLine(Guid id, Guid lineId, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, OwnerRestriction(), cancellationToken);
        InvoiceLineModel? line = invoice?.Lines.SingleOrDefault(candidate => candidate.Id == lineId);

        if (invoice is null || line is null)
        {
            return NotFound();
        }

        InvoiceLineInputViewModel model = new()
        {
            CatalogItemId = line.CatalogItemId,
            Description = line.Description,
            UnitOfMeasure = line.UnitOfMeasure,
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            Version = invoice.Version,
        };
        await PopulateLineOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("{id:guid}/lineas/{lineId:guid}/editar")]
    public async Task<IActionResult> EditLine(
        Guid id,
        Guid lineId,
        InvoiceLineInputViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLineOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.ReplaceLineAsync(
            id,
            lineId,
            ToLineCommand(model),
            model.Version,
            actorId,
            OwnerRestriction(),
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La línea fue actualizada.");
            return RedirectToAction(nameof(Details), new { id });
        }

        await RefreshLineVersionAsync(id, model, cancellationToken);
        await PopulateLineOptionsAsync(model, cancellationToken);
        return FormFailure(result, model, nameof(EditLine));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("{id:guid}/lineas/{lineId:guid}/quitar")]
    public async Task<IActionResult> RemoveLine(
        Guid id,
        Guid lineId,
        [FromForm] string version,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.RemoveLineAsync(
            id,
            lineId,
            version,
            actorId,
            OwnerRestriction(),
            cancellationToken);

        if (!result.IsSuccess)
        {
            return StateFailure(result, "Invoices");
        }

        SetSuccessMessage("La línea fue quitada de la factura.");
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);

        if (invoice is null || invoice.Status != InvoiceStatus.Draft)
        {
            return NotFound();
        }

        InvoiceConfirmViewModel model = new()
        {
            Version = invoice.Version,
            IsCash = invoice.PaymentTerm == PaymentTerm.Cash,
            TotalAmount = invoice.TotalAmount,
        };

        if (model.IsCash)
        {
            await PopulateConfirmOptionsAsync(model, cancellationToken);
        }

        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirm(
        Guid id,
        InvoiceConfirmViewModel model,
        CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);

        if (invoice is null || invoice.Status != InvoiceStatus.Draft)
        {
            return NotFound();
        }

        model.IsCash = invoice.PaymentTerm == PaymentTerm.Cash;
        model.TotalAmount = invoice.TotalAmount;

        if (model.IsCash)
        {
            ValidateCashConfirmation(model);
        }

        if (!ModelState.IsValid)
        {
            if (model.IsCash)
            {
                await PopulateConfirmOptionsAsync(model, cancellationToken);
            }

            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        bool duplicateReferenceWarning = model.IsCash && model.CashAccountId is Guid cashAccountId
            && !string.IsNullOrWhiteSpace(model.Reference)
            && await service.HasDuplicateCashReferenceAsync(cashAccountId, model.Reference, cancellationToken);

        ConfirmInvoiceCommand command = new(model.CashAccountId, model.PaymentMethod, model.Reference, model.CategoryId);
        CatalogOperationResult result = await service.ConfirmAsync(id, command, model.Version, actorId, cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage(duplicateReferenceWarning
                ? "La factura fue confirmada. Advertencia: la referencia ya se había usado antes en esta cuenta de efectivo."
                : "La factura fue confirmada.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            InvoiceModel? latest = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        if (model.IsCash)
        {
            await PopulateConfirmOptionsAsync(model, cancellationToken);
        }

        return FormFailure(result, model, nameof(Confirm));
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(new InvoiceCancelViewModel { Version = invoice.Version });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        InvoiceCancelViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.CancelAsync(
            id,
            model.Reason,
            model.Version,
            actorId,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("La factura fue anulada.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            InvoiceModel? latest = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private static InvoiceLineInputCommand ToLineCommand(InvoiceLineInputViewModel model) => new(
        model.CatalogItemId,
        model.Description,
        model.UnitOfMeasure,
        model.Quantity,
        model.UnitPrice,
        model.DiscountAmount,
        model.TaxTypeId!.Value);

    private Guid? OwnerRestriction() => User.IsInRole(SystemRoles.Assistant) && TryGetActorId(out Guid actorId)
        ? actorId
        : null;

    private static InvoiceStatusFilter NormalizeStatus(InvoiceStatusFilter status) =>
        Enum.IsDefined(status) ? status : InvoiceStatusFilter.All;

    private async Task RefreshLineVersionAsync(
        Guid invoiceId,
        InvoiceLineInputViewModel model,
        CancellationToken cancellationToken)
    {
        InvoiceModel? latest = await service.GetAsync(invoiceId, OwnerRestriction(), cancellationToken);

        if (latest is not null)
        {
            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }
    }

    private async Task PopulateCustomerOptionsAsync(
        InvoiceCreateViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomerOption> options = await customerService.GetActiveOptionsAsync(cancellationToken);

        model.CustomerOptions = options
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CustomerId))
            .ToList();
    }

    private void ValidateCashConfirmation(InvoiceConfirmViewModel model)
    {
        if (model.CashAccountId is null)
        {
            ModelState.AddModelError(nameof(model.CashAccountId), "La cuenta de efectivo es obligatoria.");
        }

        if (model.PaymentMethod is null)
        {
            ModelState.AddModelError(nameof(model.PaymentMethod), "El medio de pago es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(model.Reference))
        {
            ModelState.AddModelError(nameof(model.Reference), "La referencia es obligatoria.");
        }

        if (model.CategoryId is null)
        {
            ModelState.AddModelError(nameof(model.CategoryId), "La categoría es obligatoria.");
        }
    }

    private async Task PopulateConfirmOptionsAsync(
        InvoiceConfirmViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CashAccountOption> cashAccounts = await ledgerAccountService.GetActiveCashAccountsAsync(cancellationToken);
        model.CashAccountOptions = cashAccounts
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CashAccountId))
            .ToList();

        IReadOnlyList<FinancialCategoryOption> categories = await financialCategoryService.GetActiveOptionsAsync(
            FinancialCategoryKind.Income,
            cancellationToken: cancellationToken);
        model.CategoryOptions = categories
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CategoryId))
            .ToList();
    }

    private async Task PopulateLineOptionsAsync(
        InvoiceLineInputViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<CatalogItemOption> itemOptions = await catalogItemService.GetActiveOptionsAsync(cancellationToken);
        model.CatalogItemOptions = itemOptions
            .Select(option => new InvoiceLineCatalogItemOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name}",
                option.DefaultTaxTypeId))
            .ToList();

        IReadOnlyList<TaxTypeOption> taxOptions = await taxTypeService.GetActiveOptionsAsync(cancellationToken);
        model.TaxTypeOptions = taxOptions
            .Select(option => new SelectListItem(
                $"{option.Name} ({option.Rate:0.##}%)",
                option.Id.ToString(),
                option.Id == model.TaxTypeId))
            .ToList();
    }
}
