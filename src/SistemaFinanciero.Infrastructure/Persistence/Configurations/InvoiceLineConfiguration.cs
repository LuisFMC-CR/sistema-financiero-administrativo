using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura líneas e impuestos conservados como detalles de una factura.</summary>
internal sealed class InvoiceLineConfiguration : IEntityTypeConfiguration<InvoiceLine>
{
    public void Configure(EntityTypeBuilder<InvoiceLine> builder)
    {
        builder.ToTable(
            "FacturaLineas",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_FacturaLineas_Amounts",
                    "[Quantity] > 0 AND [UnitPrice] >= 0 AND [GrossAmount] >= 0 AND [DiscountAmount] >= 0 AND [DiscountAmount] <= [GrossAmount] AND [NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                table.HasCheckConstraint("CK_FacturaLineas_Position", "[Position] > 0");
            });
        builder.HasKey(line => line.Id);

        // El dominio asigna el Id. Sin esto, una línea nueva agregada a una factura ya guardada se
        // interpretaría como existente y EF intentaría actualizarla en lugar de insertarla.
        builder.Property(line => line.Id).ValueGeneratedNever();
        builder.Property(line => line.Position).IsRequired();
        builder.Property(line => line.Description).HasMaxLength(InvoiceLine.DescriptionMaxLength).IsRequired();
        builder.Property(line => line.UnitOfMeasure).HasMaxLength(InvoiceLine.UnitOfMeasureMaxLength).IsRequired();
        builder.Property(line => line.Quantity).HasPrecision(18, 4).IsRequired();
        builder.Property(line => line.UnitPrice).HasPrecision(18, 4).IsRequired();
        builder.Property(line => line.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.DiscountAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.HasIndex("InvoiceId").HasDatabaseName("IX_FacturaLineas_InvoiceId");
        builder.HasIndex("InvoiceId", nameof(InvoiceLine.Position))
            .IsUnique()
            .HasDatabaseName("UX_FacturaLineas_InvoiceId_Position");
        builder.HasOne<CatalogItem>().WithMany().HasForeignKey(line => line.CatalogItemId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_FacturaLineas_ProductosServicios");

        builder.HasMany(line => line.Taxes)
            .WithOne()
            .HasForeignKey("InvoiceLineId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FacturaLineaImpuestos_FacturaLineas");
    }
}
