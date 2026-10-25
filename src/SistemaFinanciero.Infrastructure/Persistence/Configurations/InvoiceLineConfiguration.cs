using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura líneas de factura y el IVA que cada una conserva como detalle.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Declara
/// <c>ValueGeneratedNever()</c> en el <c>Id</c> porque lo asigna el dominio
/// (<c>InvoiceLine</c>), no la base de datos; sin eso, EF confundiría una línea nueva con una existente
/// al guardarla. <see cref="InvoiceLine.Tax"/> es un tipo propietario de EF Core: como cada línea lleva
/// una sola tarifa de IVA, no necesita su propia tabla ni clave; vive como columnas de
/// <c>finanzas.FacturaLineas</c>. Su clave foránea restrictiva hacia <c>TaxType</c> (<c>TaxTypeId</c>,
/// opcional) impide borrar un tipo de impuesto que una línea ya usó.
/// </remarks>
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
                table.HasCheckConstraint(
                    "CK_FacturaLineas_Tax_RateAmount",
                    "[Tax_Rate] >= 0 AND [Tax_TaxableAmount] >= 0 AND [Tax_Amount] >= 0");
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

        builder.OwnsOne(line => line.Tax, tax =>
        {
            tax.Property(t => t.Code).HasColumnName("Tax_Code")
                .HasMaxLength(InvoiceLineTax.CodeMaxLength).IsRequired();
            tax.Property(t => t.Name).HasColumnName("Tax_Name")
                .HasMaxLength(InvoiceLineTax.NameMaxLength).IsRequired();
            tax.Property(t => t.Rate).HasColumnName("Tax_Rate").HasPrecision(18, 4).IsRequired();
            tax.Property(t => t.TaxableAmount).HasColumnName("Tax_TaxableAmount").HasPrecision(18, 2).IsRequired();
            tax.Property(t => t.Amount).HasColumnName("Tax_Amount").HasPrecision(18, 2).IsRequired();
            tax.Property(t => t.TaxTypeId).HasColumnName("TaxTypeId");
            tax.HasOne<TaxType>()
                .WithMany()
                .HasForeignKey(t => t.TaxTypeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_FacturaLineas_TiposImpuesto");
        });
    }
}
