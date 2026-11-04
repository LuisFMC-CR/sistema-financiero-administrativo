using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Application.Catalogs.WithholdingTypes;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Controllers;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class TaxesAndWithholdingsControllerTests
{
    [Fact]
    public async Task CreateTaxType_WithAPercentageAboveOneHundred_ReturnsAnErrorWithoutCallingTheService()
    {
        RecordingTaxTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(service, new RecordingWithholdingTypeService(), isManagement: true);
        TaxTypeCreateViewModel model = new() { Code = "ESP", Name = "Impuesto", Rate = 100.5m };

        IActionResult result = await controller.CreateTaxType(model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Rate)));
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task CreateTaxType_WithoutManagement_IsForbidden()
    {
        RecordingTaxTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(service, new RecordingWithholdingTypeService(), isManagement: false);
        TaxTypeCreateViewModel model = new() { Code = "ESP", Name = "Impuesto", Rate = 13m, HaciendaRateCode = "08" };

        IActionResult result = await controller.CreateTaxType(model, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task CreateTaxType_WithValidData_CallsTheServiceAndRedirectsToIndex()
    {
        RecordingTaxTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(service, new RecordingWithholdingTypeService(), isManagement: true);
        TaxTypeCreateViewModel model = new() { Code = "IVA-2", Name = "IVA 2 %", Rate = 2m, HaciendaRateCode = "03" };

        IActionResult result = await controller.CreateTaxType(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TaxesAndWithholdingsController.Index), redirect.ActionName);
        Assert.Equal(1, service.CreateCalls);
        Assert.Equal(2m, service.LastCreate!.Rate);
    }

    [Fact]
    public async Task EditTaxType_IgnoresThePostedCodeNameAndHaciendaRateCodeAndKeepsTheCurrentOnes()
    {
        Guid id = Guid.NewGuid();
        RecordingTaxTypeService service = new() { Current = CreateTaxType(id, "IVA-13", "IVA", 13m, "08") };
        TaxesAndWithholdingsController controller = CreateController(service, new RecordingWithholdingTypeService(), isManagement: true);
        TaxTypeRateEditViewModel model = new()
        {
            Code = "TAMPERED",
            Name = "Tampered",
            HaciendaRateCode = "99",
            Rate = 12m,
            Version = "v1",
        };

        IActionResult result = await controller.EditTaxType(id, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal("IVA-13", service.LastUpdate!.Code);
        Assert.Equal("IVA", service.LastUpdate.Name);
        Assert.Equal("08", service.LastUpdate.HaciendaRateCode);
        Assert.Equal(12m, service.LastUpdate.Rate);
    }

    [Fact]
    public async Task EditTaxType_WhenTheTaxTypeDoesNotExist_ReturnsNotFound()
    {
        RecordingTaxTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(service, new RecordingWithholdingTypeService(), isManagement: true);

        IActionResult result = await controller.EditTaxType(
            Guid.NewGuid(),
            new TaxTypeRateEditViewModel { Rate = 13m, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, service.UpdateCalls);
    }

    [Fact]
    public async Task EditTaxType_OnAConcurrencyConflict_ReplacesTheVersionAndReturns409()
    {
        Guid id = Guid.NewGuid();
        RecordingTaxTypeService service = new()
        {
            Current = CreateTaxType(id, "IVA-13", "IVA", 13m, "08", version: "version-vigente"),
            OperationResult = CatalogOperationResult.Concurrent(),
        };
        TaxesAndWithholdingsController controller = CreateController(service, new RecordingWithholdingTypeService(), isManagement: true);
        TaxTypeRateEditViewModel model = new() { Rate = 12m, Version = "version-vieja" };

        IActionResult result = await controller.EditTaxType(id, model, CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal("version-vigente", model.Version);
        Assert.Equal(StatusCodes.Status409Conflict, controller.Response.StatusCode);
    }

    [Fact]
    public async Task SetTaxTypeActive_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingTaxTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(
            service,
            new RecordingWithholdingTypeService(),
            isManagement: true,
            withActor: false);

        IActionResult result = await controller.SetTaxTypeActive(
            Guid.NewGuid(),
            new CatalogStatusViewModel { IsActive = false, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task CreateWithholdingType_WithValidData_CallsTheServiceAndRedirectsToIndex()
    {
        RecordingWithholdingTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(new RecordingTaxTypeService(), service, isManagement: true);
        WithholdingTypeCreateViewModel model = new() { Code = "RET-2", Name = "Retención 2 %", Rate = 2m };

        IActionResult result = await controller.CreateWithholdingType(model, CancellationToken.None);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(TaxesAndWithholdingsController.Index), redirect.ActionName);
        Assert.Equal(1, service.CreateCalls);
        Assert.Equal(2m, service.LastCreate!.Rate);
    }

    [Fact]
    public async Task CreateWithholdingType_WithoutManagement_IsForbidden()
    {
        RecordingWithholdingTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(new RecordingTaxTypeService(), service, isManagement: false);
        WithholdingTypeCreateViewModel model = new() { Code = "RET-2", Name = "Retención 2 %", Rate = 2m };

        IActionResult result = await controller.CreateWithholdingType(model, CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(0, service.CreateCalls);
    }

    [Fact]
    public async Task EditWithholdingType_IgnoresThePostedCodeAndNameAndKeepsTheCurrentOnes()
    {
        Guid id = Guid.NewGuid();
        RecordingWithholdingTypeService service = new() { Current = CreateWithholdingType(id, "RET-2", "Retención 2 %", 2m, 462200m) };
        TaxesAndWithholdingsController controller = CreateController(new RecordingTaxTypeService(), service, isManagement: true);
        WithholdingTypeRateEditViewModel model = new()
        {
            Code = "TAMPERED",
            Name = "Tampered",
            Rate = 3m,
            MinimumBase = 500000m,
            Version = "v1",
        };

        IActionResult result = await controller.EditWithholdingType(id, model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(1, service.UpdateCalls);
        Assert.Equal("RET-2", service.LastUpdate!.Code);
        Assert.Equal("Retención 2 %", service.LastUpdate.Name);
        Assert.Equal(3m, service.LastUpdate.Rate);
        Assert.Equal(500000m, service.LastUpdate.MinimumBase);
    }

    [Fact]
    public async Task EditWithholdingType_WhenTheWithholdingTypeDoesNotExist_ReturnsNotFound()
    {
        RecordingWithholdingTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(new RecordingTaxTypeService(), service, isManagement: true);

        IActionResult result = await controller.EditWithholdingType(
            Guid.NewGuid(),
            new WithholdingTypeRateEditViewModel { Rate = 2m, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Equal(0, service.UpdateCalls);
    }

    [Fact]
    public async Task SetWithholdingTypeActive_WithoutAnAuthenticatedUser_IsForbidden()
    {
        RecordingWithholdingTypeService service = new();
        TaxesAndWithholdingsController controller = CreateController(
            new RecordingTaxTypeService(),
            service,
            isManagement: true,
            withActor: false);

        IActionResult result = await controller.SetWithholdingTypeActive(
            Guid.NewGuid(),
            new CatalogStatusViewModel { IsActive = false, Version = "v1" },
            CancellationToken.None);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Index_NormalizesUntrustedFiltersForBothSectionsBeforeCallingTheServices()
    {
        RecordingTaxTypeService taxService = new();
        RecordingWithholdingTypeService withholdingService = new();
        TaxesAndWithholdingsController controller = CreateController(taxService, withholdingService, isManagement: true);

        IActionResult result = await controller.Index(
            taxSearch: new string('x', 150),
            taxStatus: (CatalogStatusFilter)999,
            taxPage: 1,
            withholdingSearch: new string('y', 150),
            withholdingStatus: (CatalogStatusFilter)999,
            withholdingPage: 1,
            cancellationToken: CancellationToken.None);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(new string('x', 100), taxService.LastQuery!.Search);
        Assert.Equal(CatalogStatusFilter.Active, taxService.LastQuery.Status);
        Assert.Equal(new string('y', 100), withholdingService.LastQuery!.Search);
        Assert.Equal(CatalogStatusFilter.Active, withholdingService.LastQuery.Status);
    }

    private static TaxTypeModel CreateTaxType(
        Guid id,
        string code,
        string name,
        decimal rate,
        string? haciendaRateCode,
        string version = "v1") =>
        new(
            id,
            code,
            name,
            rate,
            haciendaRateCode,
            null,
            IsActive: true,
            SystemRole: null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            version);

    private static WithholdingTypeModel CreateWithholdingType(
        Guid id,
        string code,
        string name,
        decimal rate,
        decimal? minimumBase,
        string version = "v1") =>
        new(id, code, name, rate, minimumBase, null, true, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, version);

    private static TaxesAndWithholdingsController CreateController(
        ITaxTypeService taxTypeService,
        IWithholdingTypeService withholdingTypeService,
        bool isManagement,
        bool withActor = true)
    {
        DefaultHttpContext httpContext = new();

        if (withActor)
        {
            List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())];
            if (isManagement)
            {
                claims.Add(new Claim(ClaimTypes.Role, SystemRoles.Management));
            }

            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"));
        }

        return new TaxesAndWithholdingsController(taxTypeService, withholdingTypeService, new StubAuthorizationService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new NullTempDataProvider()),
        };
    }

    private sealed class StubAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, IEnumerable<IAuthorizationRequirement> requirements) =>
            Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object? resource, string policyName) =>
            Task.FromResult(AuthorizationResult.Success());
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

        public CreateTaxTypeCommand? LastCreate { get; private set; }

        public UpdateTaxTypeCommand? LastUpdate { get; private set; }

        public string? LastUpdateVersion { get; private set; }

        public CatalogQuery? LastQuery { get; private set; }

        public TaxTypeModel? Current { get; init; }

        public CatalogOperationResult OperationResult { get; init; } = CatalogOperationResult.Succeeded();

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
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ReferenceTaxTypesLoadResult(CatalogOperationResult.Succeeded(), 0, 0));
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
