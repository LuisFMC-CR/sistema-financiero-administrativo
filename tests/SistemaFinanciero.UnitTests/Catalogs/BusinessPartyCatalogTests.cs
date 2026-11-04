namespace SistemaFinanciero.UnitTests.Catalogs;

using SistemaFinanciero.Domain.Catalogs;

public sealed class BusinessPartyCatalogTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Customer_Constructor_NormalizesFieldsAndInitializesAudit()
    {
        Guid id = Guid.NewGuid();

        Customer customer = new(
            id,
            " cli-001 ",
            " Cliente de prueba ",
            IdentificationType.LegalEntity,
            " 3101000001 ",
            " cliente@example.test ",
            " 2222-2222 ",
            " San José ",
            500000.005m,
            VatCondition.Normal,
            false,
            "8530.1",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(id, customer.Id);
        Assert.Equal("CLI-001", customer.Code);
        Assert.Equal("Cliente de prueba", customer.Name);
        Assert.Equal(IdentificationType.LegalEntity, customer.IdentificationType);
        Assert.Equal("3101000001", customer.Identification);
        Assert.Equal("cliente@example.test", customer.Email);
        Assert.Equal("2222-2222", customer.Phone);
        Assert.Equal("San José", customer.Address);
        Assert.Equal(500000.01m, customer.CreditLimit);
        Assert.Equal(VatCondition.Normal, customer.VatCondition);
        Assert.False(customer.AppliesStateWithholding);
        Assert.Equal("8530.1", customer.ActivityCode);
        Assert.True(customer.IsActive);
        Assert.Equal(CreatedAtUtc, customer.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, customer.UpdatedAtUtc);
        Assert.Equal(CreatorId, customer.CreatedByUserId);
        Assert.Equal(CreatorId, customer.UpdatedByUserId);
        Assert.Empty(customer.RowVersion);
    }

    [Theory]
    [InlineData(IdentificationType.Individual, "12345678")]
    [InlineData(IdentificationType.Individual, "1234567890")]
    [InlineData(IdentificationType.LegalEntity, "310100000")]
    [InlineData(IdentificationType.Dimex, "1234567890")]
    [InlineData(IdentificationType.Dimex, "12345678901234")]
    [InlineData(IdentificationType.Nite, "12345")]
    public void Customer_Constructor_RejectsAnIdentificationWithTheWrongLengthForItsType(
        IdentificationType type,
        string identification)
    {
        Assert.Throws<ArgumentException>(() => new Customer(
            Guid.NewGuid(),
            "CLI-002",
            "Cliente",
            type,
            identification,
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Customer_Constructor_AllowsFreeTextForAForeignNonResident()
    {
        Customer customer = new(
            Guid.NewGuid(),
            "CLI-003",
            "Cliente extranjero",
            IdentificationType.ForeignNonResident,
            "PASSPORT-AB12345",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
            null,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("PASSPORT-AB12345", customer.Identification);
    }

    [Fact]
    public void Customer_Constructor_RejectsANegativeCreditLimit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Customer(
            Guid.NewGuid(),
            "CLI-004",
            "Cliente",
            IdentificationType.LegalEntity,
            "3101000003",
            null,
            null,
            null,
            -1m,
            VatCondition.Normal,
            false,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Customer_UpdateDetails_AllowsAZeroCreditLimitAndClearingItBackToNull()
    {
        Customer customer = CreateCustomer();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        customer.UpdateDetails(
            customer.Name,
            customer.IdentificationType,
            customer.Identification,
            null,
            null,
            null,
            0m,
            VatCondition.Normal,
            false,
            null,
            changedAtUtc,
            EditorId);

        Assert.Equal(0m, customer.CreditLimit);

        customer.UpdateDetails(
            customer.Name,
            customer.IdentificationType,
            customer.Identification,
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
            null,
            changedAtUtc.AddMinutes(1),
            EditorId);

        Assert.Null(customer.CreditLimit);
    }

    [Fact]
    public void Customer_Constructor_RejectsAnUndefinedVatCondition()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Customer(
            Guid.NewGuid(),
            "CLI-005",
            "Cliente",
            IdentificationType.LegalEntity,
            "3101000004",
            null,
            null,
            null,
            null,
            (VatCondition)99,
            false,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Customer_UpdateDetails_ChangesVatConditionAndStateWithholding()
    {
        Customer customer = CreateCustomer();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        customer.UpdateDetails(
            customer.Name,
            customer.IdentificationType,
            customer.Identification,
            null,
            null,
            null,
            null,
            VatCondition.StateHigherEducationInstitution,
            true,
            null,
            changedAtUtc,
            EditorId);

        Assert.Equal(VatCondition.StateHigherEducationInstitution, customer.VatCondition);
        Assert.True(customer.AppliesStateWithholding);
    }

    [Fact]
    public void CustomerAndSupplier_CanBeDeactivatedIndependently()
    {
        Customer customer = CreateCustomer();
        Supplier supplier = CreateSupplier();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        customer.Deactivate(changedAtUtc, EditorId);

        Assert.False(customer.IsActive);
        Assert.True(supplier.IsActive);
        Assert.Equal(changedAtUtc, customer.UpdatedAtUtc);
        Assert.Equal(EditorId, customer.UpdatedByUserId);
        Assert.Equal(CreatedAtUtc, supplier.UpdatedAtUtc);
    }

    [Fact]
    public void Supplier_UpdateDetails_TrimsValuesAndConvertsBlankOptionalValuesToNull()
    {
        Supplier supplier = CreateSupplier();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(10);

        supplier.UpdateDetails(
            " Proveedor actualizado ",
            IdentificationType.LegalEntity,
            " 3101000002 ",
            null,
            " 8888-8888 ",
            "   ",
            "   ",
            changedAtUtc,
            EditorId);

        Assert.Equal("PROV-001", supplier.Code);
        Assert.Equal("Proveedor actualizado", supplier.Name);
        Assert.Equal(IdentificationType.LegalEntity, supplier.IdentificationType);
        Assert.Equal("3101000002", supplier.Identification);
        Assert.Null(supplier.Email);
        Assert.Equal("8888-8888", supplier.Phone);
        Assert.Null(supplier.Address);
        Assert.Null(supplier.EconomicActivity);
        Assert.Equal(changedAtUtc, supplier.UpdatedAtUtc);
        Assert.Equal(EditorId, supplier.UpdatedByUserId);
    }

    [Fact]
    public void Customer_UpdateDetails_RejectsAnAuditTimestampBeforeThePreviousChange()
    {
        Customer customer = CreateCustomer();
        string originalName = customer.Name;

        Assert.Throws<ArgumentOutOfRangeException>(() => customer.UpdateDetails(
            "Nombre que no debe aplicarse",
            customer.IdentificationType,
            customer.Identification,
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
            null,
            CreatedAtUtc.AddSeconds(-1),
            EditorId));

        Assert.Equal(originalName, customer.Name);
        Assert.Equal(CreatedAtUtc, customer.UpdatedAtUtc);
    }

    [Fact]
    public void Supplier_Constructor_RejectsNonUtcAuditTimestamp()
    {
        DateTimeOffset nonUtcTimestamp = new(2026, 8, 22, 12, 0, 0, TimeSpan.FromHours(-6));

        Assert.Throws<ArgumentException>(() => new Supplier(
            Guid.NewGuid(),
            "PROV-001",
            "Proveedor",
            IdentificationType.LegalEntity,
            "3101000001",
            null,
            null,
            null,
            null,
            nonUtcTimestamp,
            CreatorId));
    }

    private static Customer CreateCustomer()
    {
        return new Customer(
            Guid.NewGuid(),
            "CLI-001",
            "Cliente",
            IdentificationType.LegalEntity,
            "3101000001",
            null,
            null,
            null,
            null,
            VatCondition.Normal,
            false,
            null,
            CreatedAtUtc,
            CreatorId);
    }

    private static Supplier CreateSupplier()
    {
        return new Supplier(
            Guid.NewGuid(),
            "PROV-001",
            "Proveedor",
            IdentificationType.LegalEntity,
            "3101000002",
            null,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
