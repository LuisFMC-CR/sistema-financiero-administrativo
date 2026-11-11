using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Parameters;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Parameters;

/// <summary>
/// Paquete v9, bloque 6 (CU-06, Tabla 10): los datos de la empresa y el límite de autorización solo
/// los cambia Gerencia; Finanzas solo cambia los días de anticipación. Pruebas HTTP con un servicio de
/// parámetros que registra las llamadas, sin tocar la base de datos.
/// </summary>
public sealed class SystemParametersAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public SystemParametersAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Finance_PostingCompanyData_IsForbiddenAndNothingChanges()
    {
        RecordingParametersService service = new();
        using HttpClient client = CreateClientFor(SystemRoles.Finance, service);

        using HttpResponseMessage response = await client.PostAsync("/parametros/datos-empresa", CompanyForm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, service.UpdateCompanyCalls);
    }

    [Fact]
    public async Task Management_PostingCompanyData_IsAccepted()
    {
        RecordingParametersService service = new();
        using HttpClient client = CreateClientFor(SystemRoles.Management, service);

        using HttpResponseMessage response = await client.PostAsync("/parametros/datos-empresa", CompanyForm());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(1, service.UpdateCompanyCalls);
    }

    [Theory]
    [InlineData(SystemRoles.Finance, false)]
    [InlineData(SystemRoles.Management, true)]
    public async Task SavingParameters_TellsTheServiceWhetherTheActorIsManagement(string role, bool expected)
    {
        RecordingParametersService service = new();
        using HttpClient client = CreateClientFor(role, service);

        using HttpResponseMessage response = await client.PostAsync(
            "/parametros",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("Parameters.Version", "version-actual"),
                new KeyValuePair<string, string>("Parameters.AuthorizationLimitCrc", "900000"),
                new KeyValuePair<string, string>("Parameters.OverdueAlertDays", "10"),
            ]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, service.LastUpdateIsManagement);
    }

    private static FormUrlEncodedContent CompanyForm() => new(
    [
        new KeyValuePair<string, string>("CompanyDataForm.Version", "version-actual"),
        new KeyValuePair<string, string>("CompanyDataForm.Name", "Soporte Experto"),
        new KeyValuePair<string, string>("CompanyDataForm.LegalId", "3-101-999999"),
        new KeyValuePair<string, string>("CompanyDataForm.BranchCode", "002"),
        new KeyValuePair<string, string>("CompanyDataForm.TerminalCode", "00009"),
    ]);

    private HttpClient CreateClientFor(string role, RecordingParametersService service)
    {
        WebApplicationFactory<Program> roleFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISystemParametersService>();
                services.AddSingleton<ISystemParametersService>(service);
                services.PostConfigure<MvcOptions>(options =>
                {
                    AutoValidateAntiforgeryTokenAttribute? antiforgeryFilter = options.Filters
                        .OfType<AutoValidateAntiforgeryTokenAttribute>()
                        .SingleOrDefault();
                    if (antiforgeryFilter is not null)
                    {
                        options.Filters.Remove(antiforgeryFilter);
                    }
                });
                RoleTestAuthentication.AddTo(services);
            }));

        HttpClient client = roleFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add(RoleTestAuthentication.RoleHeader, role);
        return client;
    }

    private sealed class RecordingParametersService : ISystemParametersService
    {
        public int UpdateCompanyCalls { get; private set; }

        public bool? LastUpdateIsManagement { get; private set; }

        public Task<SystemParametersModel?> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<SystemParametersModel?>(new SystemParametersModel(
                750_000m, 7, new DateOnly(2026, 1, 1), "Soporte Experto", "3-101-123456", null, null, null, null,
                null, null, null, null, "001", "00001", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Gerente",
                "version-actual", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        public Task<CatalogOperationResult> CreateAsync(
            SaveSystemParametersCommand command,
            Guid actorId,
            bool actorIsManagement,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> UpdateAsync(
            SaveSystemParametersCommand command,
            string version,
            Guid actorId,
            bool actorIsManagement,
            CancellationToken cancellationToken = default)
        {
            LastUpdateIsManagement = actorIsManagement;
            return Task.FromResult(CatalogOperationResult.Succeeded());
        }

        public Task<CatalogOperationResult> UpdateFiscalPeriodAsync(
            DateOnly startDate,
            DateOnly endDate,
            string? reason,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<IReadOnlyList<ReopenedAccountingPeriodModel>> GetReopenedAccountingPeriodsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ReopenedAccountingPeriodModel>>([]);

        public Task<CatalogOperationResult> ReopenAccountingPeriodAsync(
            int year, int month, string reason, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> CloseAccountingPeriodAsync(
            int year, int month, string version, Guid actorId, CancellationToken cancellationToken = default) =>
            Task.FromException<CatalogOperationResult>(new NotSupportedException());

        public Task<CatalogOperationResult> UpdateCompanyAsync(
            SaveCompanyDataCommand command,
            string version,
            Guid actorId,
            CancellationToken cancellationToken = default)
        {
            UpdateCompanyCalls++;
            return Task.FromResult(CatalogOperationResult.Succeeded());
        }
    }
}
