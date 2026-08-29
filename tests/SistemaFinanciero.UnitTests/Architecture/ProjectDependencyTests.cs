using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.UnitTests.Architecture;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void Domain_DoesNotReferenceOtherSolutionProjects()
    {
        string[] solutionReferences = typeof(ExchangeRate)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("SistemaFinanciero.", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(solutionReferences);
    }

    [Fact]
    public void Application_DoesNotReferenceSolutionProjectsOtherThanDomain()
    {
        string[] solutionReferences = typeof(SystemRoles)
            .Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => name.StartsWith("SistemaFinanciero.", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.All(
            solutionReferences,
            reference => Assert.Equal("SistemaFinanciero.Domain", reference));
    }

    [Fact]
    public void SystemRoles_AreUniqueAndMatchApprovedProfiles()
    {
        string[] expectedRoles =
        [
            "Administrador",
            "Gerencia",
            "Finanzas",
            "Asistente",
        ];

        Assert.Equal(expectedRoles, SystemRoles.All);
        Assert.Equal(SystemRoles.All.Count, SystemRoles.All.Distinct(StringComparer.Ordinal).Count());
    }
}
