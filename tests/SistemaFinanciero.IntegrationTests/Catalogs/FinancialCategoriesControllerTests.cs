using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Categories;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class FinancialCategoriesControllerTests
{
    private static readonly Guid IncomeAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ExpenseAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Create_SendsTheSelectedLedgerAccountToTheService()
    {
        FakeCategoryService categories = new();
        FinancialCategoriesController controller = CreateController(categories, new FakeLedgerAccountService());
        FinancialCategoryInputViewModel model = new()
        {
            Code = "ING",
            Name = "Ingresos",
            Kind = FinancialCategoryKind.Income,
            LedgerAccountId = IncomeAccountId,
        };

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(IncomeAccountId, categories.LastCommand!.LedgerAccountId);
    }

    [Fact]
    public async Task Create_ShowsTheActiveAccountsOfBothNaturesWhenTheFormIsShownAgain()
    {
        FakeCategoryService categories = new();
        FakeLedgerAccountService ledger = new();
        FinancialCategoriesController controller = CreateController(categories, ledger);
        controller.ModelState.AddModelError(nameof(FinancialCategoryInputViewModel.Code), "El código es obligatorio.");
        FinancialCategoryInputViewModel model = new();

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, categories.CreateCalls);
        Assert.Contains(
            model.LedgerAccountOptions,
            option => option.Id == IncomeAccountId && option.Kind == FinancialCategoryKind.Income
                && option.Label == "4 - Ingresos");
        Assert.Contains(
            model.LedgerAccountOptions,
            option => option.Id == ExpenseAccountId && option.Kind == FinancialCategoryKind.Expense
                && option.Label == "5 - Gastos");
    }

    [Fact]
    public async Task Create_WhenTheServiceRejectsTheAccount_ShowsTheFormAgainWithTheMessage()
    {
        FakeCategoryService categories = new()
        {
            OperationResult = CatalogOperationResult.Invalid(
                "Una categoría de ingreso requiere una cuenta contable de tipo Ingreso."),
        };
        FinancialCategoriesController controller = CreateController(categories, new FakeLedgerAccountService());

        IActionResult result = await controller.Create(
            new FinancialCategoryInputViewModel
            {
                Code = "ING",
                Name = "Ingresos",
                Kind = FinancialCategoryKind.Income,
                LedgerAccountId = ExpenseAccountId,
            },
            CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(FinancialCategoriesController.Create), view.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_ShowsTheCurrentAccountEvenWhenItIsNoLongerActive()
    {
        Guid id = Guid.NewGuid();
        Guid inactiveAccountId = Guid.NewGuid();
        FakeCategoryService categories = new()
        {
            Current = CreateModel(id, inactiveAccountId, "OLD", "Cuenta inactiva"),
        };
        FinancialCategoriesController controller = CreateController(categories, new FakeLedgerAccountService());

        IActionResult result = await controller.Edit(id, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        FinancialCategoryEditViewModel model = Assert.IsType<FinancialCategoryEditViewModel>(view.Model);
        Assert.Equal(inactiveAccountId, model.LedgerAccountId);
        Assert.Contains(
            model.LedgerAccountOptions,
            option => option.Id == inactiveAccountId && option.Label == "OLD - Cuenta inactiva");
    }

    [Fact]
    public async Task Edit_SendsTheChosenAccountAndReplacesTheVersionOnAConflict()
    {
        Guid id = Guid.NewGuid();
        FakeCategoryService categories = new()
        {
            Current = CreateModel(id, IncomeAccountId, "4", "Ingresos", version: "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        FinancialCategoriesController controller = CreateController(categories, new FakeLedgerAccountService());
        FinancialCategoryEditViewModel model = new()
        {
            Code = "ING",
            Name = "Ingresos",
            Kind = FinancialCategoryKind.Income,
            LedgerAccountId = IncomeAccountId,
            Version = "version-vieja",
        };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(IncomeAccountId, categories.LastCommand!.LedgerAccountId);
        Assert.Equal("version-vigente", model.Version);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
        Assert.Contains(model.LedgerAccountOptions, option => option.Id == IncomeAccountId);
    }

    private static FinancialCategoryModel CreateModel(
        Guid id,
        Guid ledgerAccountId,
        string ledgerAccountCode,
        string ledgerAccountName,
        string version = "v1") =>
        new(
            id,
            "ING",
            "Ingresos",
            FinancialCategoryKind.Income,
            ParentId: null,
            ParentName: null,
            ledgerAccountId,
            ledgerAccountCode,
            ledgerAccountName,
            Description: null,
            IsActive: true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            version);

    private static FinancialCategoriesController CreateController(
        FakeCategoryService categories,
        FakeLedgerAccountService ledger)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Test")),
        };

        return new FinancialCategoriesController(categories, ledger)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider()),
        };
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class FakeCategoryService : IFinancialCategoryService
    {
        public int CreateCalls { get; private set; }

        public SaveFinancialCategoryCommand? LastCommand { get; private set; }

        public FinancialCategoryModel? Current { get; init; }

        public CatalogOperationResult OperationResult { get; init; } = CatalogOperationResult.Succeeded();

        public Task<PagedResult<FinancialCategoryModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<FinancialCategoryModel>([], query.Page, query.PageSize, 0));

        public Task<FinancialCategoryModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<IReadOnlyList<FinancialCategoryOption>> GetActiveOptionsAsync(
            FinancialCategoryKind kind,
            Guid? excludedCategoryId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinancialCategoryOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            SaveFinancialCategoryCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCommand = command;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            SaveFinancialCategoryCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> SetActiveAsync(
            Guid id,
            bool isActive,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(OperationResult);
    }

    private sealed class FakeLedgerAccountService : ILedgerAccountService
    {
        public Task<PagedResult<LedgerAccountModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromException<PagedResult<LedgerAccountModel>>(new NotSupportedException());

        public Task<LedgerAccountModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromException<LedgerAccountModel?>(new NotSupportedException());

        public Task<IReadOnlyList<LedgerAccountOption>> GetParentOptionsAsync(
            LedgerAccountType type,
            Guid? excludedAccountId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LedgerAccountOption>>([]);

        public Task<IReadOnlyList<LedgerAccountOption>> GetActiveOptionsAsync(
            LedgerAccountType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LedgerAccountOption>>(type switch
            {
                LedgerAccountType.Income => [new LedgerAccountOption(IncomeAccountId, "4", "Ingresos")],
                LedgerAccountType.Expense => [new LedgerAccountOption(ExpenseAccountId, "5", "Gastos")],
                _ => [],
            });

        public Task<IReadOnlyList<CashAccountOption>> GetActiveCashAccountsAsync(
            CurrencyCode? currency = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CashAccountOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            CreateLedgerAccountCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            UpdateLedgerAccountCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> SetActiveAsync(
            Guid id,
            bool isActive,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());
    }
}
