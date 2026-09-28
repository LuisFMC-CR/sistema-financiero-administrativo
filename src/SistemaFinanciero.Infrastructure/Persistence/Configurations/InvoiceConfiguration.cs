using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia histórica de las facturas administrativas.</summary>
internal sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable(
            "Facturas",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Facturas_Status", "[Status] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Facturas_Currency", "[Currency] IN (1, 2)");
                table.HasCheckConstraint("CK_Facturas_PaymentTerm", "[PaymentTerm] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_Facturas_DueDate",
                    "([PaymentTerm] = 1 AND [DueDate] IS NULL) OR ([PaymentTerm] = 2 AND [DueDate] IS NOT NULL AND [DueDate] >= [IssueDate])");
                table.HasCheckConstraint("CK_Facturas_Totals", "[GrossAmount] >= 0 AND [DiscountAmount] >= 0 AND [NetAmount] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                table.HasCheckConstraint("CK_Facturas_UsdRate", "[Currency] <> 2 OR [Status] = 1 OR [ConfirmedCrcPerUsd] > 0");
            });

        builder.HasKey(invoice => invoice.Id);
        builder.Property(invoice => invoice.Number)
            .UseIdentityColumn()
            .ValueGeneratedOnAdd();
        builder.Property(invoice => invoice.IssueDate).HasColumnType("date").IsRequired();
        builder.Property(invoice => invoice.Currency).IsRequired();
        builder.Property(invoice => invoice.PaymentTerm).IsRequired();
        builder.Property(invoice => invoice.DueDate).HasColumnType("date");
        builder.Property(invoice => invoice.Status).IsRequired();
        builder.Property(invoice => invoice.GrossAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.DiscountAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(invoice => invoice.ConfirmedCrcPerUsd).HasPrecision(18, 6);
        builder.Property(invoice => invoice.CancellationReason).HasMaxLength(300);
        builder.Property(invoice => invoice.CreatedAtUtc).IsRequired();
        builder.Property(invoice => invoice.UpdatedAtUtc).IsRequired();
        builder.Property(invoice => invoice.RowVersion).IsRowVersion();

        builder.HasIndex(invoice => invoice.Number)
            .IsUnique()
            .HasDatabaseName("UX_Facturas_Number");
        builder.HasIndex(invoice => new { invoice.Status, invoice.IssueDate })
            .HasDatabaseName("IX_Facturas_Status_IssueDate");
        builder.HasIndex(invoice => new { invoice.CustomerId, invoice.Status })
            .HasDatabaseName("IX_Facturas_CustomerId_Status");

        builder.HasMany(invoice => invoice.Lines)
            .WithOne()
            .HasForeignKey("InvoiceId")
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_FacturaLineas_Facturas");
        builder.HasOne<Customer>().WithMany().HasForeignKey(invoice => invoice.CustomerId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Clientes");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_ConfirmedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(invoice => invoice.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Facturas_Usuarios_CancelledBy");
    }
}
