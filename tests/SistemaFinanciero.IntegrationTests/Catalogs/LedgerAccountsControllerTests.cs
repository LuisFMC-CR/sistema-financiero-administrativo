using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.LedgerAccounts;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class LedgerAccountsControllerTests
{
    [Fact]
    public async Task Create_ForACashAccount_SendsTheKind()
    {
        RecordingLedgerAccountService service = new();
        LedgerAccountsController controller = CreateController(service);
        Guid parentId = Guid.NewGuid();
        LedgerAccountCreateViewModel model = new()
        {
            Name = "Banco",
            ParentId = parentId,
            IsCash = true,
            CashKind = CashAccountKind.Bank,
        };

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(parentId, service.LastCreate!.ParentId);
        Assert.Equal(CashAccountKind.Bank, service.LastCreate.CashKind);
    }

    [Fact]
    public async Task Create_ForAnAccountThatIsNotCash_DiscardsThePostedKind()
    {
        RecordingLedgerAccountService service = new();
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountCreateViewModel model = new()
        {
            Name = "Ingresos",
            ParentId = Guid.NewGuid(),
            IsCash = false,

            // Valor que un formulario manipulado o un campo oculto podría enviar.
            CashKind = CashAccountKind.Cash,
        };

        await controller.Create(model, CancellationToken.None);

        Assert.Null(service.LastCreate!.CashKind);
    }

    [Fact]
    public async Task Create_WithInvalidData_ShowsTheFormAgainWithParentOptionsOfEveryType()
    {
        RecordingLedgerAccountService service = new();
        service.ParentOptionsByType[LedgerAccountType.Asset] = [new LedgerAccountOption(Guid.NewGuid(), "1", "Activos")];
        service.ParentOptionsByType[LedgerAccountType.Income] = [new LedgerAccountOption(Guid.NewGuid(), "4", "Ingresos")];
        LedgerAccountsController controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(LedgerAccountCreateViewModel.Name), "El nombre es obligatorio.");
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
            new LedgerAccountCreateViewModel { Name = "A", ParentId = Guid.NewGuid() },
            CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(LedgerAccountsController.Create), view.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_Get_PopulatesDisplayFieldsFromTheCurrentAccount()
    {
        Guid id = Guid.NewGuid();
        Guid parentId = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Asset, CashAccountKind.Bank) with
            {
                ParentId = parentId,
                ParentName = "Efectivo",
            },
        };
        LedgerAccountsController controller = CreateController(service);

        IActionResult result = await controller.Edit(id, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        LedgerAccountEditViewModel model = Assert.IsType<LedgerAccountEditViewModel>(view.Model);
        Assert.Equal("COD", model.Code);
        Assert.Equal(LedgerAccountType.Asset, model.Type);
        Assert.Equal("Efectivo", model.ParentName);
        Assert.Equal(CashAccountKind.Bank, model.CashKind);
    }

    [Fact]
    public async Task Edit_PopulatesDisplayFieldsEvenWhenTheModelIsInvalid()
    {
        Guid id = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Expense, null),
        };
        LedgerAccountsController controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(LedgerAccountEditViewModel.Name), "El nombre es obligatorio.");
        LedgerAccountEditViewModel model = new() { Name = string.Empty, Version = "v1" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal("COD", model.Code);
        Assert.Equal(LedgerAccountType.Expense, model.Type);
    }

    [Fact]
    public async Task Edit_WithValidData_UpdatesOnlyNameReferenceAndDescription()
    {
        Guid id = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Asset, CashAccountKind.Bank),
        };
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountEditViewModel model = new()
        {
            Name = "Banco renombrado",
            Reference = "Terminada en 1234",
            Description = "Cuenta principal",
            Version = "v1",
        };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal("Banco renombrado", service.LastUpdate!.Name);
        Assert.Equal("Terminada en 1234", service.LastUpdate.Reference);
        Assert.Equal("v1", service.LastUpdateVersion);
    }

    [Fact]
    public async Task Edit_OnAConcurrencyConflict_ReplacesTheVersionAndReturns409()
    {
        Guid id = Guid.NewGuid();
        RecordingLedgerAccountService service = new()
        {
            Current = CreateModel(id, LedgerAccountType.Asset, null, version: "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        LedgerAccountsController controller = CreateController(service);
        LedgerAccountEditViewModel model = new() { Name = "A", Version = "version-vieja" };

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
            new LedgerAccountEditViewModel { Name = "X", Version = "v1" },
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
        string version = "v1") =>
        new(
            id,
            "COD",
            "Cuenta",
            type,
            ParentId: null,
            ParentName: null,
            cashKind,
            Reference: null,
            Description: null,
            IsActive: true,
            IsProtected: false,
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

        public UpdateLedgerAccountCommand? LastUpdate { get; private set; }

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
            LastUpdate = command;
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
