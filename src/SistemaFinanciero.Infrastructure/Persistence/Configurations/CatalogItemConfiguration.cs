using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="CatalogItem"/> a la tabla <c>catalogos.ProductosServicios</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. La usa
/// <c>CatalogItemService</c>; su clave foránea restrictiva hacia <c>FinancialCategory</c>
/// (<c>DefaultIncomeCategoryId</c>) y hacia <c>TaxType</c> (<c>DefaultTaxTypeId</c>, RF-52) impide
/// eliminar una categoría o una tarifa de IVA referenciada.
/// </remarks>
internal sealed class CatalogItemConfiguration : IEntityTypeConfiguration<CatalogItem>
{
    public void Configure(EntityTypeBuilder<CatalogItem> builder)
    {
        builder.ToTable(
            "ProductosServicios",
            "catalogos",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ProductosServicios_Codigo_NoVacio",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.HasCheckConstraint(
                    "CK_ProductosServicios_Type",
                    "[Type] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_ProductosServicios_PriceCrc",
                    "[ReferencePriceCrc] IS NULL OR [ReferencePriceCrc] > 0");
            });

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Code)
            .HasMaxLength(CatalogItem.CodeMaxLength)
            .IsRequired();
        builder.Property(item => item.Name)
            .HasMaxLength(CatalogItem.NameMaxLength)
            .IsRequired();
        builder.Property(item => item.Type).IsRequired();
        builder.Property(item => item.Description)
            .HasMaxLength(CatalogItem.DescriptionMaxLength);
        builder.Property(item => item.UnitOfMeasure)
            .HasMaxLength(CatalogItem.UnitOfMeasureMaxLength)
            .IsRequired();
        builder.Property(item => item.ReferencePriceCrc).HasPrecision(18, 4);
        builder.Property(item => item.IsActive)
            .HasDefaultValue(true);
        builder.Property(item => item.CreatedAtUtc).IsRequired();
        builder.Property(item => item.UpdatedAtUtc).IsRequired();
        builder.Property(item => item.RowVersion).IsRowVersion();

        builder.HasIndex(item => item.Code)
            .IsUnique()
            .HasDatabaseName("UX_ProductosServicios_Code");
        builder.HasIndex(item => new { item.IsActive, item.Name })
            .HasDatabaseName("IX_ProductosServicios_IsActive_Name");

        builder.HasOne<FinancialCategory>()
            .WithMany()
            .HasForeignKey(item => item.DefaultIncomeCategoryId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductosServicios_CategoriasFinancieras");
        builder.HasOne<TaxType>()
            .WithMany()
            .HasForeignKey(item => item.DefaultTaxTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductosServicios_TiposImpuesto");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductosServicios_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(item => item.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ProductosServicios_Usuarios_UpdatedBy");
    }
}
