using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

/// <summary>
/// Paquete v9, bloque 7 (CU-24, CU-25 y CU-26): traspasos, liquidaciones de IVA y pagos de renta son
/// solo de Finanzas y Gerencia. El Asistente recibe 403 en el listado y en el detalle aunque escriba
/// la ruta; el rechazo ocurre antes de llegar a la base de datos.
/// </summary>
public sealed class AssistantRestrictedRoutesTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public AssistantRestrictedRoutesTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Theory]
    [InlineData("/traspasos")]
    [InlineData("/liquidacion-iva")]
    [InlineData("/pagos-renta")]
    [InlineData("/traspasos/{0}")]
    [InlineData("/liquidacion-iva/{0}")]
    [InlineData("/pagos-renta/{0}")]
    public async Task Assistant_IsForbiddenOnListAndDetail(string routeTemplate)
    {
        WebApplicationFactory<Program> roleFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(RoleTestAuthentication.AddTo));
        using HttpClient client = roleFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add(RoleTestAuthentication.RoleHeader, SystemRoles.Assistant);

        using HttpResponseMessage response = await client.GetAsync(string.Format(
            System.Globalization.CultureInfo.InvariantCulture, routeTemplate, Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
