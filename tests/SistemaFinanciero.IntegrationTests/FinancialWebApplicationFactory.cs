using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SistemaFinanciero.IntegrationTests;

/// <summary>
/// Hospeda la aplicación en memoria sin depender del registro de eventos de Windows.
/// </summary>
public sealed class FinancialWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            services.AddDataProtection().UseEphemeralDataProtectionProvider());

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
    }
}
