using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class LedgerAccountTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesFieldsAndCreatesAnActiveAccount()
    {
        Guid parentId = Guid.NewGuid();

        LedgerAccount account = new(
            Guid.NewGuid(),
            " 4-serv ",
            " Ingresos por servicios ",
            LedgerAccountType.Income,
            parentId,
            cashKind: null,
            currency: null,
            reference: " ",
            description: " Servicios profesionales ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("4-SERV", account.Code);
        Assert.Equal("Ingresos por servicios", account.Name);
        Assert.Equal(LedgerAccountType.Income, account.Type);
        Assert.Equal(parentId, account.ParentId);
        Assert.False(account.IsCash);
        Assert.Null(account.CashKind);
        Assert.Null(account.Currency);
        Assert.Null(account.Reference);
        Assert.Equal("Servicios profesionales", account.Description);
        Assert.True(account.IsActive);
        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(CreatorId, account.UpdatedByUserId);
    }

    [Theory]
    [InlineData(LedgerAccountType.Asset)]
    [InlineData(LedgerAccountType.Liability)]
    [InlineData(LedgerAccountType.Equity)]
    [InlineData(LedgerAccountType.Income)]
    [InlineData(LedgerAccountType.Expense)]
    public void Constructor_AcceptsEveryAccountTypeWhenItIsNotACashAccount(LedgerAccountType type)
    {
        LedgerAccount account = CreateAccount(type);

        Assert.Equal(type, account.Type);
        Assert.False(account.IsCash);
    }

    [Theory]
    [InlineData(CashAccountKind.Cash, CurrencyCode.CRC)]
    [InlineData(CashAccountKind.Bank, CurrencyCode.USD)]
    public void Constructor_CreatesACashAccountWithItsKindAndFixedCurrency(
        CashAccountKind kind,
        CurrencyCode currency)
    {
        LedgerAccount account = new(
            Guid.NewGuid(),
            "1-BCO",
            "Banco",
            LedgerAccountType.Asset,
            parentId: null,
            kind,
            currency,
            reference: " Terminada en 1234 ",
            description: null,
            CreatedAtUtc,
            CreatorId);

        Assert.True(account.IsCash);
        Assert.Equal(kind, account.CashKind);
        Assert.Equal(currency, account.Currency);
        Assert.Equal("Terminada en 1234", account.Reference);
    }

    [Theory]
    [InlineData(LedgerAccountType.Liability)]
    [InlineData(LedgerAccountType.Equity)]
    [InlineData(LedgerAccountType.Income)]
    [InlineData(LedgerAccountType.Expense)]
    public void Constructor_RejectsACashAccountThatIsNotAnAsset(LedgerAccountType type)
    {
        Assert.Throws<ArgumentException>(() => CreateAccount(type, CashAccountKind.Cash, CurrencyCode.CRC));
    }

    [Fact]
    public void Constructor_RejectsACashAccountWithoutCurrency()
    {
        Assert.Throws<ArgumentException>(() =>
            CreateAccount(LedgerAccountType.Asset, CashAccountKind.Bank, currency: null));
    }

    [Theory]
    [InlineData(CurrencyCode.CRC)]
    [InlineData(CurrencyCode.USD)]
    public void Constructor_RejectsACurrencyOnAnAccountThatIsNotCash(CurrencyCode currency)
    {
        Assert.Throws<ArgumentException>(() =>
            CreateAccount(LedgerAccountType.Asset, cashKind: null, currency));
    }

    [Fact]
    public void Constructor_RejectsUndefinedTypeKindOrCurrency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateAccount((LedgerAccountType)99));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAccount(LedgerAccountType.Asset, (CashAccountKind)99, CurrencyCode.CRC));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateAccount(LedgerAccountType.Asset, CashAccountKind.Cash, (CurrencyCode)99));
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("   ", "Nombre")]
    [InlineData("COD", "")]
    [InlineData("COD", "   ")]
    public void Constructor_RejectsAMissingCodeOrName(string code, string name)
    {
        Assert.Throws<ArgumentException>(() => new LedgerAccount(
            Guid.NewGuid(),
            code,
            name,
            LedgerAccountType.Asset,
            null,
            null,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsAnEmptyParentIdentifier()
    {
        Assert.Throws<ArgumentException>(() => new LedgerAccount(
            Guid.NewGuid(),
            "COD",
            "Nombre",
            LedgerAccountType.Asset,
            Guid.Empty,
            null,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsAnAccountThatIsItsOwnParent()
    {
        Guid id = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => new LedgerAccount(
            id,
            "COD",
            "Nombre",
            LedgerAccountType.Asset,
            id,
            null,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void DomainModel_DoesNotStoreAnEditableBalance()
    {
        Assert.DoesNotContain(
            typeof(LedgerAccount).GetProperties(),
            property => property.Name.Contains("Balance", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UpdateDetails_ChangesTheEditableDataAndKeepsTypeKindAndCurrency()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Asset, CashAccountKind.Bank, CurrencyCode.USD);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        account.UpdateDetails(" bco-2 ", " Banco dos ", " ", " Cuenta corriente ", changedAtUtc, EditorId);

        Assert.Equal("BCO-2", account.Code);
        Assert.Equal("Banco dos", account.Name);
        Assert.Null(account.Reference);
        Assert.Equal("Cuenta corriente", account.Description);
        Assert.Equal(LedgerAccountType.Asset, account.Type);
        Assert.Equal(CashAccountKind.Bank, account.CashKind);
        Assert.Equal(CurrencyCode.USD, account.Currency);
        Assert.Equal(changedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(EditorId, account.UpdatedByUserId);
        Assert.Equal(CreatorId, account.CreatedByUserId);
    }

    [Fact]
    public void UpdateDetails_LeavesTheAccountUntouchedWhenAnyValueIsInvalid()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Income);

        Assert.Throws<ArgumentException>(() =>
            account.UpdateDetails("NUEVO", "   ", "Ref", "Desc", CreatedAtUtc.AddMinutes(1), EditorId));

        Assert.Equal("COD", account.Code);
        Assert.Equal("Cuenta", account.Name);
        Assert.Null(account.Reference);
        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(CreatorId, account.UpdatedByUserId);
    }

    [Fact]
    public void ChangeParent_AssignsTheNewParentAndUpdatesTheAudit()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Expense);
        Guid parentId = Guid.NewGuid();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        account.ChangeParent(parentId, changedAtUtc, EditorId);

        Assert.Equal(parentId, account.ParentId);
        Assert.Equal(changedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(EditorId, account.UpdatedByUserId);
    }

    [Fact]
    public void ChangeParent_CanMakeTheAccountARootAgain()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Expense, parentId: Guid.NewGuid());

        account.ChangeParent(null, CreatedAtUtc.AddMinutes(5), EditorId);

        Assert.Null(account.ParentId);
    }

    [Fact]
    public void ChangeParent_WithTheSameParentChangesNothing()
    {
        Guid parentId = Guid.NewGuid();
        LedgerAccount account = CreateAccount(LedgerAccountType.Expense, parentId: parentId);

        account.ChangeParent(parentId, CreatedAtUtc.AddMinutes(5), EditorId);

        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(CreatorId, account.UpdatedByUserId);
    }

    [Fact]
    public void ChangeParent_RejectsTheAccountItself()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Expense);

        Assert.Throws<ArgumentException>(() =>
            account.ChangeParent(account.Id, CreatedAtUtc.AddMinutes(5), EditorId));
    }

    [Fact]
    public void Deactivate_KeepsTheAccountAndUpdatesTheAudit()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Asset);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        account.Deactivate(changedAtUtc, EditorId);

        Assert.False(account.IsActive);
        Assert.Equal(changedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(EditorId, account.UpdatedByUserId);
    }

    [Fact]
    public void Activate_RestoresTheAccountAndSettingTheSameStateChangesNothing()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Asset);

        account.Activate(CreatedAtUtc.AddMinutes(1), EditorId);
        Assert.Equal(CreatedAtUtc, account.UpdatedAtUtc);

        account.Deactivate(CreatedAtUtc.AddMinutes(2), EditorId);
        account.Activate(CreatedAtUtc.AddMinutes(3), CreatorId);

        Assert.True(account.IsActive);
        Assert.Equal(CreatedAtUtc.AddMinutes(3), account.UpdatedAtUtc);
    }

    [Fact]
    public void Updates_RejectAnEmptyUserOrAnInstantBeforeTheLastChange()
    {
        LedgerAccount account = CreateAccount(LedgerAccountType.Asset);

        Assert.Throws<ArgumentException>(() =>
            account.Deactivate(CreatedAtUtc.AddMinutes(1), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            account.Deactivate(CreatedAtUtc.AddMinutes(-1), EditorId));
        Assert.True(account.IsActive);
    }

    private static LedgerAccount CreateAccount(
        LedgerAccountType type,
        CashAccountKind? cashKind = null,
        CurrencyCode? currency = null,
        Guid? parentId = null)
    {
        return new LedgerAccount(
            Guid.NewGuid(),
            "COD",
            "Cuenta",
            type,
            parentId,
            cashKind,
            currency,
            null,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
