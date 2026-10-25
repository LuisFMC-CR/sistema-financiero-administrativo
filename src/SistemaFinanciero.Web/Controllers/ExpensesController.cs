using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Application.Catalogs.Suppliers;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Expenses;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Models.Expenses;

namespace SistemaFinanciero.Web.Controllers;

/// <summary>Presenta el ciclo de vida del gasto: borrador, confirmación y anulación.</summary>
/// <remarks>
/// Conexiones: usa <c>IExpenseService</c> (Application) para el gasto, <c>ISupplierCatalogService</c>
/// para el selector de proveedor, <c>IFinancialCategoryService</c> para el selector de categoría de
/// gasto, <c>ITaxTypeService</c> para el selector de tarifa de IVA y <c>ILedgerAccountService</c> para
/// el selector de cuenta de efectivo que pide confirmar un gasto de contado (regla 7). Un Asistente
/// solo ve y edita los gastos que él mismo creó (<see cref="OwnerRestriction"/>, igual que con
/// facturas); Finanzas y Gerencia ven y, según su política, editan o resuelven cualquiera. Es el
/// espejo, del lado de gastos, de <c>InvoicesController</c>; a diferencia de este, no tiene líneas.
/// </remarks>
[Authorize(Policy = SystemPolicies.ViewFinancialTransactions)]
[Route("gastos")]
public sealed class ExpensesController(
    IExpenseService service,
    ISupplierCatalogService supplierService,
    IFinancialCategoryService financialCategoryService,
    ITaxTypeService taxTypeService,
    ILedgerAccountService ledgerAccountService,
    IAuthorizationService authorizationService) : CatalogControllerBase
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? code,
        string? supplierName,
        string? categoryName,
        DateOnly? issueDateFrom,
        DateOnly? issueDateTo,
        decimal? amountMin,
        decimal? amountMax,
        ExpenseStatusFilter status = ExpenseStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        ExpenseQuery query = new(
            NormalizeSearch(code),
            NormalizeSearch(supplierName),
            NormalizeSearch(categoryName),
            issueDateFrom,
            issueDateTo,
            amountMin,
            amountMax,
            NormalizeStatus(status),
            OwnerRestriction(),
            page);
        PagedResult<ExpenseModel> results = await service.SearchAsync(query, cancellationToken);

        return View(new ExpenseIndexViewModel(results, query));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("crear")]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ExpenseCreateViewModel model = new();
        await PopulateHeaderOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("crear")]
    public async Task<IActionResult> Create(
        ExpenseCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateHeaderOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        ExpenseCreationResult result = await service.CreateAsync(ToCommand(model), actorId, cancellationToken);

        if (result.Operation.IsSuccess && result.ExpenseId is Guid newId)
        {
            SetSuccessMessage("El gasto fue creado como borrador.");
            return RedirectToAction(nameof(Details), new { id = newId });
        }

        await PopulateHeaderOptionsAsync(model, cancellationToken);
        return FormFailure(result.Operation, model, nameof(Create));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        ExpenseModel? expense = await service.GetAsync(id, OwnerRestriction(), cancellationToken);

        if (expense is null)
        {
            return NotFound();
        }

        bool isOwnDraft = expense.CreatedByUserId == actorId;
        bool canManageDrafts = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.CreateFinancialDrafts)).Succeeded;
        bool canConfirm = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.ConfirmFinancialTransactions)).Succeeded;
        bool canCancel = (await authorizationService.AuthorizeAsync(
            User,
            SystemPolicies.VoidFinancialTransactions)).Succeeded;

        bool isDraft = expense.Status == ExpenseStatus.Draft;
        bool canEditDraft = isDraft && canManageDrafts && (!User.IsInRole(SystemRoles.Assistant) || isOwnDraft);

        return View(new ExpenseDetailsViewModel(
            expense,
            canEditDraft,
            canConfirm && isDraft,
            canCancel && expense.Status == ExpenseStatus.Confirmed));
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpGet("{id:guid}/encabezado")]
    public async Task<IActionResult> EditHeader(Guid id, CancellationToken cancellationToken)
    {
        ExpenseModel? expense = await service.GetAsync(id, OwnerRestriction(), cancellationToken);

        if (expense is null)
        {
            return NotFound();
        }

        ExpenseHeaderEditViewModel model = new()
        {
            SupplierId = expense.SupplierId,
            CategoryId = expense.CategoryId,
            Description = expense.Description,
            IssueDate = expense.IssueDate,
            PaymentTerm = expense.PaymentTerm,
            NetAmount = expense.NetAmount,
            TaxTypeId = expense.TaxTypeId,
            PaymentMethod = expense.PaymentMethod,
            Version = expense.Version,
        };
        await PopulateHeaderOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [Authorize(Policy = SystemPolicies.CreateFinancialDrafts)]
    [HttpPost("{id:guid}/encabezado")]
    public async Task<IActionResult> EditHeader(
        Guid id,
        ExpenseHeaderEditViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await PopulateHeaderOptionsAsync(model, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        CatalogOperationResult result = await service.UpdateHeaderAsync(
            id,
            ToCommand(model),
            model.Version,
            actorId,
            OwnerRestriction(),
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El encabezado del gasto fue actualizado correctamente.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            ExpenseModel? latest = await service.GetAsync(id, OwnerRestriction(), cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        await PopulateHeaderOptionsAsync(model, cancellationToken);
        return FormFailure(result, model, nameof(EditHeader));
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpGet("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        ExpenseModel? expense = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken: cancellationToken);

        if (expense is null || expense.Status != ExpenseStatus.Draft)
        {
            return NotFound();
        }

        ExpenseConfirmViewModel model = new()
        {
            Version = expense.Version,
            IsCash = expense.PaymentTerm == PaymentTerm.Cash,
            TotalAmount = expense.TotalAmount,
        };

        if (model.IsCash)
        {
            await PopulateConfirmOptionsAsync(model, cancellationToken);
        }

        await PopulateBudgetCheckAsync(model, expense, cancellationToken);

        return View(model);
    }

    [Authorize(Policy = SystemPolicies.ConfirmFinancialTransactions)]
    [HttpPost("{id:guid}/confirmar")]
    public async Task<IActionResult> Confirm(
        Guid id,
        ExpenseConfirmViewModel model,
        CancellationToken cancellationToken)
    {
        ExpenseModel? expense = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken: cancellationToken);

        if (expense is null || expense.Status != ExpenseStatus.Draft)
        {
            return NotFound();
        }

        model.IsCash = expense.PaymentTerm == PaymentTerm.Cash;
        model.TotalAmount = expense.TotalAmount;

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

            await PopulateBudgetCheckAsync(model, expense, cancellationToken);
            return View(model);
        }

        if (!TryGetActorId(out Guid actorId))
        {
            return Forbid();
        }

        bool actorIsManagement = User.IsInRole(SystemRoles.Management);
        ConfirmExpenseCommand command = new(model.CashAccountId, model.Reference);
        CatalogOperationResult result = await service.ConfirmAsync(
            id,
            command,
            model.Version,
            actorId,
            actorIsManagement,
            cancellationToken);

        if (result.IsSuccess)
        {
            SetSuccessMessage("El gasto fue confirmado.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            ExpenseModel? latest = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken: cancellationToken);
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

        await PopulateBudgetCheckAsync(model, expense, cancellationToken);
        return FormFailure(result, model, nameof(Confirm));
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpGet("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        ExpenseModel? expense = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken: cancellationToken);

        if (expense is null)
        {
            return NotFound();
        }

        return View(new ExpenseCancelViewModel { Version = expense.Version });
    }

    [Authorize(Policy = SystemPolicies.VoidFinancialTransactions)]
    [HttpPost("{id:guid}/anular")]
    public async Task<IActionResult> Cancel(
        Guid id,
        ExpenseCancelViewModel model,
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
            SetSuccessMessage("El gasto fue anulado.");
            return RedirectToAction(nameof(Details), new { id });
        }

        if (result.Status == CatalogOperationStatus.ConcurrencyConflict)
        {
            ExpenseModel? latest = await service.GetAsync(id, restrictToOwnerUserId: null, cancellationToken: cancellationToken);
            if (latest is null)
            {
                return NotFound();
            }

            model.Version = latest.Version;
            ModelState.Remove(nameof(model.Version));
        }

        return FormFailure(result, model, nameof(Cancel));
    }

    private static SaveExpenseCommand ToCommand(ExpenseCreateViewModel model) => new(
        model.SupplierId!.Value,
        model.CategoryId!.Value,
        model.Description,
        model.IssueDate,
        model.PaymentTerm,
        model.NetAmount,
        model.TaxTypeId!.Value,
        model.PaymentMethod);

    private Guid? OwnerRestriction() => User.IsInRole(SystemRoles.Assistant) && TryGetActorId(out Guid actorId)
        ? actorId
        : null;

    private static ExpenseStatusFilter NormalizeStatus(ExpenseStatusFilter status) =>
        Enum.IsDefined(status) ? status : ExpenseStatusFilter.All;

    private void ValidateCashConfirmation(ExpenseConfirmViewModel model)
    {
        if (model.CashAccountId is null)
        {
            ModelState.AddModelError(nameof(model.CashAccountId), "La cuenta de efectivo es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(model.Reference))
        {
            ModelState.AddModelError(nameof(model.Reference), "La referencia es obligatoria.");
        }
    }

    private async Task PopulateBudgetCheckAsync(
        ExpenseConfirmViewModel model,
        ExpenseModel expense,
        CancellationToken cancellationToken)
    {
        ExpenseBudgetCheck check = await service.CheckBudgetAsync(
            expense.CategoryId,
            expense.IssueDate,
            expense.TotalAmount,
            cancellationToken);

        model.HasBudget = check.HasBudget;
        model.BudgetedAmount = check.BudgetedAmount;
        model.AvailableAmountBeforeThisExpense = check.AvailableAmountBeforeThisExpense;
        model.ExceedsAvailableBudget = check.ExceedsAvailable;
    }

    private async Task PopulateConfirmOptionsAsync(
        ExpenseConfirmViewModel model,
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

    private async Task PopulateHeaderOptionsAsync(
        ExpenseCreateViewModel model,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SupplierOption> suppliers = await supplierService.GetActiveOptionsAsync(cancellationToken);
        model.SupplierOptions = suppliers
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.SupplierId))
            .ToList();

        IReadOnlyList<FinancialCategoryOption> categories = await financialCategoryService.GetActiveOptionsAsync(
            FinancialCategoryKind.Expense,
            cancellationToken: cancellationToken);
        model.CategoryOptions = categories
            .Select(option => new SelectListItem(
                $"{option.Code} - {option.Name}",
                option.Id.ToString(),
                option.Id == model.CategoryId))
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
