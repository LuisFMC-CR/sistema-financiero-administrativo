using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Parameters;

namespace SistemaFinanciero.IntegrationTests.Parameters;

public sealed class SystemParametersControllerTests
{
    [Fact]
    public async Task Save_WithoutAVersion_CallsCreate()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service);
        SystemParametersViewModel model = new() { AuthorizationLimitCrc = 500_000m, OverdueAlertDays = 7 };

        IActionResult result = await controller.Save(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.CreateCalls);
        Assert.Equal(0, service.UpdateCalls);
        Assert.Equal(500_000m, service.LastCommand!.AuthorizationLimitCrc);
    }

    [Fact]
    public async Task Save_WithAVersion_CallsUpdate()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service);
        SystemParametersViewModel model = new()
        {
            AuthorizationLimitCrc = 750_000m,
            OverdueAlertDays = 10,
            Version = "v1",
        };

        IActionResult result = await controller.Save(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(0, service.CreateCalls);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal("v1", service.LastUpdateVersion);
    }

    [Fact]
    public async Task Save_WithInvalidData_DoesNotCallTheService()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(SystemParametersViewModel.OverdueAlertDays), "Inválido.");

        IActionResult result = await controller.Save(new SystemParametersViewModel(), CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, service.CreateCalls);
        Assert.Equal(0, service.UpdateCalls);
    }

    [Fact]
    public async Task Save_OnAConcurrencyConflict_ReplacesTheVersionAndReturns409()
    {
        RecordingSystemParametersService service = new()
        {
            Current = new SystemParametersModel(
                500_000m, 7, new DateOnly(2026, 1, 1), null, null, null, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        SystemParametersController controller = CreateController(service);
        SystemParametersViewModel model = new()
        {
            AuthorizationLimitCrc = 600_000m,
            OverdueAlertDays = 7,
            Version = "version-vieja",
        };

        IActionResult result = await controller.Save(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        SystemParametersPageViewModel pageModel = Assert.IsType<SystemParametersPageViewModel>(view.Model);
        Assert.Equal("version-vigente", pageModel.Parameters.Version);
        // El monto propuesto se conserva en el formulario para que el usuario no lo vuelva a digitar.
        Assert.Equal(600_000m, pageModel.Parameters.AuthorizationLimitCrc);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
    }

    [Fact]
    public async Task Save_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service, withActor: false);

        IActionResult result = await controller.Save(
            new SystemParametersViewModel { AuthorizationLimitCrc = 1m, OverdueAlertDays = 7 },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task Index_WhenNotConfigured_ShowsAnEmptyModel()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service);

        IActionResult result = await controller.Index(CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        SystemParametersPageViewModel model = Assert.IsType<SystemParametersPageViewModel>(view.Model);
        Assert.Equal(string.Empty, model.Parameters.Version);
    }

    [Fact]
    public async Task Index_WhenConfigured_ShowsTheCurrentValues()
    {
        RecordingSystemParametersService service = new()
        {
            Current = new SystemParametersModel(
                500_000m, 7, new DateOnly(2026, 1, 1), null, null, null, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1"),
        };
        SystemParametersController controller = CreateController(service);

        IActionResult result = await controller.Index(CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        SystemParametersPageViewModel model = Assert.IsType<SystemParametersPageViewModel>(view.Model);
        Assert.Equal(500_000m, model.Parameters.AuthorizationLimitCrc);
        Assert.Equal("v1", model.Parameters.Version);
        Assert.Equal("Gerente", model.Parameters.UpdatedByUserName);
        Assert.Equal(new DateOnly(2026, 1, 1), model.Parameters.ClosingDate);
        Assert.Equal(new DateOnly(2026, 1, 1), model.ClosingDateForm.NewClosingDate);
        Assert.Equal("v1", model.ClosingDateForm.Version);
    }

    [Fact]
    public async Task ChangeClosingDate_CallsServiceAndRedirectsOnSuccess()
    {
        RecordingSystemParametersService service = new()
        {
            Current = new SystemParametersModel(
                500_000m, 7, new DateOnly(2026, 1, 1), null, null, null, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1"),
        };
        SystemParametersController controller = CreateController(service);
        ChangeClosingDateViewModel model = new()
        {
            NewClosingDate = new DateOnly(2026, 2, 1),
            Version = "v1",
        };

        IActionResult result = await controller.ChangeClosingDate(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.ChangeClosingDateCalls);
        Assert.Equal(new DateOnly(2026, 2, 1), service.LastNewClosingDate);
        Assert.Equal("v1", service.LastClosingDateVersion);
    }

    [Fact]
    public async Task ChangeClosingDate_OnInvalidResult_ReturnsIndexWithThePageModel()
    {
        RecordingSystemParametersService service = new()
        {
            Current = new SystemParametersModel(
                500_000m, 7, new DateOnly(2026, 2, 1), null, null, null, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1"),
            OperationResult = CatalogOperationResult.Invalid("El motivo de la reapertura es obligatorio."),
        };
        SystemParametersController controller = CreateController(service);
        ChangeClosingDateViewModel model = new()
        {
            NewClosingDate = new DateOnly(2026, 1, 1),
            Version = "v1",
        };

        IActionResult result = await controller.ChangeClosingDate(model, CancellationToken.None);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        SystemParametersPageViewModel pageModel = Assert.IsType<SystemParametersPageViewModel>(view.Model);
        Assert.Equal(new DateOnly(2026, 1, 1), pageModel.ClosingDateForm.NewClosingDate);
        Assert.Equal(new DateOnly(2026, 2, 1), pageModel.Parameters.ClosingDate);
    }

    [Fact]
    public async Task ChangeClosingDate_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service, withActor: false);

        IActionResult result = await controller.ChangeClosingDate(
            new ChangeClosingDateViewModel { NewClosingDate = new DateOnly(2026, 1, 1), Version = "v1" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.ChangeClosingDateCalls);
    }

    [Fact]
    public async Task UpdateCompany_CallsServiceAndRedirectsOnSuccess()
    {
        RecordingSystemParametersService service = new()
        {
            Current = new SystemParametersModel(
                500_000m, 7, new DateOnly(2026, 1, 1), null, null, null, null, null,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente", "v1"),
        };
        SystemParametersController controller = CreateController(service);
        CompanyDataViewModel model = new()
        {
            Name = "Soporte Experto",
            LegalId = "3-101-123456",
            Version = "v1",
        };

        IActionResult result = await controller.UpdateCompany(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.UpdateCompanyCalls);
    }

    [Fact]
    public async Task UpdateCompany_WithInvalidData_DoesNotCallTheService()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service);
        controller.ModelState.AddModelError(nameof(CompanyDataViewModel.Name), "Inválido.");

        IActionResult result = await controller.UpdateCompany(new CompanyDataViewModel(), CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(0, service.UpdateCompanyCalls);
    }

    [Fact]
    public async Task UpdateCompany_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingSystemParametersService service = new();
        SystemParametersController controller = CreateController(service, withActor: false);

        IActionResult result = await controller.UpdateCompany(
            new CompanyDataViewModel { Name = "Soporte Experto", LegalId = "3-101-123456", Version = "v1" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.UpdateCompanyCalls);
    }

    private static SystemParametersController CreateController(
        RecordingSystemParametersService service,
        bool withActor = true)
    {
        DefaultHttpContext httpContext = new();

        if (withActor)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
                authenticationType: "Test"));
        }

        return new SystemParametersController(service)
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

    private sealed class RecordingSystemParametersService : ISystemParametersService
    {
        public int CreateCalls { get; private set; }

        public int UpdateCalls { get; private set; }

        public int ChangeClosingDateCalls { get; private set; }

        public int UpdateCompanyCalls { get; private set; }

        public SaveSystemParametersCommand? LastCommand { get; private set; }

        public string? LastUpdateVersion { get; private set; }

        public DateOnly? LastNewClosingDate { get; private set; }

        public string? LastClosingDateVersion { get; private set; }

        public SystemParametersModel? Current { get; init; }

        public CatalogOperationResult OperationResult { get; init; } = CatalogOperationResult.Succeeded();

        public Task<SystemParametersModel?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);

        public Task<CatalogOperationResult> CreateAsync(
            SaveSystemParametersCommand command,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            LastCommand = command;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> UpdateAsync(
            SaveSystemParametersCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            LastCommand = command;
            LastUpdateVersion = version;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> ChangeClosingDateAsync(
            DateOnly newClosingDate,
            string? reason,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            ChangeClosingDateCalls++;
            LastNewClosingDate = newClosingDate;
            LastClosingDateVersion = version;
            return Task.FromResult(OperationResult);
        }

        public Task<CatalogOperationResult> UpdateCompanyAsync(
            SaveCompanyDataCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            UpdateCompanyCalls++;
            return Task.FromResult(OperationResult);
        }
    }
}
