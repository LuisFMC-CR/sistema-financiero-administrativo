using Microsoft.AspNetCore.Authorization;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.Web.Configuration;

/// <summary>
/// Configura las políticas de autorización acordadas para los perfiles internos.
/// </summary>
/// <remarks>
/// Conexiones: <c>Program.cs</c> llama a <c>AddSystemAuthorization</c> una sola vez al iniciar; es el
/// único lugar donde se decide qué roles (<c>SystemRoles</c>, Application) satisfacen cada política de
/// <c>SystemPolicies</c> (Application). Cada controlador solo referencia el nombre de la política, nunca
/// un rol directamente.
/// </remarks>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Exige autenticación de forma predeterminada y registra permisos por capacidad.
    /// </summary>
    /// <param name="services">Colección de servicios de la aplicación.</param>
    /// <returns>La misma colección para permitir encadenamiento.</returns>
    public static IServiceCollection AddSystemAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(
                SystemPolicies.ManageUsers,
                policy => policy.RequireRole(SystemRoles.Administrator))
            .AddPolicy(
                SystemPolicies.ManageTechnicalConfiguration,
                policy => policy.RequireRole(SystemRoles.Administrator))
            .AddPolicy(
                SystemPolicies.ViewBusinessCatalogs,
                policy => policy.RequireRole(
                    SystemRoles.Management,
                    SystemRoles.Finance,
                    SystemRoles.Assistant))
            .AddPolicy(
                SystemPolicies.ManageBusinessContacts,
                policy => policy.RequireRole(SystemRoles.Management, SystemRoles.Finance, SystemRoles.Assistant))
            .AddPolicy(
                SystemPolicies.ManageBusinessCatalogs,
                policy => policy.RequireRole(SystemRoles.Management, SystemRoles.Finance))
            .AddPolicy(
                SystemPolicies.ViewFinancialTransactions,
                policy => policy.RequireRole(
                    SystemRoles.Management,
                    SystemRoles.Finance,
                    SystemRoles.Assistant))
            .AddPolicy(
                SystemPolicies.CreateFinancialDrafts,
                policy => policy.RequireRole(SystemRoles.Management, SystemRoles.Finance, SystemRoles.Assistant))
            .AddPolicy(
                SystemPolicies.ConfirmFinancialTransactions,
                policy => policy.RequireRole(SystemRoles.Management, SystemRoles.Finance))
            .AddPolicy(
                SystemPolicies.VoidFinancialTransactions,
                policy => policy.RequireRole(SystemRoles.Management))
            .AddPolicy(
                SystemPolicies.AdjustFinancialBalances,
                policy => policy.RequireRole(SystemRoles.Management))
            .AddPolicy(
                SystemPolicies.ViewFinancialReports,
                policy => policy.RequireRole(SystemRoles.Management, SystemRoles.Finance))
            .AddPolicy(
                SystemPolicies.ViewAuditTrail,
                policy => policy.RequireRole(SystemRoles.Management, SystemRoles.Administrator))
            .AddPolicy(
                SystemPolicies.ManageSystemParameters,
                policy => policy.RequireRole(SystemRoles.Management));

        return services;
    }
}
