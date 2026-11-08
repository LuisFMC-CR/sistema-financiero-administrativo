using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class AssistantFinancialAccessTests(FinancialWebApplicationFactory factory)
    : IClassFixture<FinancialWebApplicationFactory>
{
    [Theory]
    [InlineData(SystemPolicies.ViewFinancialTransactions)]
    [InlineData(SystemPolicies.CreateFinancialDrafts)]
    [InlineData(SystemPolicies.RegisterManualIncome)]
    [InlineData(SystemPolicies.ViewReceivablesAndPayables)]
    public async Task Assistant_IsAllowedForRequestedFinancialCapabilities(string policyName)
    {
        AuthorizationResult result = await AuthorizeAssistantAsync(policyName);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(SystemPolicies.ConfirmFinancialTransactions)]
    [InlineData(SystemPolicies.VoidFinancialTransactions)]
    [InlineData(SystemPolicies.AdjustFinancialBalances)]
    [InlineData(SystemPolicies.ViewFinancialReports)]
    public async Task Assistant_IsDeniedSensitiveFinancialCapabilities(string policyName)
    {
        AuthorizationResult result = await AuthorizeAssistantAsync(policyName);

        Assert.False(result.Succeeded);
    }

    private async Task<AuthorizationResult> AuthorizeAssistantAsync(string policyName)
    {
        ClaimsPrincipal assistant = new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, SystemRoles.Assistant),
        ], "IntegrationTest"));

        IAuthorizationService authorizationService = factory.Services
            .GetRequiredService<IAuthorizationService>();

        return await authorizationService.AuthorizeAsync(assistant, resource: null, policyName);
    }
}
