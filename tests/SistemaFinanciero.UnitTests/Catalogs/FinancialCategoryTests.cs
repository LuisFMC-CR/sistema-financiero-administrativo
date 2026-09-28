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
        Guid ledgerAccountId = Guid.NewGuid();

        FinancialCategory category = new(
            Guid.NewGuid(),
            " ing-serv ",
            " Servicios ",
            FinancialCategoryKind.Income,
            parentId,
            ledgerAccountId,
            " Ingresos por soporte ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("ING-SERV", category.Code);
        Assert.Equal("Servicios", category.Name);
        Assert.Equal(FinancialCategoryKind.Income, category.Kind);
        Assert.Equal(parentId, category.ParentId);
        Assert.Equal(ledgerAccountId, category.LedgerAccountId);
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
            Guid.NewGuid(),
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RequiresALedgerAccount()
    {
        Assert.Throws<ArgumentException>(() => new FinancialCategory(
            Guid.NewGuid(),
            "ING-001",
            "Ingresos",
            FinancialCategoryKind.Income,
            null,
            Guid.Empty,
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
        Guid originalLedgerAccountId = category.LedgerAccountId;
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        category.ChangeParent(parentId, changedAtUtc, EditorId);

        Assert.Equal(parentId, category.ParentId);
        Assert.Equal(originalKind, category.Kind);
        Assert.Equal(originalLedgerAccountId, category.LedgerAccountId);
        Assert.Equal(changedAtUtc, category.UpdatedAtUtc);
        Assert.Equal(EditorId, category.UpdatedByUserId);
    }

    [Fact]
    public void ChangeLedgerAccount_UpdatesOnlyTheAccountAndAudit()
    {
        FinancialCategory category = CreateCategory();
        Guid newAccountId = Guid.NewGuid();
        Guid? originalParentId = category.ParentId;
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        category.ChangeLedgerAccount(newAccountId, changedAtUtc, EditorId);

        Assert.Equal(newAccountId, category.LedgerAccountId);
        Assert.Equal(originalParentId, category.ParentId);
        Assert.Equal(FinancialCategoryKind.Expense, category.Kind);
        Assert.Equal(changedAtUtc, category.UpdatedAtUtc);
        Assert.Equal(EditorId, category.UpdatedByUserId);
    }

    [Fact]
    public void ChangeLedgerAccount_WithTheSameAccountChangesNothing()
    {
        FinancialCategory category = CreateCategory();

        category.ChangeLedgerAccount(category.LedgerAccountId, CreatedAtUtc.AddMinutes(5), EditorId);

        Assert.Equal(CreatedAtUtc, category.UpdatedAtUtc);
        Assert.Equal(CreatorId, category.UpdatedByUserId);
    }

    [Fact]
    public void ChangeLedgerAccount_RejectsAnEmptyAccountAndLeavesTheCategoryUntouched()
    {
        FinancialCategory category = CreateCategory();
        Guid originalLedgerAccountId = category.LedgerAccountId;

        Assert.Throws<ArgumentException>(() =>
            category.ChangeLedgerAccount(Guid.Empty, CreatedAtUtc.AddMinutes(5), EditorId));

        Assert.Equal(originalLedgerAccountId, category.LedgerAccountId);
        Assert.Equal(CreatedAtUtc, category.UpdatedAtUtc);
    }

    [Fact]
    public void ChangeLedgerAccount_RejectsAnEmptyUserOrAnInstantBeforeTheLastChange()
    {
        FinancialCategory category = CreateCategory();
        Guid originalLedgerAccountId = category.LedgerAccountId;

        Assert.Throws<ArgumentException>(() =>
            category.ChangeLedgerAccount(Guid.NewGuid(), CreatedAtUtc.AddMinutes(5), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            category.ChangeLedgerAccount(Guid.NewGuid(), CreatedAtUtc.AddMinutes(-5), EditorId));

        Assert.Equal(originalLedgerAccountId, category.LedgerAccountId);
    }

    [Fact]
    public void UpdateDetails_DoesNotExposeKindParentOrLedgerAccountAsEditableData()
    {
        FinancialCategory category = CreateCategory();
        FinancialCategoryKind originalKind = category.Kind;
        Guid? originalParentId = category.ParentId;
        Guid originalLedgerAccountId = category.LedgerAccountId;

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
        Assert.Equal(originalLedgerAccountId, category.LedgerAccountId);
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
            Guid.NewGuid(),
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Theory]
    [InlineData(FinancialCategoryKind.Income, LedgerAccountType.Income)]
    [InlineData(FinancialCategoryKind.Expense, LedgerAccountType.Expense)]
    public void Kind_MapsToTheLedgerAccountTypeOfTheSameNature(
        FinancialCategoryKind kind,
        LedgerAccountType expectedType)
    {
        Assert.Equal(expectedType, kind.ToLedgerAccountType());
    }

    [Fact]
    public void Kind_RejectsAnUndefinedNatureWhenMappingToAnAccountType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ((FinancialCategoryKind)99).ToLedgerAccountType());
    }

    private static FinancialCategory CreateCategory()
    {
        return new FinancialCategory(
            Guid.NewGuid(),
            "GAS-001",
            "Gastos",
            FinancialCategoryKind.Expense,
            null,
            Guid.NewGuid(),
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
