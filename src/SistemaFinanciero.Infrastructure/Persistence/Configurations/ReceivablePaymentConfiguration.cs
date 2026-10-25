using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de los abonos.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>ReceivablePaymentService.RegisterAsync</c> crea una fila por factura dentro de un mismo abono,
/// todas con el mismo <see cref="ReceivablePayment.GroupId"/>.
/// </remarks>
internal sealed class ReceivablePaymentConfiguration : IEntityTypeConfiguration<ReceivablePayment>
{
    public void Configure(EntityTypeBuilder<ReceivablePayment> builder)
    {
        builder.ToTable(
            "Abonos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Abonos_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_Abonos_Amount", "[Amount] >= 0");
            });

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.Reference).HasMaxLength(ReceivablePayment.ReferenceMaxLength).IsRequired();
        builder.Property(payment => payment.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.Status).IsRequired();
        builder.Property(payment => payment.CancellationReason).HasMaxLength(ReceivablePayment.ReasonMaxLength);
        builder.Property(payment => payment.CreatedAtUtc).IsRequired();
        builder.Property(payment => payment.UpdatedAtUtc).IsRequired();
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        builder.HasIndex(payment => payment.GroupId)
            .HasDatabaseName("IX_Abonos_GroupId");
        builder.HasIndex(payment => new { payment.ReceivableId, payment.Status })
            .HasDatabaseName("IX_Abonos_ReceivableId_Status");
        builder.HasIndex(payment => new { payment.CashAccountId, payment.Reference })
            .HasDatabaseName("IX_Abonos_CashAccountId_Reference");

        builder.HasOne<Receivable>().WithMany().HasForeignKey(payment => payment.ReceivableId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Abonos_CuentasPorCobrar");
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(payment => payment.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Abonos_CuentasContables");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Abonos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Abonos_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Abonos_Usuarios_CancelledBy");
    }
}
