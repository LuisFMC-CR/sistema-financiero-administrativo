using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Currencies;
using SistemaFinanciero.Infrastructure.Persistence;

namespace SistemaFinanciero.IntegrationTests.Catalogs;

public sealed class CatalogPersistenceModelTests : IClassFixture<FinancialWebApplicationFactory>
{
    private readonly FinancialWebApplicationFactory factory;

    public CatalogPersistenceModelTests(FinancialWebApplicationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public void CatalogEntities_AreMappedToTheApprovedSchemasAndTables()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();

        AssertTable<Customer>(context, "catalogos", "Clientes");
        AssertTable<Supplier>(context, "catalogos", "Proveedores");
        AssertTable<CatalogItem>(context, "catalogos", "ProductosServicios");
        AssertTable<FinancialCategory>(context, "catalogos", "CategoriasFinancieras");
        AssertTable<DailyExchangeRate>(context, "finanzas", "TiposCambioDiarios");
        AssertTable<TaxType>(context, "catalogos", "TiposImpuesto");
        AssertTable<LedgerAccount>(context, "catalogos", "CuentasContables");
        AssertTable<WithholdingType>(context, "catalogos", "TiposRetencion");
        AssertTable<SistemaFinanciero.Domain.Parameters.SystemParameters>(context, "finanzas", "ParametrosSistema");
        AssertTable<SistemaFinanciero.Domain.Parameters.SystemParameterChange>(
            context,
            "finanzas",
            "ParametrosSistemaHistorial");
    }

    [Fact]
    public void MutableCatalogs_UseSqlServerRowVersion()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();

        Type[] entityTypes =
        [
            typeof(Customer),
            typeof(Supplier),
            typeof(CatalogItem),
            typeof(FinancialCategory),
            typeof(DailyExchangeRate),
            typeof(TaxType),
            typeof(LedgerAccount),
            typeof(WithholdingType),
            typeof(SistemaFinanciero.Domain.Parameters.SystemParameters),
        ];

        foreach (Type entityType in entityTypes)
        {
            IEntityType metadata = context.Model.FindEntityType(entityType)!;
            IProperty rowVersion = metadata.FindProperty("RowVersion")!;

            Assert.True(rowVersion.IsConcurrencyToken);
            Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
        }
    }

    [Fact]
    public void MonetaryPrecisionAndCurrencyStorage_AreExplicit()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();

        IEntityType item = context.Model.FindEntityType(typeof(CatalogItem))!;
        Assert.Equal(18, item.FindProperty(nameof(CatalogItem.ReferencePriceCrc))!.GetPrecision());
        Assert.Equal(4, item.FindProperty(nameof(CatalogItem.ReferencePriceCrc))!.GetScale());
        Assert.Equal(18, item.FindProperty(nameof(CatalogItem.ReferencePriceUsd))!.GetPrecision());
        Assert.Equal(4, item.FindProperty(nameof(CatalogItem.ReferencePriceUsd))!.GetScale());

        IEntityType rate = context.Model.FindEntityType(typeof(DailyExchangeRate))!;
        Assert.Equal(18, rate.FindProperty(nameof(DailyExchangeRate.Rate))!.GetPrecision());
        Assert.Equal(6, rate.FindProperty(nameof(DailyExchangeRate.Rate))!.GetScale());

        IEntityType taxType = context.Model.FindEntityType(typeof(TaxType))!;
        Assert.Equal(18, taxType.FindProperty(nameof(TaxType.Rate))!.GetPrecision());
        Assert.Equal(4, taxType.FindProperty(nameof(TaxType.Rate))!.GetScale());

        IEntityType ledgerAccount = context.Model.FindEntityType(typeof(LedgerAccount))!;
        Assert.Equal("char(3)", ledgerAccount.FindProperty(nameof(LedgerAccount.Currency))!.GetColumnType());
        Assert.True(ledgerAccount.FindProperty(nameof(LedgerAccount.Currency))!.IsNullable);
        Assert.True(ledgerAccount.FindProperty(nameof(LedgerAccount.CashKind))!.IsNullable);
        Assert.Null(ledgerAccount.FindProperty(nameof(LedgerAccount.IsCash)));
    }

    [Fact]
    public void BusinessForeignKeys_NeverCascadeDelete()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();

        Type[] entityTypes =
        [
            typeof(Customer),
            typeof(Supplier),
            typeof(CatalogItem),
            typeof(FinancialCategory),
            typeof(DailyExchangeRate),
            typeof(TaxType),
            typeof(LedgerAccount),
            typeof(WithholdingType),
        ];

        IEnumerable<IForeignKey> foreignKeys = entityTypes
            .Select(context.Model.FindEntityType)
            .SelectMany(entityType => entityType!.GetForeignKeys());

        Assert.All(
            foreignKeys,
            foreignKey => Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior));
    }

    [Fact]
    public void BusinessCodesAndRateDate_HaveUniqueIndexes()
    {
        using IServiceScope scope = factory.Services.CreateScope();
        FinancialDbContext context = scope.ServiceProvider.GetRequiredService<FinancialDbContext>();

        AssertUniqueIndex<Customer>(context, nameof(Customer.Code));
        AssertUniqueIndex<Supplier>(context, nameof(Supplier.Code));
        AssertUniqueIndex<CatalogItem>(context, nameof(CatalogItem.Code));
        AssertUniqueIndex<FinancialCategory>(context, nameof(FinancialCategory.Code));
        AssertUniqueIndex<DailyExchangeRate>(context, nameof(DailyExchangeRate.EffectiveDate));
        AssertUniqueIndex<TaxType>(context, nameof(TaxType.Code));
        AssertUniqueIndex<LedgerAccount>(context, nameof(LedgerAccount.Code));
        AssertUniqueIndex<WithholdingType>(context, nameof(WithholdingType.Code));
    }

    private static void AssertTable<TEntity>(
        FinancialDbContext context,
        string expectedSchema,
        string expectedTable)
    {
        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))!;
        Assert.Equal(expectedSchema, entityType.GetSchema());
        Assert.Equal(expectedTable, entityType.GetTableName());
    }

    private static void AssertUniqueIndex<TEntity>(
        FinancialDbContext context,
        string propertyName)
    {
        IEntityType entityType = context.Model.FindEntityType(typeof(TEntity))!;
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.IsUnique &&
                index.Properties.Count == 1 &&
                index.Properties[0].Name == propertyName);
    }
}
