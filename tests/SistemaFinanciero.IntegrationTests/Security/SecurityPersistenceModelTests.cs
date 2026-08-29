using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Infrastructure.Identity;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class SecurityPersistenceModelTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public SecurityPersistenceModelTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void ApplicationUser_MustChangePassword_IsARequiredBooleanWithSafeDefault()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        IEntityType user = context.Model.FindEntityType(typeof(ApplicationUser))!;
        IProperty property = user.FindProperty(nameof(ApplicationUser.MustChangePassword))!;

        Assert.False(property.IsNullable);
        Assert.Equal("bit", property.GetColumnType());
        Assert.Equal(false, property.GetDefaultValue());
    }

    [Fact]
    public void UserRoleMapping_EnforcesOneRolePerUser()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        IEntityType assignment = context.Model.FindEntityType(typeof(IdentityUserRole<Guid>))!;

        Assert.Contains(
            assignment.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name == nameof(IdentityUserRole<Guid>.UserId));
    }

    [Fact]
    public void SecurityAuditEvent_UsesApprovedTablePropertiesAndChecks()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        IModel designModel = context.GetService<IDesignTimeModel>().Model;
        IEntityType audit = designModel.FindEntityType(typeof(SecurityAuditEvent))!;

        Assert.Equal("seguridad", audit.GetSchema());
        Assert.Equal("EventosSeguridad", audit.GetTableName());
        Assert.Equal(nameof(SecurityAuditEvent.Id), Assert.Single(audit.FindPrimaryKey()!.Properties).Name);
        Assert.False(audit.FindProperty(nameof(SecurityAuditEvent.Action))!.IsNullable);
        Assert.False(audit.FindProperty(nameof(SecurityAuditEvent.OccurredAtUtc))!.IsNullable);
        Assert.True(audit.FindProperty(nameof(SecurityAuditEvent.ActorUserId))!.IsNullable);
        Assert.False(audit.FindProperty(nameof(SecurityAuditEvent.TargetUserId))!.IsNullable);
        Assert.Equal(32, audit.FindProperty(nameof(SecurityAuditEvent.Role))!.GetMaxLength());

        string[] checkNames = audit.GetCheckConstraints()
            .Select(constraint => constraint.Name ?? string.Empty)
            .ToArray();
        Assert.Contains("CK_EventosSeguridad_Action", checkNames);
        Assert.Contains("CK_EventosSeguridad_Role", checkNames);
    }

    [Fact]
    public void SecurityAuditEvent_UserRelationships_NeverCascadeDelete()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        IEntityType audit = context.Model.FindEntityType(typeof(SecurityAuditEvent))!;
        IForeignKey[] foreignKeys = audit.GetForeignKeys().ToArray();

        Assert.Equal(2, foreignKeys.Length);
        Assert.All(
            foreignKeys,
            foreignKey =>
            {
                Assert.Equal(typeof(ApplicationUser), foreignKey.PrincipalEntityType.ClrType);
                Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
            });
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.Properties.Single().Name ==
                nameof(SecurityAuditEvent.ActorUserId));
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.Properties.Single().Name ==
                nameof(SecurityAuditEvent.TargetUserId));
    }

    [Theory]
    [InlineData(EntityState.Modified)]
    [InlineData(EntityState.Deleted)]
    public void SecurityAuditEvent_RejectsMutationBeforeDatabaseAccess(EntityState state)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();
        SecurityAuditEvent auditEvent = new(
            Guid.NewGuid(),
            SecurityAuditAction.UserCreated,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            SystemRoles.Assistant);
        context.Attach(auditEvent);
        context.Entry(auditEvent).State = state;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => context.SaveChanges());

        Assert.Contains("inmutable", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
