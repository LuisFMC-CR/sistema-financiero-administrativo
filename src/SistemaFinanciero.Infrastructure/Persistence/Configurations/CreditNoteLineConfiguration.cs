using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.CreditNotes;
using SistemaFinanciero.Domain.Invoices;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class CreditNoteLineConfiguration : IEntityTypeConfiguration<CreditNoteLine>
{
    public void Configure(EntityTypeBuilder<CreditNoteLine> builder)
    {
        builder.ToTable(
            "NotaCreditoLineas",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_NotaCreditoLineas_Amounts",
                    "[NetAmount] >= 0 AND [TaxRate] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] = [NetAmount] + [TaxAmount]");
            });

        builder.HasKey(line => line.Id);
        builder.Property(line => line.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.TaxRate).HasPrecision(5, 2).IsRequired();
        builder.Property(line => line.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(line => line.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.HasIndex(line => new { line.CreditNoteId, line.InvoiceLineId })
            .IsUnique()
            .HasDatabaseName("UX_NotaCreditoLineas_CreditNoteId_InvoiceLineId");
        builder.HasOne<InvoiceLine>().WithMany().HasForeignKey(line => line.InvoiceLineId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_NotaCreditoLineas_FacturaLineas");
    }
}
