using System.ComponentModel.DataAnnotations;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Web.Models.Catalogs;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

/// <summary>
/// ADR-0061: el formulario de productos y servicios solo acepta las unidades Unidad u Hora y muestra
/// el mensaje junto al campo cuando llega una unidad retirada.
/// </summary>
public sealed class SelectableUnitOfMeasureAttributeTests
{
    [Theory]
    [InlineData(UnitOfMeasureCode.Unit, true)]
    [InlineData(UnitOfMeasureCode.Hour, true)]
    [InlineData(UnitOfMeasureCode.ProfessionalService, false)]
    [InlineData(UnitOfMeasureCode.OtherService, false)]
    public void UnitOfMeasure_IsValidOnlyForUnitAndHour(UnitOfMeasureCode unit, bool expectedValid)
    {
        CatalogItemInputViewModel model = new()
        {
            Name = "Soporte técnico",
            CabysCode = "8311500000000",
            UnitOfMeasure = unit,
        };
        List<ValidationResult> results = [];

        bool valid = Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
        if (!expectedValid)
        {
            ValidationResult error = Assert.Single(results);
            Assert.Equal("Seleccione la unidad de medida Unidad u Hora.", error.ErrorMessage);
            Assert.Contains(nameof(CatalogItemInputViewModel.UnitOfMeasure), error.MemberNames);
        }
    }
}
