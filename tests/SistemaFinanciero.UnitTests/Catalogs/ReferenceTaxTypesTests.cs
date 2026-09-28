using SistemaFinanciero.Application.Catalogs.TaxTypes;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class ReferenceTaxTypesTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void EveryReferenceTaxType_IsAValidTaxType()
    {
        foreach (CreateTaxTypeCommand reference in ReferenceTaxTypes.All)
        {
            TaxType taxType = new(
                Guid.NewGuid(),
                reference.Code,
                reference.Name,
                reference.CalculationType,
                reference.Rate,
                reference.Description,
                CreatedAtUtc,
                CreatorId);

            // El código no cambia al normalizarse: el servicio compara contra el código ya guardado.
            Assert.Equal(reference.Code, taxType.Code);
            Assert.Equal(reference.Rate, taxType.Rate);
        }
    }

    [Fact]
    public void ReferenceCodes_AreUniqueIgnoringCase()
    {
        string[] codes = ReferenceTaxTypes.All.Select(reference => reference.Code).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void ReferenceTaxTypes_CoverTheRatesListedInAdr0009()
    {
        decimal[] rates = ReferenceTaxTypes.All
            .Select(reference => reference.Rate)
            .Distinct()
            .Order()
            .ToArray();

        Assert.Equal([0m, 0.5m, 1m, 2m, 4m, 13m], rates);
        Assert.Contains(ReferenceTaxTypes.All, reference => reference.Code == "IVA-EXENTO" && reference.Rate == 0m);
        Assert.Contains(
            ReferenceTaxTypes.All,
            reference => reference.Code == "IVA-0-SIN-CREDITO" && reference.Rate == 0m);
    }

    [Fact]
    public void ReferenceTaxTypes_ArePercentagesAndAskForAccountingValidation()
    {
        Assert.All(
            ReferenceTaxTypes.All,
            reference =>
            {
                Assert.Equal(TaxCalculationType.Percentage, reference.CalculationType);
                Assert.Contains("asesoría contable", reference.Description);
            });
    }
}
