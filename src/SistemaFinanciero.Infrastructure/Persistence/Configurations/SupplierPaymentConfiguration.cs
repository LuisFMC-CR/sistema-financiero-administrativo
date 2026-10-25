using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de los pagos a proveedores.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>SupplierPaymentService.RegisterAsync</c> crea una fila por cuenta por pagar dentro de un mismo
/// pago, todas con el mismo <see cref="SupplierPayment.GroupId"/>. Espejo de
/// <c>ReceivablePaymentConfiguration</c>, del lado de ingresos.
/// </remarks>
internal sealed class SupplierPaymentConfiguration : IEntityTypeConfiguration<SupplierPayment>
{
    public void Configure(EntityTypeBuilder<SupplierPayment> builder)
    {
        builder.ToTable(
            "Pagos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Pagos_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_Pagos_Amount", "[Amount] >= 0");
            });

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.Reference).HasMaxLength(SupplierPayment.ReferenceMaxLength).IsRequired();
        builder.Property(payment => payment.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.Status).IsRequired();
        builder.Property(payment => payment.CancellationReason).HasMaxLength(SupplierPayment.ReasonMaxLength);
        builder.Property(payment => payment.CreatedAtUtc).IsRequired();
        builder.Property(payment => payment.UpdatedAtUtc).IsRequired();
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        builder.HasIndex(payment => payment.GroupId)
            .HasDatabaseName("IX_Pagos_GroupId");
        builder.HasIndex(payment => new { payment.PayableId, payment.Status })
            .HasDatabaseName("IX_Pagos_PayableId_Status");
        builder.HasIndex(payment => new { payment.CashAccountId, payment.Reference })
            .HasDatabaseName("IX_Pagos_CashAccountId_Reference");

        builder.HasOne<Payable>().WithMany().HasForeignKey(payment => payment.PayableId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Pagos_CuentasPorPagar");
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(payment => payment.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Pagos_CuentasContables");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Pagos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Pagos_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Pagos_Usuarios_CancelledBy");
    }
}
