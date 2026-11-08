using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class BalanceAdjustmentAuthorizationTests(FinancialWebApplicationFactory factory)
    : IClassFixture<FinancialWebApplicationFactory>
{
    [Theory]
    [InlineData(SystemRoles.Management)]
    [InlineData(SystemRoles.Finance)]
    public async Task FinanceAndManagement_CanAdjustFinancialBalances(string role)
    {
        AuthorizationResult result = await AuthorizeAsync(role);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(SystemRoles.Assistant)]
    [InlineData(SystemRoles.Administrator)]
    public async Task AssistantAndAdministrator_CannotAdjustFinancialBalances(string role)
    {
        AuthorizationResult result = await AuthorizeAsync(role);

        Assert.False(result.Succeeded);
    }

    private async Task<AuthorizationResult> AuthorizeAsync(string role)
    {
        ClaimsPrincipal principal = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
        ], "IntegrationTest"));

        IAuthorizationService authorizationService = factory.Services
            .GetRequiredService<IAuthorizationService>();

        return await authorizationService.AuthorizeAsync(
            principal,
            resource: null,
            SystemPolicies.AdjustFinancialBalances);
    }
}
