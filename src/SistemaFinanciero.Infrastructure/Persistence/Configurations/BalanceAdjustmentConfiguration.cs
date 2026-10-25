using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Adjustments;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de los ajustes manuales de saldo.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>EnsureAppendOnlyLogsAreNotMutated</c> protege la tabla contra modificaciones y borrados, igual
/// que el resto de bitácoras.
/// </remarks>
internal sealed class BalanceAdjustmentConfiguration : IEntityTypeConfiguration<BalanceAdjustment>
{
    public void Configure(EntityTypeBuilder<BalanceAdjustment> builder)
    {
        builder.ToTable(
            "AjustesSaldo",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_AjustesSaldo_Direction", "[Direction] IN (1, 2)");
                table.HasCheckConstraint("CK_AjustesSaldo_Amount", "[Amount] > 0");
                table.HasCheckConstraint(
                    "CK_AjustesSaldo_UnaSolaCuenta",
                    "([ReceivableId] IS NOT NULL AND [PayableId] IS NULL) OR "
                        + "([ReceivableId] IS NULL AND [PayableId] IS NOT NULL)");
            });

        builder.HasKey(adjustment => adjustment.Id);
        builder.Property(adjustment => adjustment.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(adjustment => adjustment.Direction).HasConversion<int>().IsRequired();
        builder.Property(adjustment => adjustment.Reason).HasMaxLength(BalanceAdjustment.ReasonMaxLength).IsRequired();
        builder.Property(adjustment => adjustment.CreatedAtUtc).IsRequired();

        builder.HasIndex(adjustment => adjustment.ReceivableId)
            .HasDatabaseName("IX_AjustesSaldo_ReceivableId");
        builder.HasIndex(adjustment => adjustment.PayableId)
            .HasDatabaseName("IX_AjustesSaldo_PayableId");

        builder.HasOne<Receivable>().WithMany().HasForeignKey(adjustment => adjustment.ReceivableId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_AjustesSaldo_CuentasPorCobrar");
        builder.HasOne<Payable>().WithMany().HasForeignKey(adjustment => adjustment.PayableId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_AjustesSaldo_CuentasPorPagar");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(adjustment => adjustment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_AjustesSaldo_Usuarios_CreatedBy");
    }
}
