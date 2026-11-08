using SistemaFinanciero.Domain.Catalogs;

namespace SistemaFinanciero.UnitTests.Catalogs;

public sealed class CatalogItemTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid CreatorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Constructor_NormalizesTextAndKeepsFourReferencePriceDecimals()
    {
        Guid accountId = Guid.NewGuid();
        Guid taxTypeId = Guid.NewGuid();

        CatalogItem item = new(
            Guid.NewGuid(),
            " srv-001 ",
            " Soporte mensual ",
            CatalogItemType.Service,
            "8311500000000",
            " Atención remota ",
            UnitOfMeasureCode.Hour,
            123.45678m,
            accountId,
            taxTypeId,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("SRV-001", item.Code);
        Assert.Equal("Soporte mensual", item.Name);
        Assert.Equal(CatalogItemType.Service, item.Type);
        Assert.Equal("8311500000000", item.CabysCode);
        Assert.Equal("Atención remota", item.Description);
        Assert.Equal(UnitOfMeasureCode.Hour, item.UnitOfMeasure);
        Assert.Equal(123.4568m, item.ReferencePriceCrc);
        Assert.Equal(accountId, item.DefaultIncomeAccountId);
        Assert.Equal(taxTypeId, item.DefaultTaxTypeId);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Constructor_AllowsMissingReferencePriceAndCategory()
    {
        CatalogItem item = CreateItem(referencePriceCrc: null);

        Assert.Null(item.ReferencePriceCrc);
        Assert.Null(item.DefaultIncomeAccountId);
        Assert.Null(item.DefaultTaxTypeId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.00001)]
    public void Constructor_RejectsAReferencePriceThatIsNotPositiveAfterRounding(
        decimal invalidPrice)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CreateItem(referencePriceCrc: invalidPrice));
    }

    [Theory]
    [InlineData("123456789012")]
    [InlineData("12345678901234")]
    [InlineData("831150000000A")]
    public void Constructor_RejectsACabysCodeThatIsNotThirteenDigits(string invalidCabysCode)
    {
        Assert.Throws<ArgumentException>(() => new CatalogItem(
            Guid.NewGuid(),
            "ITEM-001",
            "Elemento",
            CatalogItemType.Service,
            invalidCabysCode,
            null,
            UnitOfMeasureCode.Unit,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    [Fact]
    public void UpdateDetails_ChangesEditableValuesAndAudit()
    {
        CatalogItem item = CreateItem(referencePriceCrc: 100m);
        Guid accountId = Guid.NewGuid();
        Guid taxTypeId = Guid.NewGuid();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        item.UpdateDetails(
            " Equipo ",
            "8511900000000",
            " ",
            UnitOfMeasureCode.Unit,
            20.12345m,
            accountId,
            taxTypeId,
            changedAtUtc,
            EditorId);

        Assert.Equal("ITEM-001", item.Code);
        Assert.Equal("Equipo", item.Name);
        Assert.Equal(CatalogItemType.Service, item.Type);
        Assert.Equal("8511900000000", item.CabysCode);
        Assert.Null(item.Description);
        Assert.Equal(UnitOfMeasureCode.Unit, item.UnitOfMeasure);
        Assert.Equal(20.1235m, item.ReferencePriceCrc);
        Assert.Equal(accountId, item.DefaultIncomeAccountId);
        Assert.Equal(taxTypeId, item.DefaultTaxTypeId);
        Assert.Equal(changedAtUtc, item.UpdatedAtUtc);
        Assert.Equal(EditorId, item.UpdatedByUserId);
    }

    [Fact]
    public void Constructor_RejectsUnknownItemType()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CatalogItem(
            Guid.NewGuid(),
            "ITEM-001",
            "Elemento",
            (CatalogItemType)99,
            "8311500000000",
            null,
            UnitOfMeasureCode.Unit,
            null,
            null,
            null,
            CreatedAtUtc,
            CreatorId));
    }

    private static CatalogItem CreateItem(decimal? referencePriceCrc)
    {
        return new CatalogItem(
            Guid.NewGuid(),
            "ITEM-001",
            "Elemento",
            CatalogItemType.Service,
            "8311500000000",
            null,
            UnitOfMeasureCode.Unit,
            referencePriceCrc,
            null,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
