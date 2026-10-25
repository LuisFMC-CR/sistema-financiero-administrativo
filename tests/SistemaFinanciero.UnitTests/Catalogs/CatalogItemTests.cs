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
        Guid categoryId = Guid.NewGuid();
        Guid taxTypeId = Guid.NewGuid();

        CatalogItem item = new(
            Guid.NewGuid(),
            " srv-001 ",
            " Soporte mensual ",
            CatalogItemType.Service,
            " Atención remota ",
            " mes ",
            123.45678m,
            categoryId,
            taxTypeId,
            CreatedAtUtc,
            CreatorId);

        Assert.Equal("SRV-001", item.Code);
        Assert.Equal("Soporte mensual", item.Name);
        Assert.Equal(CatalogItemType.Service, item.Type);
        Assert.Equal("Atención remota", item.Description);
        Assert.Equal("mes", item.UnitOfMeasure);
        Assert.Equal(123.4568m, item.ReferencePriceCrc);
        Assert.Equal(categoryId, item.DefaultIncomeCategoryId);
        Assert.Equal(taxTypeId, item.DefaultTaxTypeId);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void Constructor_AllowsMissingReferencePriceAndCategory()
    {
        CatalogItem item = CreateItem(referencePriceCrc: null);

        Assert.Null(item.ReferencePriceCrc);
        Assert.Null(item.DefaultIncomeCategoryId);
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

    [Fact]
    public void UpdateDetails_ChangesEditableValuesAndAudit()
    {
        CatalogItem item = CreateItem(referencePriceCrc: 100m);
        Guid categoryId = Guid.NewGuid();
        Guid taxTypeId = Guid.NewGuid();
        DateTimeOffset changedAtUtc = CreatedAtUtc.AddMinutes(5);

        item.UpdateDetails(
            " Equipo ",
            " ",
            " unidad ",
            20.12345m,
            categoryId,
            taxTypeId,
            changedAtUtc,
            EditorId);

        Assert.Equal("ITEM-001", item.Code);
        Assert.Equal("Equipo", item.Name);
        Assert.Equal(CatalogItemType.Service, item.Type);
        Assert.Null(item.Description);
        Assert.Equal("unidad", item.UnitOfMeasure);
        Assert.Equal(20.1235m, item.ReferencePriceCrc);
        Assert.Equal(categoryId, item.DefaultIncomeCategoryId);
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
            null,
            "unidad",
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
            null,
            "unidad",
            referencePriceCrc,
            null,
            null,
            CreatedAtUtc,
            CreatorId);
    }
}
