using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class FinancialAccountTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesFieldsAndFixesTypeAndCurrency()
    {
        FinancialAccount account = new(
            Guid.NewGuid(),
            " bnk-usd ",
            " Banco USD ",
            FinancialAccountType.Bank,
            CurrencyCode.USD,
            " Cuenta terminada en 1234 ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("BNK-USD", account.Code);
        Assert.Equal("Banco USD", account.Name);
        Assert.Equal(FinancialAccountType.Bank, account.Type);
        Assert.Equal(CurrencyCode.USD, account.Currency);
        Assert.Equal("Cuenta terminada en 1234", account.Reference);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void UpdateDetails_PreservesTypeAndCurrency()
    {
        FinancialAccount account = CreateAccount();
        FinancialAccountType originalType = account.Type;
        CurrencyCode originalCurrency = account.Currency;
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        account.UpdateDetails(
            " caja-principal ",
            " Caja principal ",
            " ",
            changedAtUtc,
            EditorId);

        Assert.Equal("CAJA-PRINCIPAL", account.Code);
        Assert.Equal("Caja principal", account.Name);
        Assert.Null(account.Reference);
        Assert.Equal(originalType, account.Type);
        Assert.Equal(originalCurrency, account.Currency);
        Assert.Equal(changedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(EditorId, account.UpdatedByUserId);
    }

    [Fact]
    public void DomainModel_DoesNotStoreAnEditableBalance()
    {
        Assert.DoesNotContain(
            typeof(FinancialAccount).GetProperties(),
            property => property.Name.Contains("Balance", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(99, 1)]
    [InlineData(1, 99)]
    public void Constructor_RejectsUnknownTypeOrCurrency(int accountType, int currency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FinancialAccount(
            Guid.NewGuid(),
            "CTA-001",
            "Cuenta",
            (FinancialAccountType)accountType,
            (CurrencyCode)currency,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Deactivate_PreservesTheAccountAndUpdatesAudit()
    {
        FinancialAccount account = CreateAccount();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        account.Deactivate(changedAtUtc, EditorId);

        Assert.False(account.IsActive);
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.Equal(changedAtUtc, account.UpdatedAtUtc);
        Assert.Equal(EditorId, account.UpdatedByUserId);
    }

    private static FinancialAccount CreateAccount()
    {
        return new FinancialAccount(
            Guid.NewGuid(),
            "CAJA-CRC",
            "Caja CRC",
            FinancialAccountType.Cash,
            CurrencyCode.CRC,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
