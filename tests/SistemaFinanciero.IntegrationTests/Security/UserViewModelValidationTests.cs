using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SistemaFinanciero.Application.Security;
using SistemaFinanciero.Web.Models.Users;

namespace SistemaFinanciero.IntegrationTests.Security;

public sealed class UserViewModelValidationTests
{
    [Fact]
    public void CreateUser_AcceptsACompleteInternalAccount()
    {
        UserCreateViewModel model = new()
        {
            FullName = "Ana Administrativa",
            Email = "ana@empresa.example",
            Role = SystemRoles.Assistant,
            TemporaryPassword = "Temporal-2026!",
            ConfirmPassword = "Temporal-2026!",
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void CreateUser_RejectsOversizedIdentityFieldsAndInvalidEmail()
    {
        UserCreateViewModel model = new()
        {
            FullName = new string('N', 151),
            Email = new string('x', 245) + "-sin-formato",
            Role = new string('R', 51),
            TemporaryPassword = "Temporal-2026!",
            ConfirmPassword = "Temporal-2026!",
        };

        List<ValidationResult> results = Validate(model);

        AssertInvalid(results, nameof(model.FullName));
        AssertInvalid(results, nameof(model.Email));
        AssertInvalid(results, nameof(model.Role));
    }

    [Fact]
    public void CreateUser_RejectsShortOrMismatchedTemporaryPassword()
    {
        UserCreateViewModel model = new()
        {
            FullName = "Usuario de prueba",
            Email = "usuario@empresa.example",
            Role = SystemRoles.Finance,
            TemporaryPassword = "Corta1!",
            ConfirmPassword = "Otra-clave-2026!",
        };

        List<ValidationResult> results = Validate(model);

        AssertInvalid(results, nameof(model.TemporaryPassword));
        AssertInvalid(results, nameof(model.ConfirmPassword));
    }

    [Fact]
    public void EditAndStateOperations_RequireAnOpaqueVersion()
    {
        UserEditViewModel edit = new()
        {
            FullName = "Usuario vigente",
            Email = "vigente@empresa.example",
            Role = SystemRoles.Management,
            Version = string.Empty,
        };
        UserStatusViewModel status = new()
        {
            IsActive = null,
            Version = string.Empty,
        };
        UserVersionViewModel version = new();

        AssertInvalid(Validate(edit), nameof(edit.Version));
        AssertInvalid(Validate(status), nameof(status.IsActive));
        AssertInvalid(Validate(status), nameof(status.Version));
        AssertInvalid(Validate(version), nameof(version.Version));
    }

    [Fact]
    public void ResetPassword_RejectsMismatchAndCanClearSensitiveValues()
    {
        UserResetPasswordViewModel model = new()
        {
            TargetFullName = "Cuenta objetivo",
            TargetEmail = "objetivo@empresa.example",
            TemporaryPassword = "Temporal-2026!",
            ConfirmPassword = "Diferente-2026!",
            Version = "version-opaca",
        };

        AssertInvalid(Validate(model), nameof(model.ConfirmPassword));

        model.ClearPasswords();

        Assert.Empty(model.TemporaryPassword);
        Assert.Empty(model.ConfirmPassword);
    }

    [Theory]
    [InlineData(nameof(UserResetPasswordViewModel.TargetFullName))]
    [InlineData(nameof(UserResetPasswordViewModel.TargetEmail))]
    public void ResetPassword_DisplayFields_AreNeverBoundFromTheRequest(string propertyName)
    {
        PropertyInfo property = typeof(UserResetPasswordViewModel).GetProperty(propertyName)!;

        Assert.NotNull(property.GetCustomAttribute<BindNeverAttribute>());
    }

    private static void AssertInvalid(
        IEnumerable<ValidationResult> results,
        string propertyName)
    {
        Assert.Contains(
            results,
            result => result.MemberNames.Contains(propertyName, StringComparer.Ordinal));
    }

    private static List<ValidationResult> Validate(object model)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(
            model,
            new ValidationContext(model),
            results,
            validateAllProperties: true);
        return results;
    }
}
