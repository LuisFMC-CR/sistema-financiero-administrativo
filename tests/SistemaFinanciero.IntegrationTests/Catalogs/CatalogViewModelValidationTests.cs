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
            CabysCode = "8311500000000",
            UnitOfMeasure = UnitOfMeasureCode.Hour,
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
            CabysCode = "8311500000000",
            UnitOfMeasure = UnitOfMeasureCode.Unit,
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
            IdentificationType = IdentificationType.LegalEntity,
            Identification = "3101000001",
            EconomicActivity = null,
        };

        Assert.Empty(Validate(model));
    }

    [Fact]
    public void TaxTypeRateEdit_RequiresTheVersionToken()
    {
        TaxTypeRateEditViewModel model = new() { Rate = 13m, Version = string.Empty };

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
    public void LedgerAccountEdit_RequiresTheVersionToken()
    {
        LedgerAccountEditViewModel model = new() { Code = "A", Name = "A", Version = string.Empty };

        Assert.Contains(
            Validate(model),
            result => result.MemberNames.Contains(nameof(model.Version)));
    }

    [Fact]
    public void WithholdingTypeRateEdit_RequiresTheVersionToken()
    {
        WithholdingTypeRateEditViewModel model = new() { Rate = 2m, Version = string.Empty };

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
