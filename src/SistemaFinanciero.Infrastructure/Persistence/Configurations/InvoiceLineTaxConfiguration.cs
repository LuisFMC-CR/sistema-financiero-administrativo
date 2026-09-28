using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la fotografía de impuestos aplicada a cada línea.</summary>
internal sealed class InvoiceLineTaxConfiguration : IEntityTypeConfiguration<InvoiceLineTax>
{
    public void Configure(EntityTypeBuilder<InvoiceLineTax> builder)
    {
        builder.ToTable(
            "FacturaLineaImpuestos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_FacturaLineaImpuestos_Type", "[CalculationType] IN (1, 2)");
                table.HasCheckConstraint("CK_FacturaLineaImpuestos_RateAmount", "[Rate] >= 0 AND [TaxableAmount] >= 0 AND [Amount] >= 0");
            });
        builder.HasKey(tax => tax.Id);

        // El dominio asigna el Id; ver InvoiceLineConfiguration.
        builder.Property(tax => tax.Id).ValueGeneratedNever();
        builder.Property(tax => tax.Code).HasMaxLength(InvoiceLineTax.CodeMaxLength).IsRequired();
        builder.Property(tax => tax.Name).HasMaxLength(InvoiceLineTax.NameMaxLength).IsRequired();
        builder.Property(tax => tax.CalculationType).IsRequired();
        builder.Property(tax => tax.Rate).HasPrecision(18, 4).IsRequired();
        builder.Property(tax => tax.TaxableAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(tax => tax.Amount).HasPrecision(18, 2).IsRequired();
        builder.HasOne<TaxType>()
            .WithMany()
            .HasForeignKey(tax => tax.TaxTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_FacturaLineaImpuestos_TiposImpuesto");
        builder.HasIndex("InvoiceLineId", nameof(InvoiceLineTax.Code))
            .IsUnique()
            .HasDatabaseName("UX_FacturaLineaImpuestos_Line_Code");
    }
}
