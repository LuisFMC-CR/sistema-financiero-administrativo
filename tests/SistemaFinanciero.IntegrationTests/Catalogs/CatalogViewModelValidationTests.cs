using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Catalogs;
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

    private static List<ValidationResult> Validate(object model)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
