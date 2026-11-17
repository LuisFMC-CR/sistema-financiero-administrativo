using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SistemaFinanciero.Application.Catalogs.Common;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Audit;

namespace SistemaFinanciero.IntegrationTests.Security;

/// <summary>
/// ADR-0060: el inicio del Administrador se titula «Administración del sistema» y muestra los últimos
/// cinco registros de la bitácora de seguridad; un perfil sin <c>ManageUsers</c> no los ve ni dispara
/// la consulta. La bitácora se sustituye por una en memoria, sin base de datos.
/// </summary>
public sealed class AdministratorHomeTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public AdministratorHomeTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Administrator_SeesTheAdministrationTitleAndOnlyTheFiveMostRecentSecurityEvents()
    {
        FakeSecurityAuditService audit = new(entryCount: 7);
        using HttpClient client = CreateClient(audit, SystemRoles.Administrator);

        using HttpResponseMessage response = await client.GetAsync("/");
        string html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Administración del sistema", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Control financiero contable", html, StringComparison.Ordinal);
        Assert.Contains("Últimos registros de seguridad", html, StringComparison.Ordinal);
        Assert.Contains("/administracion/bitacora-seguridad", html, StringComparison.Ordinal);
        for (int index = 1; index <= 5; index++)
        {
            Assert.Contains($"Cuenta objetivo {index}<", html, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("Cuenta objetivo 6<", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Cuenta objetivo 7<", html, StringComparison.Ordinal);
        Assert.Equal(1, audit.Calls);
    }

    [Fact]
    public async Task Administrator_WithAnEmptySecurityLog_SeesTheEmptyMessage()
    {
        FakeSecurityAuditService audit = new(entryCount: 0);
        using HttpClient client = CreateClient(audit, SystemRoles.Administrator);

        string html = await client.GetStringAsync("/");

        Assert.Contains("No hay registros en la bitácora de seguridad.", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProfileWithoutManageUsers_KeepsTheFinancialTitleAndDoesNotQueryTheSecurityLog()
    {
        FakeSecurityAuditService audit = new(entryCount: 7);
        using HttpClient client = CreateClient(audit, role: "SinPerfil");

        string html = await client.GetStringAsync("/");

        Assert.Contains("Control financiero contable", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Últimos registros de seguridad", html, StringComparison.Ordinal);
        Assert.Equal(0, audit.Calls);
    }

    private HttpClient CreateClient(FakeSecurityAuditService audit, string role)
    {
        WebApplicationFactory<Program> roleFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                RoleTestAuthentication.AddTo(services);
                services.RemoveAll<ISecurityAuditService>();
                services.AddSingleton<ISecurityAuditService>(audit);
            }));
        HttpClient client = roleFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
        client.DefaultRequestHeaders.Add(RoleTestAuthentication.RoleHeader, role);
        return client;
    }

    /// <summary>Bitácora en memoria, de la más reciente a la más antigua, como la real.</summary>
    private sealed class FakeSecurityAuditService(int entryCount) : ISecurityAuditService
    {
        public int Calls { get; private set; }

        public Task<PagedResult<SecurityAuditEntryModel>> SearchAsync(
            SecurityAuditQuery query,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            DateTimeOffset now = new(2026, 11, 16, 15, 0, 0, TimeSpan.Zero);
            List<SecurityAuditEntryModel> entries = Enumerable.Range(1, entryCount)
                .Select(index => new SecurityAuditEntryModel(
                    Guid.NewGuid(),
                    SecurityAuditAction.UserCreated,
                    "Administrador de prueba",
                    $"Cuenta objetivo {index}",
                    SystemRoles.Finance,
                    now.AddMinutes(-index)))
                .ToList();
            return Task.FromResult(new PagedResult<SecurityAuditEntryModel>(entries, query.Page, 20, entryCount));
        }
    }
}
