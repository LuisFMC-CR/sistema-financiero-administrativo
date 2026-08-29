using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class FinancialCategoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesFieldsAndKeepsKind()
    {
        Guid parentId = Guid.NewGuid();

        FinancialCategory category = new(
            Guid.NewGuid(),
            " ing-serv ",
            " Servicios ",
            FinancialCategoryKind.Income,
            parentId,
            " Ingresos por soporte ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("ING-SERV", category.Code);
        Assert.Equal("Servicios", category.Name);
        Assert.Equal(FinancialCategoryKind.Income, category.Kind);
        Assert.Equal(parentId, category.ParentId);
        Assert.Equal("Ingresos por soporte", category.Description);
        Assert.True(category.IsActive);
    }

    [Fact]
    public void Constructor_RejectsTheCategoryItselfAsParent()
    {
        Guid id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new FinancialCategory(
            id,
            "ING-001",
            "Ingresos",
            FinancialCategoryKind.Income,
            id,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void ChangeParent_UpdatesOnlyParentAndAudit()
    {
        FinancialCategory category = CreateCategory();
        Guid parentId = Guid.NewGuid();
        FinancialCategoryKind originalKind = category.Kind;
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        category.ChangeParent(parentId, changedAtUtc, EditorId);

        Assert.Equal(parentId, category.ParentId);
        Assert.Equal(originalKind, category.Kind);
        Assert.Equal(changedAtUtc, category.UpdatedAtUtc);
        Assert.Equal(EditorId, category.UpdatedByUserId);
    }

    [Fact]
    public void UpdateDetails_DoesNotExposeKindOrParentAsEditableData()
    {
        FinancialCategory category = CreateCategory();
        FinancialCategoryKind originalKind = category.Kind;
        Guid? originalParentId = category.ParentId;

        category.UpdateDetails(
            " gas-op ",
            " Gastos operativos ",
            " ",
            CreatedAtUtc.AddMinutes(5),
            EditorId);

        Assert.Equal("GAS-OP", category.Code);
        Assert.Equal("Gastos operativos", category.Name);
        Assert.Null(category.Description);
        Assert.Equal(originalKind, category.Kind);
        Assert.Equal(originalParentId, category.ParentId);
    }

    [Fact]
    public void Constructor_RejectsUnknownKind()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FinancialCategory(
            Guid.NewGuid(),
            "CAT-001",
            "Categoría",
            (FinancialCategoryKind)99,
            null,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    private static FinancialCategory CreateCategory()
    {
        return new FinancialCategory(
            Guid.NewGuid(),
            "GAS-001",
            "Gastos",
            FinancialCategoryKind.Expense,
            null,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
