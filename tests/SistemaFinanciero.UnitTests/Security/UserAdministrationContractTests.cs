using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Application.Security.Users;

namespace SistemaFinanciero.UnitTests.Security;

public sealed class UserAdministrationContractTests
{
    [Fact]
    public void UserQuery_NormalizesTextAndBoundsPaging()
    {
        UserQuery query = new(
            search: "  persona@empresa.example  ",
            status: UserStatusFilter.All,
            role: "  Finanzas  ",
            page: 0,
            pageSize: 500);

        Assert.Equal("persona@empresa.example", query.Search);
        Assert.Equal(UserStatusFilter.All, query.Status);
        Assert.Equal("Finanzas", query.Role);
        Assert.Equal(1, query.Page);
        Assert.Equal(100, query.PageSize);
    }

    [Fact]
    public void UserQuery_TreatsWhitespaceAsNoFilterAndEnforcesMinimumPageSize()
    {
        UserQuery query = new(search: "   ", role: "\t", pageSize: 0);

        Assert.Null(query.Search);
        Assert.Null(query.Role);
        Assert.Equal(UserStatusFilter.Active, query.Status);
        Assert.Equal(1, query.PageSize);
    }

    [Fact]
    public void UserContracts_RepresentExactlyOneRolePerAccount()
    {
        Assert.Equal(typeof(string), typeof(UserModel).GetProperty(nameof(UserModel.Role))?.PropertyType);
        Assert.Equal(
            typeof(string),
            typeof(CreateUserCommand).GetProperty(nameof(CreateUserCommand.Role))?.PropertyType);
        Assert.Equal(
            typeof(string),
            typeof(UpdateUserCommand).GetProperty(nameof(UpdateUserCommand.Role))?.PropertyType);

        Assert.DoesNotContain(
            typeof(UserModel).GetProperties(),
            property => property.Name.Contains("Roles", StringComparison.Ordinal));
    }

    [Fact]
    public void UserModel_DoesNotExposeIdentitySecrets()
    {
        string[] propertyNames = typeof(UserModel)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("PasswordHash", propertyNames);
        Assert.DoesNotContain("SecurityStamp", propertyNames);
        Assert.DoesNotContain("ConcurrencyStamp", propertyNames);
        Assert.DoesNotContain("Token", propertyNames);
        Assert.Contains(nameof(UserModel.Version), propertyNames);
    }

    [Fact]
    public void SecurityAuditActions_HaveStableUniquePersistedValues()
    {
        SecurityAuditAction[] actions = Enum.GetValues<SecurityAuditAction>();

        Assert.Equal(9, actions.Length);
        Assert.Equal(actions.Length, actions.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, actions.Length), actions.Select(action => (int)action));
    }

    [Fact]
    public void MustChangePasswordClaim_UsesAStableApplicationOwnedType()
    {
        Assert.Equal(
            "SistemaFinanciero.MustChangePassword",
            SystemClaimTypes.MustChangePassword);
    }

    [Theory]
    [InlineData(UserOperationStatus.Success, true)]
    [InlineData(UserOperationStatus.NotFound, false)]
    [InlineData(UserOperationStatus.Invalid, false)]
    [InlineData(UserOperationStatus.Duplicate, false)]
    [InlineData(UserOperationStatus.ConcurrencyConflict, false)]
    [InlineData(UserOperationStatus.ProtectedAction, false)]
    public void OperationResult_IsSuccessfulOnlyForSuccess(
        UserOperationStatus status,
        bool expected)
    {
        UserOperationResult result = new(status);

        Assert.Equal(expected, result.IsSuccess);
    }
}
