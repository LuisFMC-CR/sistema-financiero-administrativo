using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class LedgerAccountsControllerTests
{
    [Fact]
    public async Task Create_ForACashAccount_SendsTheKindAndTheCurrency()
    {
        RecordingLedgerAccountService service = new();
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountCreateViewModel model = new()
        {
            Code = "BCO-USD",
            Name = "Banco dólares",
            Type = LedgerAccountType.Asset,
            IsCash = true,
            CashKind = CashAccountKind.Bank,
            Currency = CurrencyCode.USD,
        };

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(CashAccountKind.Bank, service.LastCreate!.CashKind);
        Assert.Equal(CurrencyCode.USD, service.LastCreate.Currency);
    }

    [Fact]
    public async Task Create_ForAnAccountThatIsNotCash_DiscardsThePostedKindAndCurrency()
    {
        RecordingLedgerAccountService service = new();
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountCreateViewModel model = new()
        {
            Code = "ING",
            Name = "Ingresos",
            Type = LedgerAccountType.Income,
            IsCash = false,

            // Valores que un formulario manipulado o un campo oculto podrían enviar.
            CashKind = CashAccountKind.Cash,
            Currency = CurrencyCode.CRC,
        };

        await controller.Create(model, CancellationToken.None);

        Assert.Null(service.LastCreate!.CashKind);
        Assert.Null(service.LastCreate.Currency);
    }

    [Fact]
    public async Task Create_WithInvalidData_ShowsTheFormAgainWithParentOptionsOfEveryType()
    {
        RecordingLedgerAccountService service = new();
        service.ParentOptionsByType[LedgerAccountType.Asset] = [new LedgerAccountOption(Guid.NewGuid(), "1", "Activos")];
        service.ParentOptionsByType[LedgerAccountType.Income] = [new LedgerAccountOption(Guid.NewGuid(), "4", "Ingresos")];
        LedgerAccountsController controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(LedgerAccountCreateViewModel.Code), "El código es obligatorio.");
        LedgerAccountCreateViewModel model = new();

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, service.CreateCalls);
        Assert.Equal(2, model.ParentOptions.Count);
        Assert.Contains(model.ParentOptions, option => option.Type == LedgerAccountType.Asset && option.Label == "1 - Activos");
        Assert.Contains(model.ParentOptions, option => option.Type == LedgerAccountType.Income && option.Label == "4 - Ingresos");
    }

    [Fact]
    public async Task Create_WhenTheServiceRejectsIt_ShowsTheFormAgainWithTheMessage()
    {
        RecordingLedgerAccountService service = new()
        {
            OperationResult = CatalogOperationResult.Duplicated("Ya existe una cuenta contable con el mismo código."),
        };
        LedgerAccountsController controller = CreateController(service);

        IActionResult result = await controller.Create(
            new LedgerAccountCreateViewModel { Code = "A", Name = "A" },
            CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(LedgerAccountsController.Create), view.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_NeverTakesTypeKindOrCurrencyFromTheForm()
    {
        Guid id = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Asset, CashAccountKind.Bank, CurrencyCode.USD),
        };
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountEditViewModel model = new()
        {
            Code = "BCO",
            Name = "Banco",
            Version = "v1",

            // Un formulario manipulado intenta presentar la cuenta como un gasto en colones.
            Type = LedgerAccountType.Expense,
            CashKind = null,
            Currency = CurrencyCode.CRC,
        };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(LedgerAccountType.Asset, model.Type);
        Assert.Equal(CashAccountKind.Bank, model.CashKind);
        Assert.Equal(CurrencyCode.USD, model.Currency);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal("v1", service.LastUpdateVersion);
    }

    [Fact]
    public async Task Edit_WhenTheModelIsInvalid_ShowsParentOptionsOfTheFixedTypeOnly()
    {
        Guid id = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Expense, null, null),
        };
        service.ParentOptionsByType[LedgerAccountType.Expense] = [new LedgerAccountOption(Guid.NewGuid(), "5", "Gastos")];
        service.ParentOptionsByType[LedgerAccountType.Asset] = [new LedgerAccountOption(Guid.NewGuid(), "1", "Activos")];
        LedgerAccountsController controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(LedgerAccountEditViewModel.Name), "El nombre es obligatorio.");
        LedgerAccountEditViewModel model = new() { Code = "GAS", Name = string.Empty, Version = "v1" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, service.UpdateCalls);
        LedgerAccountParentOptionViewModel option = Assert.Single(model.ParentOptions);
        Assert.Equal(LedgerAccountType.Expense, option.Type);
        Assert.Equal(id, service.LastExcludedAccountId);
    }

    [Fact]
    public async Task Edit_KeepsTheCurrentParentVisibleEvenWhenItIsNoLongerOffered()
    {
        Guid id = Guid.NewGuid();
        Guid inactiveParentId = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Asset, null, null) with
            {
                ParentId = inactiveParentId,
                ParentName = "Cuenta agrupadora inactiva",
            },
        };
        LedgerAccountsController controller = CreateController(service);

        IActionResult result = await controller.Edit(id, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        LedgerAccountEditViewModel model = Assert.IsType<LedgerAccountEditViewModel>(view.Model);
        LedgerAccountParentOptionViewModel option = Assert.Single(model.ParentOptions);
        Assert.Equal(inactiveParentId, option.Id);
        Assert.Equal("Cuenta agrupadora inactiva", option.Label);
    }

    [Fact]
    public async Task Edit_OnAConcurrencyConflict_ReplacesTheVersionAndReturns409()
    {
        Guid id = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Asset, null, null, version: "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountEditViewModel model = new() { Code = "A", Name = "A", Version = "version-vieja" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("version-vigente", model.Version);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Edit_WhenTheAccountDoesNotExist_ReturnsNotFound()
    {
        RecordingLedgerAccountService service = new();
        LedgerAccountsController controller = CreateController(service);

        IActionResult result = await controller.Edit(
            Guid.NewGuid(),
            new LedgerAccountEditViewModel { Code = "X", Name = "X", Version = "v1" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, service.UpdateCalls);
    }

    [Fact]
    public async Task SetActive_WhenTheServiceBlocksIt_RedirectsWithTheDependencyMessage()
    {
        RecordingLedgerAccountService service = new()
        {
            OperationResult = CatalogOperationResult.Dependency("Desactive o reasigne primero las cuentas hijas activas."),
        };
        LedgerAccountsController controller = CreateController(service);

        IActionResult result = await controller.SetActive(
            Guid.NewGuid(),
            new CatalogStatusViewModel { IsActive = false, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(
            "Desactive o reasigne primero las cuentas hijas activas.",
            controller.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Index_NormalizesUntrustedFiltersBeforeCallingTheService()
    {
        RecordingLedgerAccountService service = new();
        LedgerAccountsController controller = CreateController(service);

        IActionResult action = await controller.Index(
            new string('x', 150),
            (CatalogStatusFilter)999,
            page: 1,
            CancellationToken.None);

        Assert.IsType<ViewResult>(action);
        Assert.Equal(new string('x', 100), service.LastQuery!.Search);
        Assert.Equal(CatalogStatusFilter.Active, service.LastQuery.Status);
    }

    private static LedgerAccountModel CreateModel(
        Guid id,
        LedgerAccountType type,
        CashAccountKind? cashKind,
        CurrencyCode? currency,
        string version = "v1") =>
        new(
            id,
            "COD",
            "Cuenta",
            type,
            ParentId: null,
            ParentName: null,
            cashKind,
            currency,
            Reference: null,
            Description: null,
            IsActive: true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            version);

    private static LedgerAccountsController CreateController(RecordingLedgerAccountService service)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Test")),
        };

        return new LedgerAccountsController(service)
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

    private sealed class RecordingLedgerAccountService : ILedgerAccountService
    {
        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public CreateLedgerAccountCommand? LastCreate { get; private set; }

        public string? LastUpdateVersion { get; private set; }

        public Guid? LastExcludedAccountId { get; private set; }

        public CatalogQuery? LastQuery { get; private set; }

        public LedgerAccountModel? Current { get; init; }

        public CatalogOperationResult OperationResult { get; init; } = CatalogOperationResult.Succeeded();

        public Dictionary<LedgerAccountType, IReadOnlyList<LedgerAccountOption>> ParentOptionsByType { get; } = [];

        public Task<PagedResult<LedgerAccountModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<LedgerAccountModel>([], query.Page, query.PageSize, 0));
        }

        public Task<LedgerAccountModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<IReadOnlyList<LedgerAccountOption>> GetParentOptionsAsync(
            LedgerAccountType type,
            Guid? excludedAccountId = null,
            CancellationToken cancellationToken = default)
        {
            LastExcludedAccountId = excludedAccountId;
            return Task.FromResult(
                ParentOptionsByType.TryGetValue(type, out IReadOnlyList<LedgerAccountOption>? options)
                    ? options
                    : []);
        }

        public Task<IReadOnlyList<LedgerAccountOption>> GetActiveOptionsAsync(
            LedgerAccountType type,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LedgerAccountOption>>([]);

        public Task<IReadOnlyList<CashAccountOption>> GetActiveCashAccountsAsync(
            CurrencyCode? currency = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CashAccountOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            CreateLedgerAccountCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCreate = command;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            UpdateLedgerAccountCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastUpdateVersion = version;
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
}
