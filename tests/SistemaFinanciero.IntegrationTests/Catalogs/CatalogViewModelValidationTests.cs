using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class CatalogViewModelValidationTests
{
    [Fact]
    public void CatalogItem_AllowsBothReferencePricesToRemainUndefined()
    {
        CatalogItemInputViewModel model = new()
        {
            Code = "SRV-001",
            Name = "Servicio variable",
            Type = CatalogItemType.Service,
            UnitOfMeasure = "hora",
            ReferencePriceCrc = null,
            ReferencePriceUsd = null,
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void CatalogItem_RejectsAZeroReferencePrice()
    {
        CatalogItemInputViewModel model = new()
        {
            Code = "PROD-001",
            Name = "Producto",
            Type = CatalogItemType.Product,
            UnitOfMeasure = "unidad",
            ReferencePriceCrc = 0,
        };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.ReferencePriceCrc)));
    }

    [Fact]
    public void Contact_RejectsFieldsBeyondDatabaseLengths()
    {
        CustomerInputViewModel model = new()
        {
            Code = new string('A', 31),
            Name = new string('N', 151),
            Phone = new string('1', 31),
        };

        List<ValidationResult> results = Validate(model);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Code)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Name)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Phone)));
    }

    [Fact]
    public void TaxType_AcceptsAValidPercentageAndAZeroRate()
    {
        TaxTypeCreateViewModel model = new()
        {
            Code = "IVA-0",
            Name = "IVA 0 %",
            CalculationType = TaxCalculationType.Percentage,
            Rate = 0m,
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void TaxType_RejectsANegativeRate()
    {
        TaxTypeCreateViewModel model = new() { Code = "IVA", Name = "IVA", Rate = -0.5m };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Rate)));
    }

    [Fact]
    public void TaxType_RejectsAnUndefinedCalculationType()
    {
        TaxTypeCreateViewModel model = new()
        {
            Code = "IVA",
            Name = "IVA",
            CalculationType = (TaxCalculationType)99,
        };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.CalculationType)));
    }

    [Fact]
    public void TaxType_RejectsFieldsBeyondDatabaseLengthsAndMissingRequiredOnes()
    {
        TaxTypeCreateViewModel tooLong = new()
        {
            Code = new string('A', 31),
            Name = new string('N', 121),
            Description = new string('D', 301),
        };
        TaxTypeCreateViewModel missing = new() { Code = string.Empty, Name = string.Empty };

        List<ValidationResult> longResults = Validate(tooLong);
        List<ValidationResult> missingResults = Validate(missing);

        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Code)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Name)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Description)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Code)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Name)));
    }

    [Fact]
    public void TaxTypeEdit_RequiresTheVersionToken()
    {
        TaxTypeEditViewModel model = new() { Code = "IVA", Name = "IVA", Rate = 13m, Version = string.Empty };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Version)));
    }

    [Fact]
    public void LedgerAccount_AllowsACashAssetAndANonCashAccountOfAnyType()
    {
        LedgerAccountCreateViewModel cashAsset = new()
        {
            Code = "BCO",
            Name = "Banco",
            Type = LedgerAccountType.Asset,
            IsCash = true,
        };
        LedgerAccountCreateViewModel liability = new()
        {
            Code = "PAS",
            Name = "Pasivos",
            Type = LedgerAccountType.Liability,
        };

        Assert.Empty(Validate(cashAsset));
        Assert.Empty(Validate(liability));
    }

    [Theory]
    [InlineData(LedgerAccountType.Liability)]
    [InlineData(LedgerAccountType.Equity)]
    [InlineData(LedgerAccountType.Income)]
    [InlineData(LedgerAccountType.Expense)]
    public void LedgerAccount_RejectsACashAccountThatIsNotAnAsset(LedgerAccountType type)
    {
        LedgerAccountCreateViewModel model = new()
        {
            Code = "X",
            Name = "X",
            Type = type,
            IsCash = true,
        };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.IsCash)));
    }

    [Fact]
    public void LedgerAccount_RejectsUndefinedTypeKindAndCurrency()
    {
        LedgerAccountCreateViewModel model = new()
        {
            Code = "X",
            Name = "X",
            Type = (LedgerAccountType)99,
            CashKind = (CashAccountKind)99,
            Currency = (SistemaFinanciero.Domain.Currencies.CurrencyCode)99,
        };

        List<ValidationResult> results = Validate(model);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Type)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.CashKind)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Currency)));
    }

    [Fact]
    public void LedgerAccount_RejectsFieldsBeyondDatabaseLengthsAndMissingRequiredOnes()
    {
        LedgerAccountCreateViewModel tooLong = new()
        {
            Code = new string('A', 31),
            Name = new string('N', 121),
            Reference = new string('R', 101),
            Description = new string('D', 301),
        };
        LedgerAccountCreateViewModel missing = new() { Code = string.Empty, Name = string.Empty };

        List<ValidationResult> longResults = Validate(tooLong);
        List<ValidationResult> missingResults = Validate(missing);

        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Code)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Name)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Reference)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Description)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Code)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Name)));
    }

    [Fact]
    public void FinancialCategory_RequiresALedgerAccount()
    {
        FinancialCategoryInputViewModel withoutAccount = new()
        {
            Code = "ING",
            Name = "Ingresos",
            Kind = FinancialCategoryKind.Income,
            LedgerAccountId = null,
        };
        FinancialCategoryInputViewModel withAccount = new()
        {
            Code = "ING",
            Name = "Ingresos",
            Kind = FinancialCategoryKind.Income,
            LedgerAccountId = Guid.NewGuid(),
        };

        Assert.Contains(
            Validate(withoutAccount),
            result => result.MemberNames.Contains(nameof(withoutAccount.LedgerAccountId)));
        Assert.Empty(Validate(withAccount));
    }

    [Fact]
    public void LedgerAccountEdit_RequiresTheVersionToken()
    {
        LedgerAccountEditViewModel model = new() { Code = "A", Name = "A", Version = string.Empty };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Version)));
    }

    [Fact]
    public void WithholdingType_AcceptsAValidRateIncludingTheBounds()
    {
        WithholdingTypeCreateViewModel zero = new() { Code = "RET-0", Name = "Sin retención", Rate = 0m };
        WithholdingTypeCreateViewModel full = new() { Code = "RET-100", Name = "Retención total", Rate = 100m };

        Assert.Empty(Validate(zero));
        Assert.Empty(Validate(full));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public void WithholdingType_RejectsARateOutsideZeroToOneHundred(decimal rate)
    {
        WithholdingTypeCreateViewModel model = new() { Code = "RET", Name = "Retención", Rate = rate };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Rate)));
    }

    [Fact]
    public void WithholdingType_RejectsFieldsBeyondDatabaseLengthsAndMissingRequiredOnes()
    {
        WithholdingTypeCreateViewModel tooLong = new()
        {
            Code = new string('A', 31),
            Name = new string('N', 121),
            Description = new string('D', 301),
        };
        WithholdingTypeCreateViewModel missing = new() { Code = string.Empty, Name = string.Empty };

        List<ValidationResult> longResults = Validate(tooLong);
        List<ValidationResult> missingResults = Validate(missing);

        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Code)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Name)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Description)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Code)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Name)));
    }

    [Fact]
    public void WithholdingTypeEdit_RequiresTheVersionToken()
    {
        WithholdingTypeEditViewModel model = new() { Code = "RET", Name = "Retención", Rate = 2m, Version = string.Empty };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Version)));
    }

    private static List<ValidationResult> Validate(object model)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
