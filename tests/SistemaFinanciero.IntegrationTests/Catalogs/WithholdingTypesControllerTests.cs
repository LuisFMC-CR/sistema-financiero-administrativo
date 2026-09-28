using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class WithholdingTypesControllerTests
{
    [Fact]
    public async Task Create_WithValidData_CallsTheServiceAndRedirects()
    {
        RecordingWithholdingTypeService service = new();
        WithholdingTypesController controller = CreateController(service);
        WithholdingTypeCreateViewModel model = new() { Code = "RET-2", Name = "Retención 2 %", Rate = 2m };

        IActionResult result = await controller.Create(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(WithholdingTypesController.Index), redirect.ActionName);
        Assert.Equal(1, service.CreateCalls);
        Assert.Equal(2m, service.LastCreate!.Rate);
    }

    [Fact]
    public async Task Create_WithARateAboveOneHundred_ReturnsAnErrorWithoutCallingTheService()
    {
        RecordingWithholdingTypeService service = new();
        WithholdingTypesController controller = CreateController(service);
        WithholdingTypeCreateViewModel model = new() { Code = "RET", Name = "Retención", Rate = 150m };
        controller.ModelState.AddModelError(nameof(model.Rate), "La tarifa debe estar entre 0 y 100 %.");

        IActionResult result = await controller.Create(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task Create_WhenTheCodeAlreadyExists_ShowsTheFormAgainWithTheMessage()
    {
        RecordingWithholdingTypeService service = new()
        {
            OperationResult = CatalogOperationResult.Duplicated("Ya existe un tipo de retención con el mismo código."),
        };
        WithholdingTypesController controller = CreateController(service);

        IActionResult result = await controller.Create(
            new WithholdingTypeCreateViewModel { Code = "RET", Name = "Retención", Rate = 2m },
            CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal(nameof(WithholdingTypesController.Create), view.ViewName);
        Assert.False(controller.ModelState.IsValid);
    }

    [Fact]
    public async Task Edit_WithValidData_UpdatesAndRedirects()
    {
        Guid id = Guid.NewGuid();
        RecordingWithholdingTypeService service = new() { Current = CreateModel(id, "RET-2", 2m) };
        WithholdingTypesController controller = CreateController(service);
        WithholdingTypeEditViewModel model = new() { Code = "RET-2", Name = "Retención", Rate = 4m, Version = "v1" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal(4m, service.LastUpdate!.Rate);
        Assert.Equal("v1", service.LastUpdateVersion);
    }

    [Fact]
    public async Task Edit_OnAConcurrencyConflict_ReplacesTheVersionAndReturns409()
    {
        Guid id = Guid.NewGuid();
        RecordingWithholdingTypeService service = new()
        {
            Current = CreateModel(id, "RET-2", 2m, version: "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        WithholdingTypesController controller = CreateController(service);
        WithholdingTypeEditViewModel model = new() { Code = "RET-2", Name = "Retención", Rate = 4m, Version = "version-vieja" };

        IActionResult result = await controller.Edit(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("version-vigente", model.Version);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Edit_WhenTheServiceReportsTheRecordIsMissing_ReturnsNotFound()
    {
        // El servicio ya no tiene el registro (por ejemplo, otro usuario lo eliminó entre pantallas).
        // El controlador no consulta el registro antes de actualizar, porque no hay campos fijos que
        // restaurar; se apoya en la respuesta del servicio para el caso "no existe".
        RecordingWithholdingTypeService service = new() { OperationResult = CatalogOperationResult.Missing() };
        WithholdingTypesController controller = CreateController(service);

        IActionResult result = await controller.Edit(
            Guid.NewGuid(),
            new WithholdingTypeEditViewModel { Code = "X", Name = "X", Rate = 2m, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(1, service.UpdateCalls);
    }

    [Fact]
    public async Task Edit_WhenTheRouteIdHasNoMatchingRecordOnGet_ReturnsNotFound()
    {
        RecordingWithholdingTypeService service = new();
        WithholdingTypesController controller = CreateController(service);

        IActionResult result = await controller.Edit(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_NormalizesUntrustedFiltersBeforeCallingTheService()
    {
        RecordingWithholdingTypeService service = new();
        WithholdingTypesController controller = CreateController(service);

        IActionResult action = await controller.Index(
            new string('x', 150),
            (CatalogStatusFilter)999,
            page: 1,
            CancellationToken.None);

        Assert.IsType<ViewResult>(action);
        Assert.Equal(new string('x', 100), service.LastQuery!.Search);
        Assert.Equal(CatalogStatusFilter.Active, service.LastQuery.Status);
    }

    [Fact]
    public async Task SetActive_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingWithholdingTypeService service = new();
        WithholdingTypesController controller = CreateController(service, withActor: false);

        IActionResult result = await controller.SetActive(
            Guid.NewGuid(),
            new CatalogStatusViewModel { IsActive = false, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    private static WithholdingTypeModel CreateModel(
        Guid id,
        string code,
        decimal rate,
        string version = "v1") =>
        new(id, code, "Retención", rate, null, IsActive: true, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, version);

    private static WithholdingTypesController CreateController(
        RecordingWithholdingTypeService service,
        bool withActor = true)
    {
        DefaultHttpContext httpContext = new();

        if (withActor)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Test"));
        }

        return new WithholdingTypesController(service)
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

    private sealed class RecordingWithholdingTypeService : IWithholdingTypeService
    {
        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public CreateWithholdingTypeCommand? LastCreate { get; private set; }

        public UpdateWithholdingTypeCommand? LastUpdate { get; private set; }

        public string? LastUpdateVersion { get; private set; }

        public CatalogQuery? LastQuery { get; private set; }

        public WithholdingTypeModel? Current { get; init; }

        public CatalogOperationResult OperationResult { get; init; } = CatalogOperationResult.Succeeded();

        public Task<PagedResult<WithholdingTypeModel>> SearchAsync(
            CatalogQuery query,
            CancellationToken cancellationToken = default)
        {
            LastQuery = query;
            return Task.FromResult(new PagedResult<WithholdingTypeModel>([], query.Page, query.PageSize, 0));
        }

        public Task<WithholdingTypeModel?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<IReadOnlyList<WithholdingTypeOption>> GetActiveOptionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WithholdingTypeOption>>([]);

        public Task<CatalogOperationResult> CreateAsync(
            CreateWithholdingTypeCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCreate = command;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> UpdateAsync(
            Guid id,
            UpdateWithholdingTypeCommand command,
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
