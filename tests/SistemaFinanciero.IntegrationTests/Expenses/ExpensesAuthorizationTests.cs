using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Expenses;

/// <summary>
/// Verifica, sin tocar SQL Server, que las rutas de <c>ExpensesController</c> exigen exactamente las
/// políticas de la matriz de perfiles: Asistente, Finanzas y Gerencia preparan borradores y confirman,
/// y solo Gerencia anula. Sigue el mismo patrón que <c>InvoiceAuthorizationTests</c>.
/// </summary>
public sealed class ExpensesAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public ExpensesAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/gastos")]
    [InlineData("/gastos/crear")]
    public async Task ExpenseRoutes_WhenAnonymous_RedirectToLogin(string route)
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
    public void ExpenseEndpoints_RequireExactlyThePolicyOfEachAction()
    {
        RouteEndpoint[] endpoints = factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "gastos",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.NotEmpty(endpoints);

        // Confirmar y anular son las acciones más sensibles: cada una exige su propia política además
        // de la de solo-consulta de la clase, nunca la política del otro perfil.
        RouteEndpoint[] confirmEndpoints = endpoints
            .Where(endpoint => endpoint.RoutePattern.RawText!.EndsWith("confirmar", StringComparison.Ordinal))
            .ToArray();
        Assert.NotEmpty(confirmEndpoints);
        Assert.All(
            confirmEndpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.ConfirmFinancialTransactions));
        Assert.All(
            confirmEndpoints,
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

        // Crear y editar encabezado son de Asistente y Finanzas.
        string[] draftActionSuffixes = ["crear", "encabezado"];
        RouteEndpoint[] draftEndpoints = endpoints
            .Where(endpoint => draftActionSuffixes.Any(suffix =>
                endpoint.RoutePattern.RawText!.EndsWith(suffix, StringComparison.Ordinal)))
            .ToArray();
        Assert.NotEmpty(draftEndpoints);
        Assert.All(
            draftEndpoints,
            endpoint => Assert.Contains(
                endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                data => data.Policy == SystemPolicies.CreateFinancialDrafts));

        // El listado y el detalle son de solo consulta: no exigen ninguna política adicional.
        RouteEndpoint list = Assert.Single(
            endpoints,
            endpoint => endpoint.RoutePattern.RawText == "gastos");
        Assert.DoesNotContain(
            list.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == SystemPolicies.CreateFinancialDrafts);
    }
}
