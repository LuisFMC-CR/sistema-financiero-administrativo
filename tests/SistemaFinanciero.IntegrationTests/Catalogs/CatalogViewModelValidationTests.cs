using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class CatalogViewModelValidationTests
{
    [Fact]
    public void CatalogItem_AllowsTheReferencePriceToRemainUndefined()
    {
        CatalogItemInputViewModel model = new()
        {
            Name = "Servicio variable",
            Type = CatalogItemType.Service,
            UnitOfMeasure = "hora",
            ReferencePriceCrc = null,
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void CatalogItem_RejectsAZeroReferencePrice()
    {
        CatalogItemInputViewModel model = new()
        {
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
            Identification = new string('I', 51),
            Name = new string('N', 151),
            Phone = new string('1', 31),
        };

        List<ValidationResult> results = Validate(model);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Identification)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Name)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(model.Phone)));
    }

    [Fact]
    public void Contact_RequiresIdentification()
    {
        CustomerInputViewModel model = new() { Name = "Cliente", Identification = string.Empty };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Identification)));
    }

    [Fact]
    public void Supplier_AllowsAnOptionalEconomicActivity()
    {
        SupplierInputViewModel model = new()
        {
            Name = "Proveedor",
            Identification = "3-101-000001",
            EconomicActivity = null,
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void TaxType_AcceptsAValidPercentageAndAZeroRate()
    {
        TaxTypeCreateViewModel model = new()
        {
            Code = "IVA-0",
            Name = "IVA 0 %",
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
    public void LedgerAccount_AllowsACashAccountAndANonCashAccountWithAParent()
    {
        LedgerAccountCreateViewModel cashAccount = new()
        {
            Name = "Banco",
            ParentId = Guid.NewGuid(),
            IsCash = true,
        };
        LedgerAccountCreateViewModel plainAccount = new()
        {
            Name = "Detalle",
            ParentId = Guid.NewGuid(),
        };

        Assert.Empty(Validate(cashAccount));
        Assert.Empty(Validate(plainAccount));
    }

    [Fact]
    public void LedgerAccount_RequiresAParent()
    {
        LedgerAccountCreateViewModel model = new() { Name = "Detalle", ParentId = Guid.Empty };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.ParentId)));
    }

    [Fact]
    public void LedgerAccount_RejectsAnUndefinedCashKind()
    {
        LedgerAccountCreateViewModel model = new()
        {
            Name = "X",
            ParentId = Guid.NewGuid(),
            CashKind = (CashAccountKind)99,
        };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.CashKind)));
    }

    [Fact]
    public void LedgerAccount_RejectsFieldsBeyondDatabaseLengthsAndMissingRequiredOnes()
    {
        LedgerAccountCreateViewModel tooLong = new()
        {
            Name = new string('N', 121),
            ParentId = Guid.NewGuid(),
            Reference = new string('R', 101),
            Description = new string('D', 301),
        };
        LedgerAccountCreateViewModel missing = new() { Name = string.Empty, ParentId = Guid.NewGuid() };

        List<ValidationResult> longResults = Validate(tooLong);
        List<ValidationResult> missingResults = Validate(missing);

        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Name)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Reference)));
        Assert.Contains(longResults, result => result.MemberNames.Contains(nameof(tooLong.Description)));
        Assert.Contains(missingResults, result => result.MemberNames.Contains(nameof(missing.Name)));
    }

    [Fact]
    public void FinancialCategory_RequiresALedgerAccount()
    {
        FinancialCategoryInputViewModel withoutAccount = new()
        {
            Name = "Ingresos",
            Kind = FinancialCategoryKind.Income,
            LedgerAccountId = null,
        };
        FinancialCategoryInputViewModel withAccount = new()
        {
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
