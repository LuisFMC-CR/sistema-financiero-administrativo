using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Invoices;

/// <summary>
/// Verifica, sin tocar SQL Server, que las rutas de <c>InvoicesController</c> exigen exactamente las
/// políticas de la matriz de perfiles: Asistente, Finanzas y Gerencia preparan borradores y confirman,
/// y solo Gerencia anula. Sigue el mismo patrón que <c>CatalogAuthorizationTests</c>.
/// </summary>
public sealed class InvoiceAuthorizationTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public InvoiceAuthorizationTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void ViewFinancialTransactionsPolicy_AllowsManagementFinanceAndAssistantOnly()
    {
        AssertPolicyRoles(
            SystemPolicies.ViewFinancialTransactions,
            SystemRoles.Management,
            SystemRoles.Finance,
            SystemRoles.Assistant);
    }

    [Fact]
    public void CreateFinancialDraftsPolicy_AllowsManagementFinanceAndAssistantOnly()
    {
        AssertPolicyRoles(
            SystemPolicies.CreateFinancialDrafts,
            SystemRoles.Management,
            SystemRoles.Finance,
            SystemRoles.Assistant);
    }

    [Fact]
    public void ConfirmFinancialTransactionsPolicy_AllowsManagementAndFinanceOnly()
    {
        AssertPolicyRoles(SystemPolicies.ConfirmFinancialTransactions, SystemRoles.Management, SystemRoles.Finance);
    }

    [Fact]
    public void VoidFinancialTransactionsPolicy_AllowsManagementOnly()
    {
        AssertPolicyRoles(SystemPolicies.VoidFinancialTransactions, SystemRoles.Management);
    }

    [Theory]
    [InlineData("/facturacion/facturas")]
    [InlineData("/facturacion/facturas/crear")]
    public async Task InvoiceRoutes_WhenAnonymous_RedirectToLogin(string route)
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
    public void InvoiceEndpoints_RequireExactlyThePolicyOfEachAction()
    {
        RouteEndpoint[] endpoints = factory.Services
            .GetServices<Microsoft.AspNetCore.Routing.EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(
                "facturacion/facturas",
                StringComparison.OrdinalIgnoreCase) == true)
            .ToArray();

        Assert.NotEmpty(endpoints);

        // Confirmar y anular son las acciones más sensibles: cada una exige su propia política además
        // de la de solo-consulta de la clase, nunca la política del otro perfil. Confirmar tiene GET y
        // POST (la de contado pide cuenta de efectivo, medio de pago, referencia y categoría).
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

        // Crear, editar encabezado, agregar/editar/quitar línea son de Asistente y Finanzas.
        string[] draftActionSuffixes = ["crear", "encabezado", "agregar", "editar", "quitar"];
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
            endpoint => endpoint.RoutePattern.RawText == "facturacion/facturas");
        Assert.DoesNotContain(
            list.Metadata.GetOrderedMetadata<IAuthorizeData>(),
            data => data.Policy == SystemPolicies.CreateFinancialDrafts);
    }

    private void AssertPolicyRoles(string policyName, params string[] expectedRoles)
    {
        AuthorizationOptions options = factory.Services
            .GetRequiredService<IOptions<AuthorizationOptions>>()
            .Value;
        AuthorizationPolicy policy = options.GetPolicy(policyName)!;
        RolesAuthorizationRequirement requirement = Assert.Single(
            policy.Requirements.OfType<RolesAuthorizationRequirement>());

        Assert.Equal(
            expectedRoles.Order(StringComparer.Ordinal),
            requirement.AllowedRoles.Order(StringComparer.Ordinal));
    }
}
