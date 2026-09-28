using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class TaxTypeTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesTextAndKeepsFourRateDecimals()
    {
        TaxType taxType = new(
            Guid.NewGuid(),
            " iva-13 ",
            " IVA tarifa general ",
            TaxCalculationType.Percentage,
            13.00005m,
            " Tarifa general del IVA ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("IVA-13", taxType.Code);
        Assert.Equal("IVA tarifa general", taxType.Name);
        Assert.Equal(TaxCalculationType.Percentage, taxType.CalculationType);
        Assert.Equal(13.0001m, taxType.Rate);
        Assert.Equal("Tarifa general del IVA", taxType.Description);
        Assert.True(taxType.IsActive);
        Assert.Equal(CreatedAtUtc, taxType.CreatedAtUtc);
        Assert.Equal(CreatorId, taxType.UpdatedByUserId);
    }

    [Fact]
    public void Constructor_AllowsAZeroRateForExemptAndZeroRatedTaxes()
    {
        TaxType taxType = CreateTaxType(rate: 0m);

        Assert.Equal(0m, taxType.Rate);
    }

    [Fact]
    public void Constructor_AllowsAPercentageOfExactlyOneHundred()
    {
        TaxType taxType = CreateTaxType(rate: 100m);

        Assert.Equal(100m, taxType.Rate);
    }

    [Fact]
    public void Constructor_RejectsAPercentageAboveOneHundred()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTaxType(rate: 100.0001m));
    }

    [Fact]
    public void Constructor_AllowsAFixedAmountAboveOneHundred()
    {
        TaxType taxType = CreateTaxType(TaxCalculationType.FixedAmountPerUnit, rate: 250.5m);

        Assert.Equal(250.5m, taxType.Rate);
    }

    [Theory]
    [InlineData(TaxCalculationType.Percentage)]
    [InlineData(TaxCalculationType.FixedAmountPerUnit)]
    public void Constructor_RejectsANegativeRate(TaxCalculationType calculationType)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTaxType(calculationType, rate: -0.01m));
    }

    [Fact]
    public void Constructor_RejectsARateThatIsNegativeBeforeRoundingOnly()
    {
        // -0,00004 se redondea a cero, que es válido; -0,00005 se redondea a -0,0001, que no lo es.
        Assert.Equal(0m, CreateTaxType(rate: -0.00004m).Rate);
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateTaxType(rate: -0.00005m));
    }

    [Fact]
    public void Constructor_RejectsAnUndefinedCalculationType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateTaxType((TaxCalculationType)99, rate: 1m));
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("   ", "Nombre")]
    [InlineData("IVA", "")]
    [InlineData("IVA", "   ")]
    public void Constructor_RejectsAMissingCodeOrName(string code, string name)
    {
        Assert.Throws<ArgumentException>(() => new TaxType(
            Guid.NewGuid(),
            code,
            name,
            TaxCalculationType.Percentage,
            13m,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsATextThatExceedsTheMaximumLength()
    {
        Assert.Throws<ArgumentException>(() => new TaxType(
            Guid.NewGuid(),
            new string('A', TaxType.CodeMaxLength + 1),
            "Nombre",
            TaxCalculationType.Percentage,
            13m,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_TreatsABlankDescriptionAsMissing()
    {
        TaxType taxType = new(
            Guid.NewGuid(),
            "IVA",
            "IVA",
            TaxCalculationType.Percentage,
            13m,
            "   ",
            CreatedAtUtc,
            CreatorId);

        Assert.Null(taxType.Description);
    }

    [Fact]
    public void UpdateDetails_ChangesTheEditableDataAndKeepsTheCalculationType()
    {
        TaxType taxType = CreateTaxType(TaxCalculationType.FixedAmountPerUnit, rate: 5m);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        taxType.UpdateDetails(" esp-2 ", " Específico 2 ", 7.5m, " ", changedAtUtc, EditorId);

        Assert.Equal("ESP-2", taxType.Code);
        Assert.Equal("Específico 2", taxType.Name);
        Assert.Equal(7.5m, taxType.Rate);
        Assert.Null(taxType.Description);
        Assert.Equal(TaxCalculationType.FixedAmountPerUnit, taxType.CalculationType);
        Assert.Equal(changedAtUtc, taxType.UpdatedAtUtc);
        Assert.Equal(EditorId, taxType.UpdatedByUserId);
        Assert.Equal(CreatorId, taxType.CreatedByUserId);
    }

    [Fact]
    public void UpdateDetails_ValidatesTheRateAgainstTheCalculationTypeOfTheTax()
    {
        TaxType percentage = CreateTaxType(TaxCalculationType.Percentage, rate: 13m);
        TaxType fixedAmount = CreateTaxType(TaxCalculationType.FixedAmountPerUnit, rate: 13m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            percentage.UpdateDetails("IVA", "IVA", 150m, null, CreatedAtUtc.AddMinutes(1), EditorId));

        fixedAmount.UpdateDetails("ESP", "Específico", 150m, null, CreatedAtUtc.AddMinutes(1), EditorId);
        Assert.Equal(150m, fixedAmount.Rate);
    }

    [Fact]
    public void UpdateDetails_LeavesTheTaxUntouchedWhenAnyValueIsInvalid()
    {
        TaxType taxType = CreateTaxType(rate: 13m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            taxType.UpdateDetails("NUEVO", "Nuevo nombre", -1m, "Otra", CreatedAtUtc.AddMinutes(1), EditorId));

        Assert.Equal("IVA", taxType.Code);
        Assert.Equal("IVA tarifa general", taxType.Name);
        Assert.Equal(13m, taxType.Rate);
        Assert.Null(taxType.Description);
        Assert.Equal(CreatedAtUtc, taxType.UpdatedAtUtc);
        Assert.Equal(CreatorId, taxType.UpdatedByUserId);
    }

    [Fact]
    public void UpdateDetails_RejectsAnEmptyUserOrAnInstantBeforeTheLastChange()
    {
        TaxType taxType = CreateTaxType(rate: 13m);

        Assert.Throws<ArgumentException>(() =>
            taxType.UpdateDetails("IVA", "IVA", 13m, null, CreatedAtUtc.AddMinutes(1), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            taxType.UpdateDetails("IVA", "IVA", 13m, null, CreatedAtUtc.AddMinutes(-1), EditorId));
    }

    [Fact]
    public void Deactivate_KeepsTheTaxAndUpdatesTheAudit()
    {
        TaxType taxType = CreateTaxType(rate: 13m);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        taxType.Deactivate(changedAtUtc, EditorId);

        Assert.False(taxType.IsActive);
        Assert.Equal(13m, taxType.Rate);
        Assert.Equal(changedAtUtc, taxType.UpdatedAtUtc);
        Assert.Equal(EditorId, taxType.UpdatedByUserId);
    }

    [Fact]
    public void Activate_RestoresTheTaxAfterItWasDeactivated()
    {
        TaxType taxType = CreateTaxType(rate: 13m);
        taxType.Deactivate(CreatedAtUtc.AddMinutes(5), EditorId);

        taxType.Activate(CreatedAtUtc.AddMinutes(10), CreatorId);

        Assert.True(taxType.IsActive);
        Assert.Equal(CreatedAtUtc.AddMinutes(10), taxType.UpdatedAtUtc);
    }

    [Fact]
    public void SettingTheStateItAlreadyHas_ChangesNothing()
    {
        TaxType taxType = CreateTaxType(rate: 13m);

        taxType.Activate(CreatedAtUtc.AddMinutes(5), EditorId);

        Assert.True(taxType.IsActive);
        Assert.Equal(CreatedAtUtc, taxType.UpdatedAtUtc);
        Assert.Equal(CreatorId, taxType.UpdatedByUserId);
    }

    private static TaxType CreateTaxType(
        TaxCalculationType calculationType = TaxCalculationType.Percentage,
        decimal rate = 13m)
    {
        return new TaxType(
            Guid.NewGuid(),
            "IVA",
            "IVA tarifa general",
            calculationType,
            rate,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
