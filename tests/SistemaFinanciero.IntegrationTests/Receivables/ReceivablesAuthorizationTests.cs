using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Receivables;

/// <summary>
/// Verifica, sin tocar SQL Server, que las rutas de <c>ReceivablesController</c> y
/// <c>ReceivablePaymentsController</c> exigen exactamente las políticas de la matriz de perfiles:
/// Gerencia y Finanzas consultan cuentas por cobrar y registran abonos; solo Gerencia los anula.
/// Sigue el mismo patrón que <c>InvoiceAuthorizationTests</c>.
/// </summary>
public sealed class ReceivablesAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public ReceivablesAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/cuentas-por-cobrar")]
    [InlineData("/cuentas-por-cobrar/abonos/nuevo")]
    public async Task ReceivableRoutes_WhenAnonymous_RedirectToLogin(string route)
    {
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/cuenta/iniciar-sesion", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public void ReceivablesEndpoints_RequireViewFinancialReports()
    {
        RouteEndpoint[] endpoints = GetEndpointsStartingWith("cuentas-por-cobrar")
            .Where(endpoint => !endpoint.RoutePattern.RawText!.Contains("abonos", StringComparison.Ordinal)
                && !endpoint.RoutePattern.RawText!.Contains("ajustar", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(
            endpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ViewFinancialReports));
    }

    [Fact]
    public void BalanceAdjustmentEndpoints_RequireAdjustFinancialBalancesNotTheOthers()
    {
        RouteEndpoint[] endpoints = GetEndpointsStartingWith("cuentas-por-cobrar")
            .Where(endpoint => endpoint.RoutePattern.RawText!.Contains("ajustar", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(
            endpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.AdjustFinancialBalances));
        Assert.All(
            endpoints,
            endpoint => Assert.DoesNotContain(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ViewFinancialReports));
    }

    [Fact]
    public void ReceivablePaymentsEndpoints_RequireTheirOwnPolicyNotTheOthers()
    {
        RouteEndpoint[] endpoints = GetEndpointsStartingWith("cuentas-por-cobrar/abonos");
        Assert.NotEmpty(endpoints);

        RouteEndpoint[] registerEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText!.EndsWith("nuevo", StringComparison.Ordinal)
                || endpoint.RoutePattern.RawText!.EndsWith("aplicar", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(registerEndpoints);
        Assert.All(
            registerEndpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ConfirmFinancialTransactions));
        Assert.All(
            registerEndpoints,
            endpoint => Assert.DoesNotContain(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.VoidFinancialTransactions));

        RouteEndpoint[] cancelEndpoints = endpoints
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

    private RouteEndpoint[] GetEndpointsStartingWith(string prefix)
    {
        return factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();
    }
}
