using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.UnitTests.Catalogs;

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
            " 3-101-000001 ",
            " cliente@example.test ",
            " 2222-2222 ",
            " San José ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal(id, customer.Id);
        Assert.Equal("CLI-001", customer.Code);
        Assert.Equal("Cliente de prueba", customer.Name);
        Assert.Equal("3-101-000001", customer.Identification);
        Assert.Equal("cliente@example.test", customer.Email);
        Assert.Equal("2222-2222", customer.Phone);
        Assert.Equal("San José", customer.Address);
        Assert.True(customer.IsActive);
        Assert.Equal(CreatedAtUtc, customer.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, customer.UpdatedAtUtc);
        Assert.Equal(CreatorId, customer.CreatedByUserId);
        Assert.Equal(CreatorId, customer.UpdatedByUserId);
        Assert.Empty(customer.RowVersion);
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
            " prov-002 ",
            " Proveedor actualizado ",
            " ",
            null,
            " 8888-8888 ",
            "   ",
            changedAtUtc,
            EditorId);

        Assert.Equal("PROV-002", supplier.Code);
        Assert.Equal("Proveedor actualizado", supplier.Name);
        Assert.Null(supplier.Identification);
        Assert.Null(supplier.Email);
        Assert.Equal("8888-8888", supplier.Phone);
        Assert.Null(supplier.Address);
        Assert.Equal(changedAtUtc, supplier.UpdatedAtUtc);
        Assert.Equal(EditorId, supplier.UpdatedByUserId);
    }

    [Fact]
    public void Customer_UpdateDetails_RejectsAnAuditTimestampBeforeThePreviousChange()
    {
        Customer customer = CreateCustomer();
        string originalName = customer.Name;

        Assert.Throws<ArgumentOutOfRangeException>(() => customer.UpdateDetails(
            "CLI-002",
            "Nombre que no debe aplicarse",
            null,
            null,
            null,
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
            null,
            null,
            null,
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
            null,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
