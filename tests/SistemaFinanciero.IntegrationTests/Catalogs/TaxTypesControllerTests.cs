using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class TaxTypesControllerTests
{
    [Fact]
    public async Task Create_WithAPercentageAboveOneHundred_ReturnsAnErrorWithoutCallingTheService()
    {
        RecordingTaxTypeService service = new();
        TaxTypesController controller = CreateController(service);
        TaxTypeCreateViewModel model = CreateModel(TaxCalculationType.Percentage, rate: 100.5m);

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Rate)));
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task Create_WithAFixedAmountAboveOneHundred_CallsTheService()
    {
        RecordingTaxTypeService service = new();
        TaxTypesController controller = CreateController(service);
        TaxTypeCreateViewModel model = CreateModel(TaxCalculationType.FixedAmountPerUnit, rate: 250m);

        IActionResult result = await controller.Create(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TaxTypesController.Index), redirect.ActionName);
        Assert.Equal(1, service.CreateCalls);
        Assert.Equal(TaxCalculationType.FixedAmountPerUnit, service.LastCreate!.CalculationType);
        Assert.Equal(250m, service.LastCreate.Rate);
    }

    [Fact]
    public async Task Create_WhenTheCodeAlreadyExists_ShowsTheFormAgainWithTheMessage()
    {
        RecordingTaxTypeService service = new()
        {
            OperationResult = CatalogOperationResult.Duplicated("Ya existe un tipo de impuesto con el mismo código."),
        };
        TaxTypesController controller = CreateController(service);

        IActionResult result = await controller.Create(
            CreateModel(TaxCalculationType.Percentage, rate: 13m),
            CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(TaxTypesController.Create), view.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_NeverTakesTheCalculationTypeFromTheForm()
    {
        Guid id = Guid.NewGuid();
        RecordingTaxTypeService service = new() { Current = CreateTaxType(id, TaxCalculationType.Percentage, 13m) };
        TaxTypesController controller = CreateController(service);
        TaxTypeEditViewModel model = new()
        {
            Code = "IVA-13",
            Name = "IVA",
            Rate = 150m,
            Version = "v1",

            // Un formulario manipulado intenta presentar el impuesto como monto fijo para evitar el tope de 100 %.
            CalculationType = TaxCalculationType.FixedAmountPerUnit,
        };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(TaxCalculationType.Percentage, model.CalculationType);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Rate)));
        Assert.Equal(0, service.UpdateCalls);
    }

    [Fact]
    public async Task Edit_WithValidData_UpdatesAndRedirects()
    {
        Guid id = Guid.NewGuid();
        RecordingTaxTypeService service = new() { Current = CreateTaxType(id, TaxCalculationType.Percentage, 13m) };
        TaxTypesController controller = CreateController(service);
        TaxTypeEditViewModel model = new() { Code = "IVA-13", Name = "IVA", Rate = 12m, Version = "v1" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal(12m, service.LastUpdate!.Rate);
        Assert.Equal("v1", service.LastUpdateVersion);
    }

    [Fact]
    public async Task Edit_OnAConcurrencyConflict_ReplacesTheVersionAndReturns409()
    {
        Guid id = Guid.NewGuid();
        RecordingTaxTypeService service = new()
        {
            Current = CreateTaxType(id, TaxCalculationType.Percentage, 13m, version: "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        TaxTypesController controller = CreateController(service);
        TaxTypeEditViewModel model = new() { Code = "IVA-13", Name = "IVA", Rate = 12m, Version = "version-vieja" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("version-vigente", model.Version);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Edit_WhenTheTaxTypeDoesNotExist_ReturnsNotFound()
    {
        RecordingTaxTypeService service = new();
        TaxTypesController controller = CreateController(service);

        IActionResult result = await controller.Edit(
            Guid.NewGuid(),
            new TaxTypeEditViewModel { Code = "X", Name = "X", Version = "v1" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, service.UpdateCalls);
    }

    [Fact]
    public async Task LoadReference_ReportsHowManyTaxTypesWereCreated()
    {
        RecordingTaxTypeService service = new()
        {
            LoadResult = new ReferenceTaxTypesLoadResult(CatalogOperationResult.Succeeded(), 5, 3),
        };
        TaxTypesController controller = CreateController(service);

        IActionResult result = await controller.LoadReference(CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        string message = Assert.IsType<string>(controller.TempData["SuccessMessage"]);
        Assert.Contains("5", message);
        Assert.Contains("3 ya existían", message);
    }

    [Fact]
    public async Task LoadReference_WhenNothingIsMissing_SaysNoneWasCreated()
    {
        RecordingTaxTypeService service = new()
        {
            LoadResult = new ReferenceTaxTypesLoadResult(CatalogOperationResult.Succeeded(), 0, 8),
        };
        TaxTypesController controller = CreateController(service);

        await controller.LoadReference(CancellationToken.None);

        string message = Assert.IsType<string>(controller.TempData["SuccessMessage"]);
        Assert.Contains("no se creó ninguna", message);
    }

    [Fact]
    public async Task LoadReference_WhenTheServiceFails_ShowsAnError()
    {
        RecordingTaxTypeService service = new()
        {
            LoadResult = new ReferenceTaxTypesLoadResult(
                CatalogOperationResult.Duplicated("Ya existe un tipo de impuesto con el mismo código."),
                0,
                0),
        };
        TaxTypesController controller = CreateController(service);

        await controller.LoadReference(CancellationToken.None);

        Assert.Equal(
            "Ya existe un tipo de impuesto con el mismo código.",
            controller.TempData["ErrorMessage"]);
        Assert.Null(controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task LoadReference_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingTaxTypeService service = new();
        TaxTypesController controller = CreateController(service, withActor: false);

        IActionResult result = await controller.LoadReference(CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.LoadCalls);
    }

    [Fact]
    public async Task Index_NormalizesUntrustedFiltersBeforeCallingTheService()
    {
        RecordingTaxTypeService service = new();
        TaxTypesController controller = CreateController(service);

        IActionResult action = await controller.Index(
            new string('x', 150),
            (CatalogStatusFilter)999,
            page: 1,
            CancellationToken.None);

        Assert.IsType<ViewResult>(action);
        Assert.Equal(new string('x', 100), service.LastQuery!.Search);
        Assert.Equal(CatalogStatusFilter.Active, service.LastQuery.Status);
    }

    private static TaxTypeCreateViewModel CreateModel(TaxCalculationType calculationType, decimal rate) =>
        new()
        {
            Code = "ESP",
            Name = "Impuesto",
            CalculationType = calculationType,
            Rate = rate,
        };

    private static TaxTypeModel CreateTaxType(
        Guid id,
        TaxCalculationType calculationType,
        decimal rate,
        string version = "v1") =>
        new(
            id,
            "IVA-13",
            "IVA",
            calculationType,
            rate,
            null,
            IsActive: true,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            version);

    private static TaxTypesController CreateController(RecordingTaxTypeService service, bool withActor = true)
    {
        DefaultHttpContext httpContext = new();

        if (withActor)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Test"));
        }

        return new TaxTypesController(service)
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

    private sealed class RecordingTaxTypeService : ITaxTypeService
    {
        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public int LoadCalls { get; private set; }

        public CreateTaxTypeCommand? LastCreate { get; private set; }

        public UpdateTaxTypeCommand? LastUpdate { get; private set; }

        public string? LastUpdateVersion { get; private set; }

        public CatalogQuery? LastQuery { get; private set; }

        public TaxTypeModel? Current { get; init; }

        public CatalogOperationResult OperationResult { get; init; } = CatalogOperationResult.Succeeded();

        public ReferenceTaxTypesLoadResult LoadResult { get; init; } =
            new(CatalogOperationResult.Succeeded(), 0, 0);

        public Task<PagedResult<TaxTypeModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<TaxTypeModel>([], query.Page, query.PageSize, 0));
        }

        public Task<TaxTypeModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<IReadOnlyList<TaxTypeOption>> GetActiveOptionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TaxTypeOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            CreateTaxTypeCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCreate = command;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            UpdateTaxTypeCommand command,
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

        public Task<ReferenceTaxTypesLoadResult> LoadReferenceTaxTypesAsync(
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            LoadCalls++;
            return Task.FromResult(LoadResult);
        }
    }
}
