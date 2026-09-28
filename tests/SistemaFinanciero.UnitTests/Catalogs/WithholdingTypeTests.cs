using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class WithholdingTypeTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 9, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesTextAndKeepsFourRateDecimals()
    {
        WithholdingType withholding = new(
            Guid.NewGuid(),
            " ret-2 ",
            " Retención del 2 % ",
            2.00005m,
            " Retención sobre compras ",
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("RET-2", withholding.Code);
        Assert.Equal("Retención del 2 %", withholding.Name);
        Assert.Equal(2.0001m, withholding.Rate);
        Assert.Equal("Retención sobre compras", withholding.Description);
        Assert.True(withholding.IsActive);
        Assert.Equal(CreatedAtUtc, withholding.CreatedAtUtc);
        Assert.Equal(CreatorId, withholding.UpdatedByUserId);
    }

    [Fact]
    public void Constructor_AllowsAZeroRate()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 0m);

        Assert.Equal(0m, withholding.Rate);
    }

    [Fact]
    public void Constructor_AllowsExactlyOneHundred()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 100m);

        Assert.Equal(100m, withholding.Rate);
    }

    [Fact]
    public void Constructor_RejectsARateAboveOneHundred()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithholdingType(rate: 100.0001m));
    }

    [Fact]
    public void Constructor_RejectsANegativeRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithholdingType(rate: -0.01m));
    }

    [Fact]
    public void Constructor_RejectsARateThatIsNegativeBeforeRoundingOnly()
    {
        // -0,00004 se redondea a cero, que es válido; -0,00005 se redondea a -0,0001, que no lo es.
        Assert.Equal(0m, CreateWithholdingType(rate: -0.00004m).Rate);
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithholdingType(rate: -0.00005m));
    }

    [Theory]
    [InlineData("", "Nombre")]
    [InlineData("   ", "Nombre")]
    [InlineData("RET", "")]
    [InlineData("RET", "   ")]
    public void Constructor_RejectsAMissingCodeOrName(string code, string name)
    {
        Assert.Throws<ArgumentException>(() => new WithholdingType(
            Guid.NewGuid(),
            code,
            name,
            2m,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_RejectsATextThatExceedsTheMaximumLength()
    {
        Assert.Throws<ArgumentException>(() => new WithholdingType(
            Guid.NewGuid(),
            new string('A', WithholdingType.CodeMaxLength + 1),
            "Nombre",
            2m,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void Constructor_TreatsABlankDescriptionAsMissing()
    {
        WithholdingType withholding = new(
            Guid.NewGuid(),
            "RET",
            "Retención",
            2m,
            "   ",
            CreatedAtUtc,
            CreatorId);

        Assert.Null(withholding.Description);
    }

    [Fact]
    public void UpdateDetails_ChangesTheEditableData()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 2m);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        withholding.UpdateDetails(" ret-4 ", " Retención 4 % ", 4m, " ", changedAtUtc, EditorId);

        Assert.Equal("RET-4", withholding.Code);
        Assert.Equal("Retención 4 %", withholding.Name);
        Assert.Equal(4m, withholding.Rate);
        Assert.Null(withholding.Description);
        Assert.Equal(changedAtUtc, withholding.UpdatedAtUtc);
        Assert.Equal(EditorId, withholding.UpdatedByUserId);
        Assert.Equal(CreatorId, withholding.CreatedByUserId);
    }

    [Fact]
    public void UpdateDetails_LeavesTheWithholdingUntouchedWhenTheRateIsInvalid()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 2m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            withholding.UpdateDetails("NUEVO", "Nuevo nombre", 150m, "Otra", CreatedAtUtc.AddMinutes(1), EditorId));

        Assert.Equal("RET", withholding.Code);
        Assert.Equal("Retención", withholding.Name);
        Assert.Equal(2m, withholding.Rate);
        Assert.Null(withholding.Description);
        Assert.Equal(CreatedAtUtc, withholding.UpdatedAtUtc);
        Assert.Equal(CreatorId, withholding.UpdatedByUserId);
    }

    [Fact]
    public void UpdateDetails_RejectsAnEmptyUserOrAnInstantBeforeTheLastChange()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 2m);

        Assert.Throws<ArgumentException>(() =>
            withholding.UpdateDetails("RET", "Retención", 2m, null, CreatedAtUtc.AddMinutes(1), Guid.Empty));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            withholding.UpdateDetails("RET", "Retención", 2m, null, CreatedAtUtc.AddMinutes(-1), EditorId));
    }

    [Fact]
    public void Deactivate_KeepsTheWithholdingAndUpdatesTheAudit()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 2m);
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        withholding.Deactivate(changedAtUtc, EditorId);

        Assert.False(withholding.IsActive);
        Assert.Equal(2m, withholding.Rate);
        Assert.Equal(changedAtUtc, withholding.UpdatedAtUtc);
        Assert.Equal(EditorId, withholding.UpdatedByUserId);
    }

    [Fact]
    public void Activate_RestoresTheWithholdingAfterItWasDeactivated()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 2m);
        withholding.Deactivate(CreatedAtUtc.AddMinutes(5), EditorId);

        withholding.Activate(CreatedAtUtc.AddMinutes(10), CreatorId);

        Assert.True(withholding.IsActive);
        Assert.Equal(CreatedAtUtc.AddMinutes(10), withholding.UpdatedAtUtc);
    }

    [Fact]
    public void SettingTheStateItAlreadyHas_ChangesNothing()
    {
        WithholdingType withholding = CreateWithholdingType(rate: 2m);

        withholding.Activate(CreatedAtUtc.AddMinutes(5), EditorId);

        Assert.True(withholding.IsActive);
        Assert.Equal(CreatedAtUtc, withholding.UpdatedAtUtc);
        Assert.Equal(CreatorId, withholding.UpdatedByUserId);
    }

    private static WithholdingType CreateWithholdingType(decimal rate)
    {
        return new WithholdingType(
            Guid.NewGuid(),
            "RET",
            "Retención",
            rate,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
