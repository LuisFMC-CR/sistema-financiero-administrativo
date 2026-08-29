using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class SecurityAuditEventTests
{
    [Fact]
    public void Contract_DoesNotExposeCredentialOrTokenFields()
    {
        string[] propertyNames = typeof(SecurityAuditEvent)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("Password", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Stamp", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Constructor_AllowsTechnicalBootstrapWithoutActor()
    {
        Guid targetId = Guid.NewGuid();
        DateTimeOffset occurredAtUtc = new(2026, 8, 22, 18, 0, 0, TimeSpan.Zero);

        SecurityAuditEvent auditEvent = new(
            Guid.NewGuid(),
            SecurityAuditAction.BootstrapAdministratorCreated,
            actorUserId: null,
            targetId,
            occurredAtUtc,
            SystemRoles.Administrator);

        Assert.Null(auditEvent.ActorUserId);
        Assert.Equal(targetId, auditEvent.TargetUserId);
        Assert.Equal(occurredAtUtc, auditEvent.OccurredAtUtc);
        Assert.Equal(SystemRoles.Administrator, auditEvent.Role);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void Constructor_RejectsUnknownAction(int rawAction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SecurityAuditEvent(
            Guid.NewGuid(),
            (SecurityAuditAction)rawAction,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RejectsEmptyIdentifiers()
    {
        Assert.Throws<ArgumentException>(() => new SecurityAuditEvent(
            Guid.Empty,
            SecurityAuditAction.UserCreated,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new SecurityAuditEvent(
            Guid.NewGuid(),
            SecurityAuditAction.UserCreated,
            Guid.Empty,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => new SecurityAuditEvent(
            Guid.NewGuid(),
            SecurityAuditAction.UserCreated,
            Guid.NewGuid(),
            Guid.Empty,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Constructor_RequiresUtcAndAnApprovedRole()
    {
        DateTimeOffset nonUtc = new(2026, 8, 22, 12, 0, 0, TimeSpan.FromHours(-6));

        Assert.Throws<ArgumentException>(() => new SecurityAuditEvent(
            Guid.NewGuid(),
            SecurityAuditAction.UserRoleChanged,
            Guid.NewGuid(),
            Guid.NewGuid(),
            nonUtc,
            SystemRoles.Finance));
        Assert.Throws<ArgumentException>(() => new SecurityAuditEvent(
            Guid.NewGuid(),
            SecurityAuditAction.UserRoleChanged,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "Superusuario"));
    }
}
