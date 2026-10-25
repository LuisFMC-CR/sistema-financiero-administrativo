using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Incomes;

/// <summary>
/// Verifica, sin tocar SQL Server, que las rutas de <c>IncomesController</c> exigen exactamente las
/// políticas de la matriz de perfiles: Gerencia y Finanzas consultan y registran ingresos manuales;
/// solo Gerencia los anula. Sigue el mismo patrón que <c>InvoiceAuthorizationTests</c>.
/// </summary>
public sealed class IncomesAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public IncomesAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task IncomeRoutes_WhenAnonymous_RedirectToLogin()
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync("/ingresos");

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void IndexEndpoint_RequiresOnlyViewFinancialReports()
    {
        RouteEndpoint index = Assert.Single(
            GetEndpoints(),
            endpoint => endpoint.RoutePattern.RawText == "ingresos");

        IAuthorizeData[] metadata = index.Metadata.GetOrderedMetadata<IAuthorizeData>().ToArray();
        Assert.Contains(metadata, data => data.Policy == SystemPolicies.ViewFinancialReports);
        Assert.DoesNotContain(metadata, data => data.Policy == SystemPolicies.ConfirmFinancialTransactions);
        Assert.DoesNotContain(metadata, data => data.Policy == SystemPolicies.VoidFinancialTransactions);
    }

    [Fact]
    public void CreateEndpoints_RequireConfirmFinancialTransactionsNotVoid()
    {
        RouteEndpoint[] createEndpoints = GetEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText!.EndsWith("nuevo", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(createEndpoints);
        Assert.All(
            createEndpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ConfirmFinancialTransactions));
        Assert.All(
            createEndpoints,
            endpoint => Assert.DoesNotContain(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.VoidFinancialTransactions));
    }

    [Fact]
    public void CancelEndpoints_RequireVoidFinancialTransactionsNotConfirm()
    {
        RouteEndpoint[] cancelEndpoints = GetEndpoints()
            .Where(endpoint => endpoint.RoutePattern.RawText!.EndsWith("anular", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(cancelEndpoints);
        Assert.All(
            cancelEndpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.VoidFinancialTransactions));
        Assert.All(
            cancelEndpoints,
            endpoint => Assert.DoesNotContain(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ConfirmFinancialTransactions));
    }

    private RouteEndpoint[] GetEndpoints()
    {
        return factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("ingresos", StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
    }
}
