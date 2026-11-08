using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.CashTransfers;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de los traspasos entre cuentas de efectivo.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>CashTransferService.RegisterAsync</c> crea cada fila. Espejo de
/// <c>SupplierPaymentConfiguration</c>, sin agrupación: un traspaso siempre es una sola fila.
/// </remarks>
internal sealed class CashTransferConfiguration : IEntityTypeConfiguration<CashTransfer>
{
    public void Configure(EntityTypeBuilder<CashTransfer> builder)
    {
        builder.ToTable(
            "Traspasos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Traspasos_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_Traspasos_Amount", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_Traspasos_DifferentAccounts",
                    "[SourceCashAccountId] <> [DestinationCashAccountId]");
            });

        builder.HasKey(transfer => transfer.Id);
        builder.Property(transfer => transfer.Code).HasMaxLength(CashTransfer.CodeMaxLength).IsRequired();
        builder.Property(transfer => transfer.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(transfer => transfer.Reference).HasMaxLength(CashTransfer.ReferenceMaxLength).IsRequired();
        builder.Property(transfer => transfer.Description).HasMaxLength(CashTransfer.DescriptionMaxLength).IsRequired();
        builder.Property(transfer => transfer.TransferDate).HasColumnType("date").IsRequired();
        builder.Property(transfer => transfer.Status).IsRequired();
        builder.Property(transfer => transfer.CancellationReason).HasMaxLength(CashTransfer.ReasonMaxLength);
        builder.Property(transfer => transfer.CreatedAtUtc).IsRequired();
        builder.Property(transfer => transfer.UpdatedAtUtc).IsRequired();
        builder.Property(transfer => transfer.RowVersion).IsRowVersion();

        builder.HasIndex(transfer => transfer.Code)
            .IsUnique()
            .HasDatabaseName("UX_Traspasos_Code");
        builder.HasIndex(transfer => new { transfer.Status, transfer.TransferDate })
            .HasDatabaseName("IX_Traspasos_Status_TransferDate");
        builder.HasIndex(transfer => new { transfer.SourceCashAccountId, transfer.Reference })
            .HasDatabaseName("IX_Traspasos_SourceCashAccountId_Reference");
        builder.HasIndex(transfer => new { transfer.DestinationCashAccountId, transfer.Reference })
            .HasDatabaseName("IX_Traspasos_DestinationCashAccountId_Reference");

        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(transfer => transfer.SourceCashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Traspasos_CuentasContables_Source");
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(transfer => transfer.DestinationCashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Traspasos_CuentasContables_Destination");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(transfer => transfer.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Traspasos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(transfer => transfer.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Traspasos_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(transfer => transfer.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Traspasos_Usuarios_CancelledBy");
    }
}
