using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Ledger;

/// <summary>
/// Verifica, sin tocar SQL Server, que "Ver asiento" (<c>Trace</c>) exige
/// <c>Reportes.Ver</c> (Gerencia y Finanzas, igual que los reportes contables que también muestran
/// cuentas y saldos) y que el comando de generación (<c>Index</c>/<c>Backfill</c>) es exclusivo de
/// Gerencia vía <c>Parametros.Administrar</c>, porque nunca se ejecuta al arrancar y solo Gerencia
/// debe poder dispararlo (bloque 5 del paquete de mejoras finales).
/// </summary>
public sealed class JournalEntriesAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private const string RoutePrefix = "contabilidad/asientos";
    private readonly FinancialWebApplicationFactory factory;

    public JournalEntriesAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task JournalEntriesRoute_WhenAnonymous_RedirectsToLogin()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync($"/{RoutePrefix}");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Theory]
    [InlineData("contabilidad/asientos", SystemPolicies.ManageSystemParameters)]
    [InlineData("contabilidad/asientos/generar-pendientes", SystemPolicies.ManageSystemParameters)]
    [InlineData("contabilidad/asientos/por-origen/{sourceType:int}/{sourceId:guid}", SystemPolicies.ViewFinancialReports)]
    public void JournalEntriesEndpoint_RequiresTheExpectedPolicy(string routeTemplate, string expectedPolicy)
    {
        RouteEndpoint endpoint = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => string.Equals(
                candidate.RoutePattern.RawText,
                routeTemplate,
                StringComparison.Ordinal));

        Assert.Contains(
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => string.Equals(data.Policy, expectedPolicy, StringComparison.Ordinal));
    }
}
