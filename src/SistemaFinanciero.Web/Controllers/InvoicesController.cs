using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuestPDF.Fluent;
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
using SistemaFinanciero.Web.Models.Catalogs;
using SistemaFinanciero.Web.Models.Invoices;
using SistemaFinanciero.Web.Reporting;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el ciclo de vida de la factura administrativa y su confirmación.</summary>
/// <remarks>
/// Conexiones: usa <c>IInvoiceService</c> (Application) para la factura, <c>ICustomerCatalogService</c>
/// para el selector de cliente, <c>ICatalogItemService</c> para el selector de artículo de una línea,
/// <c>ITaxTypeService</c> para el selector de tarifa de IVA, y <c>ILedgerAccountService</c> para los
/// selectores de cuenta de efectivo y cuenta de ingreso que pide
/// confirmar una factura de contado (regla 10). Un Asistente consulta facturas de cualquier usuario,
/// pero solo edita sus propios borradores (<see cref="OwnerRestriction"/>); Finanzas y Gerencia ven
/// y, según su política, editan o resuelven cualquiera. Las políticas de <c>SystemPolicies</c> a
/// nivel de acción son siempre un subconjunto de la política de clase
/// (<c>ViewFinancialTransactions</c>), así que combinarlas con <c>[Authorize]</c> adicional en cada
/// acción restringe correctamente sin contradicciones.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialTransactions)]
[Route("facturacion/facturas")]
public sealed class InvoicesController(
    IInvoiceService service,
    ICustomerCatalogService customerService,
    ICatalogItemService catalogItemService,
    ITaxTypeService taxTypeService,
    ILedgerAccountService ledgerAccountService,
    IAuthorizationService authorizationService,
    ISystemParametersService systemParametersService,
    TimeProvider timeProvider) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        long? number,
        string? customerName,
        DateOnly? issueDate,
        PaymentTerm? paymentTerm,
        DateOnly? dueDate,
        InvoiceStatusFilter status = InvoiceStatusFilter.All,
        decimal? totalAmount = null,
        string? createdByUserName = null,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        InvoiceQuery query = new(
            status: NormalizeStatus(status),
            createdByUserId: null,
            paymentTerm: paymentTerm,
            page: page,
            number: number,
            customerName: customerName,
            issueDate: issueDate,
            dueDate: dueDate,
            totalAmount: totalAmount,
            createdByUserName: createdByUserName);
        PagedResult<InvoiceSummaryModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new InvoiceIndexViewModel(
            results,
            query.Number,
            query.CustomerName,
            query.IssueDate,
            query.PaymentTerm,
            query.DueDate,
            query.Status,
            query.TotalAmount,
            query.CreatedByUserName));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("crear")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        InvoiceCreateViewModel model = new() { IssueDate = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime) };
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
            new CreateInvoiceCommand(model.CustomerId!.Value, model.IncomeAccountId!.Value, model.IssueDate, model.PaymentTerm),
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

        InvoiceModel? invoice = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);

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
        bool isDraft = invoice.Status == InvoiceStatus.Draft;
        bool canEditDraft = isDraft && canManageDrafts && (!User.IsInRole(SystemRoles.Assistant) || isOwnDraft);

        return View(new InvoiceDetailsViewModel(
            invoice,
            canEditDraft,
            canConfirm && isDraft,
            canConfirm && invoice.Status == InvoiceStatus.Confirmed));
    }

    /// <summary>
    /// Exporta la factura interna en PDF (regla 15, RF-37). Solo confirmadas o anuladas: un borrador
    /// todavía no tiene número ni totales definitivos (regla 10).
    /// </summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);

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

    /// <summary>
    /// Exporta los datos de una factura confirmada en JSON, con los códigos del formato 4.4 de
    /// Hacienda (bloque 3 del paquete de mejoras finales, RF-88); no emite, firma ni transmite nada.
    /// </summary>
    [HttpGet("{id:guid}/exportar-datos")]
    public async Task<IActionResult> ExportData(Guid id, CancellationToken cancellationToken)
    {
        InvoiceModel? invoice = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken);

        if (invoice is null)
        {
            return NotFound();
        }

        if (invoice.Status != InvoiceStatus.Confirmed)
        {
            TempData["ErrorMessage"] = "Solo se exportan datos de facturas confirmadas.";
            return RedirectToAction(nameof(Details), new { id });
        }

        SystemParametersModel? companyData = await systemParametersService.GetAsync(cancellationToken);
        string json = InvoiceElectronicDataExport.BuildJson(invoice, companyData);
        string fileName = invoice.Number is long number
            ? $"factura-{number}-datos.json"
            : "factura-datos.json";
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    /// <summary>
    /// Exporta a Excel los datos de las facturas confirmadas de un período, con los mismos datos por
    /// línea que <see cref="ExportData"/> (bloque 3, RF-88).
    /// </summary>
    [HttpGet("exportar-datos")]
    public async Task<IActionResult> ExportDataRange(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out _))
        {
            return Forbid();
        }

        InvoiceQuery query = new(
            status: InvoiceStatusFilter.Confirmed,
            issueDateFrom: from,
            issueDateTo: to,
            pageSize: 100);
        PagedResult<InvoiceSummaryModel> summaries = await service.SearchAsync(query, cancellationToken);

        if (summaries.Items.Count == 0)
        {
            TempData["ErrorMessage"] = "No hay facturas confirmadas en el período indicado.";
            return RedirectToAction(nameof(Index));
        }

        List<InvoiceModel> invoices = [];
        foreach (InvoiceSummaryModel summary in summaries.Items)
        {
            InvoiceModel? invoice = await service.GetAsync(summary.Id, restrictToOwnerUserId: null, cancellationToken);
            if (invoice is not null)
            {
                invoices.Add(invoice);
            }
        }

        byte[] workbook = InvoiceElectronicDataExport.BuildWorkbook(invoices, User.Identity?.Name ?? "—");
        return File(
            workbook,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"facturas-datos-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
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
            IncomeAccountId = await ProposeIncomeAccountIdAsync(invoice, cancellationToken),
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
            new UpdateInvoiceHeaderCommand(model.CustomerId!.Value, model.IncomeAccountId!.Value, model.IssueDate, model.PaymentTerm),
            model.Version,
            actorId,
            OwnerRestriction(),
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage(result.Message is null
                ? "El encabezado de la factura fue actualizado correctamente."
                : $"El encabezado de la factura fue actualizado correctamente. {result.Message}");
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
        await PopulateLineOptionsAsync(model, invoice.CustomerId, cancellationToken);
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
            InvoiceModel? invoiceForOptions = await service.GetAsync(id, OwnerRestriction(), cancellationToken);
            await PopulateLineOptionsAsync(model, invoiceForOptions?.CustomerId ?? Guid.Empty, cancellationToken);
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

        Guid customerIdAfterFailure = await RefreshLineVersionAsync(id, model, cancellationToken);
        await PopulateLineOptionsAsync(model, customerIdAfterFailure, cancellationToken);
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
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            DiscountAmount = line.DiscountAmount,
            Version = invoice.Version,
        };
        await PopulateLineOptionsAsync(model, invoice.CustomerId, cancellationToken);
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
            InvoiceModel? invoiceForOptions = await service.GetAsync(id, OwnerRestriction(), cancellationToken);
            await PopulateLineOptionsAsync(model, invoiceForOptions?.CustomerId ?? Guid.Empty, cancellationToken);
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

        Guid customerIdAfterFailure = await RefreshLineVersionAsync(id, model, cancellationToken);
        await PopulateLineOptionsAsync(model, customerIdAfterFailure, cancellationToken);
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
        else
        {
            await PopulateCreditLimitCheckAsync(model, invoice, cancellationToken);
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
            else
            {
                await PopulateCreditLimitCheckAsync(model, invoice, cancellationToken);
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

        ConfirmInvoiceCommand command = new(model.CashAccountId, model.PaymentMethod, model.Reference);
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
        else
        {
            await PopulateCreditLimitCheckAsync(model, invoice, cancellationToken);
        }

        return FormFailure(result, model, nameof(Confirm));
    }

    private static InvoiceLineInputCommand ToLineCommand(InvoiceLineInputViewModel model) => new(
        model.CatalogItemId!.Value,
        model.Quantity,
        model.UnitPrice,
        model.DiscountAmount,
        model.TaxTypeId);

    private Guid? OwnerRestriction() => User.IsInRole(SystemRoles.Assistant) && TryGetActorId(out Guid actorId)
        ? actorId
        : null;

    private static InvoiceStatusFilter NormalizeStatus(InvoiceStatusFilter status) =>
        Enum.IsDefined(status) ? status : InvoiceStatusFilter.All;

    private async Task<Guid> RefreshLineVersionAsync(
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

        return latest?.CustomerId ?? Guid.Empty;
    }

    /// <summary>
    /// Propone la categoría de ingreso al editar el encabezado: si la factura tiene una sola línea
    /// (la primera) tomada de un producto o servicio con <c>DefaultIncomeAccountId</c>, la propone en
    /// vez del valor ya guardado (bloque 2 del paquete de mejoras finales); la persona puede
    /// cambiarla antes de guardar.
    /// </summary>
    private async Task<Guid> ProposeIncomeAccountIdAsync(InvoiceModel invoice, CancellationToken cancellationToken)
    {
        if (invoice.Lines.Count == 1 && invoice.Lines[0].CatalogItemId is Guid catalogItemId)
        {
            CatalogItemModel? catalogItem = await catalogItemService.GetAsync(catalogItemId, cancellationToken);
            if (catalogItem?.DefaultIncomeAccountId is Guid defaultAccountId)
            {
                return defaultAccountId;
            }
        }

        return invoice.IncomeAccountId;
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

        IReadOnlyList<LedgerAccountOption> categories = await ledgerAccountService.GetActiveOptionsAsync(
            LedgerAccountType.Income,
            cancellationToken: cancellationToken);
        model.IncomeCategoryOptions = categories
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.IncomeAccountId))
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
    }

    private async Task PopulateCreditLimitCheckAsync(
        InvoiceConfirmViewModel model,
        InvoiceModel invoice,
        CancellationToken cancellationToken)
    {
        CustomerCreditLimitCheck check = await service.CheckCustomerCreditLimitAsync(
            invoice.CustomerId,
            invoice.TotalAmount,
            cancellationToken);

        model.HasCreditLimit = check.HasLimit;
        model.CreditLimit = check.CreditLimit;
        model.OutstandingBalanceBeforeThisInvoice = check.OutstandingBalanceBeforeThisInvoice;
        model.ExceedsCreditLimit = check.ExceedsLimit;
    }

    private async Task PopulateLineOptionsAsync(
        InvoiceLineInputViewModel model,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        CustomerModel? customer = await customerService.GetAsync(customerId, cancellationToken);
        VatCondition vatCondition = customer?.VatCondition ?? VatCondition.Normal;

        IReadOnlyList<TaxTypeOption> taxOptions = await taxTypeService.GetActiveOptionsAsync(cancellationToken);
        Dictionary<Guid, decimal> taxRatesById = taxOptions.ToDictionary(option => option.Id, option => option.Rate);

        IReadOnlyList<CatalogItemOption> itemOptions = await catalogItemService.GetActiveOptionsAsync(cancellationToken);
        model.CatalogItemOptions = itemOptions
            .Select(option => new InvoiceLineCatalogItemOptionViewModel(
                option.Id,
                $"{option.Code} - {option.Name} ({option.UnitOfMeasure.ToDisplayName()})",
                option.Name,
                option.UnitOfMeasure.ToDisplayName(),
                option.ReferencePriceCrc,
                option.DefaultTaxTypeId,
                option.DefaultTaxTypeId is Guid defaultTaxTypeId && taxRatesById.TryGetValue(defaultTaxTypeId, out decimal defaultRate)
                    ? defaultRate
                    : null))
            .ToList();

        // RF-93 (bloque 1 del paquete v7): la factura nunca ofrece una tarifa que no corresponda a la
        // condición de IVA del cliente. En Normal se excluyen las reservadas para otra condición; en
        // las otras dos condiciones solo se ofrece la tarifa que esa condición fuerza.
        IEnumerable<TaxTypeOption> allowedTaxOptions = vatCondition switch
        {
            VatCondition.StateHigherEducationInstitution =>
                taxOptions.Where(option => option.SystemRole == TaxTypeSystemRole.Reduced2),
            VatCondition.NotSubject =>
                taxOptions.Where(option => option.SystemRole == TaxTypeSystemRole.NotSubject),
            _ => taxOptions.Where(option =>
                option.SystemRole is not (TaxTypeSystemRole.Reduced2 or TaxTypeSystemRole.NotSubject)),
        };

        model.TaxTypeOptions = allowedTaxOptions
            .Select(option => new InvoiceLineTaxTypeOptionViewModel(
                option.Id,
                $"{option.Name} ({option.Rate:0.##}%)",
                option.Rate))
            .ToList();
    }
}
