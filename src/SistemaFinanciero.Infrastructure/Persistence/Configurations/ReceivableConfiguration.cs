using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de las cuentas por cobrar.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>InvoiceService.ConfirmAsync</c> la crea cuando la factura es a crédito;
/// <c>InvoiceService.CancelAsync</c> la anula en cascada. <c>CK_CuentasPorCobrar_Balance</c> repite en
/// SQL la invariante 0 ≤ <c>Balance</c> ≤ <c>OriginalAmount</c> que ya exige el dominio.
/// </remarks>
internal sealed class ReceivableConfiguration : IEntityTypeConfiguration<Receivable>
{
    public void Configure(EntityTypeBuilder<Receivable> builder)
    {
        builder.ToTable(
            "CuentasPorCobrar",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_CuentasPorCobrar_Status", "[Status] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_CuentasPorCobrar_Balance",
                    "[Balance] >= 0 AND [Balance] <= [OriginalAmount]");
            });

        builder.HasKey(receivable => receivable.Id);
        builder.Property(receivable => receivable.OriginalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(receivable => receivable.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(receivable => receivable.DueDate).HasColumnType("date").IsRequired();
        builder.Property(receivable => receivable.Status).IsRequired();
        builder.Property(receivable => receivable.CreatedAtUtc).IsRequired();
        builder.Property(receivable => receivable.UpdatedAtUtc).IsRequired();
        builder.Property(receivable => receivable.RowVersion).IsRowVersion();

        builder.HasIndex(receivable => receivable.InvoiceId)
            .IsUnique()
            .HasDatabaseName("UX_CuentasPorCobrar_InvoiceId");
        builder.HasIndex(receivable => new { receivable.CustomerId, receivable.Status })
            .HasDatabaseName("IX_CuentasPorCobrar_CustomerId_Status");
        builder.HasIndex(receivable => new { receivable.Status, receivable.DueDate })
            .HasDatabaseName("IX_CuentasPorCobrar_Status_DueDate");

        builder.HasOne<Invoice>().WithMany().HasForeignKey(receivable => receivable.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorCobrar_Facturas");
        builder.HasOne<Customer>().WithMany().HasForeignKey(receivable => receivable.CustomerId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorCobrar_Clientes");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(receivable => receivable.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorCobrar_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(receivable => receivable.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorCobrar_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(receivable => receivable.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorCobrar_Usuarios_CancelledBy");
    }
}
