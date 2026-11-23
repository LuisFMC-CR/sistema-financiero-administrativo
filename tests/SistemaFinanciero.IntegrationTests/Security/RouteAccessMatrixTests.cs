using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

/// <summary>
/// Un perfil no entra por la URL a lo que su menú no le ofrece (matriz de perfiles de CLAUDE.md):
/// la autorización se valida siempre en el servidor, no solo ocultando opciones. Cada ruta se pide con
/// cada perfil que no debe entrar y debe responder 403 antes de tocar la base de datos.
/// </summary>
public sealed class RouteAccessMatrixTests : IClassFixture<FinancialWebApplicationFactory>
{
    private static readonly string[] FinancialCatalogRoutes =
    [
        "/catalogos/cuentas-contables",
        "/catalogos/cuentas-contables/crear",
        "/impuestos-y-retenciones",
        "/catalogos/tipos-impuesto",
        "/catalogos/tipos-retencion",
    ];

    private static readonly string[] ManagementOrFinanceRoutes =
    [
        "/traspasos",
        "/liquidacion-iva",
        "/pagos-renta",
        "/notas-de-credito",
        "/presupuesto",
        "/contabilidad/asientos",
        "/reportes",
        "/parametros",
    ];

    private static readonly string[] ManagementOnlyRoutes = ["/auditoria/bitacora-operaciones"];

    private static readonly string[] AdministratorOnlyRoutes =
    [
        "/administracion/usuarios",
        "/administracion/bitacora-seguridad",
    ];

    private static readonly string[] BusinessRoutes =
    [
        "/catalogos/clientes",
        "/catalogos/proveedores",
        "/catalogos/productos-servicios",
        "/facturacion/facturas",
        "/ingresos",
        "/ingresos/abonos/nuevo",
        "/gastos",
        "/cuentas-por-cobrar",
        "/cuentas-por-pagar",
        "/cuentas-por-pagar/pagos/nuevo",
    ];

    private readonly FinancialWebApplicationFactory factory;

    public RouteAccessMatrixTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    public static TheoryData<string, string> DeniedRoutes()
    {
        TheoryData<string, string> data = [];

        foreach (string route in FinancialCatalogRoutes.Concat(ManagementOrFinanceRoutes)
            .Concat(ManagementOnlyRoutes).Concat(AdministratorOnlyRoutes))
        {
            data.Add(SystemRoles.Assistant, route);
        }

        foreach (string route in ManagementOnlyRoutes.Concat(AdministratorOnlyRoutes))
        {
            data.Add(SystemRoles.Finance, route);
        }

        foreach (string route in AdministratorOnlyRoutes)
        {
            data.Add(SystemRoles.Management, route);
        }

        foreach (string route in FinancialCatalogRoutes.Concat(ManagementOrFinanceRoutes)
            .Concat(ManagementOnlyRoutes).Concat(BusinessRoutes))
        {
            data.Add(SystemRoles.Administrator, route);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(DeniedRoutes))]
    public async Task Route_WhenTheProfileIsNotAllowed_IsForbidden(string role, string route)
    {
        using WebApplicationFactory<Program> roleFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(RoleTestAuthentication.AddTo));
        using HttpClient client = roleFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add(RoleTestAuthentication.RoleHeader, role);

        using HttpResponseMessage response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
