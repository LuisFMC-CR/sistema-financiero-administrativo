using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.VatSettlements;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de las liquidaciones mensuales del IVA.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>VatSettlementService.RegisterAsync</c> crea cada fila. El índice único filtrado garantiza una
/// sola liquidación vigente por mes (paquete v8, bloque 2).
/// </remarks>
internal sealed class VatSettlementConfiguration : IEntityTypeConfiguration<VatSettlement>
{
    public void Configure(EntityTypeBuilder<VatSettlement> builder)
    {
        builder.ToTable(
            "LiquidacionesIva",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_LiquidacionesIva_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_LiquidacionesIva_NonNegativeAmounts",
                    "[VatPayableAmount] >= 0 AND [VatCreditApplied] >= 0 AND [PaymentAmount] >= 0 AND [CreditCarriedForward] >= 0");
                table.HasCheckConstraint("CK_LiquidacionesIva_PeriodMonth", "[PeriodMonth] BETWEEN 1 AND 12");
                table.HasCheckConstraint(
                    "CK_LiquidacionesIva_Totals",
                    "[VatPayableAmount] = [VatCreditApplied] + [PaymentAmount]");
                table.HasCheckConstraint(
                    "CK_LiquidacionesIva_Payment",
                    "([PaymentAmount] = 0 AND [CashAccountId] IS NULL AND [Reference] IS NULL)"
                    + " OR ([PaymentAmount] > 0 AND [CashAccountId] IS NOT NULL AND [Reference] IS NOT NULL)");
            });

        builder.HasKey(settlement => settlement.Id);
        builder.Property(settlement => settlement.Code).HasMaxLength(VatSettlement.CodeMaxLength).IsRequired();
        builder.Property(settlement => settlement.PeriodYear).IsRequired();
        builder.Property(settlement => settlement.PeriodMonth).IsRequired();
        builder.Property(settlement => settlement.SettlementDate).HasColumnType("date").IsRequired();
        builder.Property(settlement => settlement.VatPayableAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(settlement => settlement.VatCreditApplied).HasPrecision(18, 2).IsRequired();
        builder.Property(settlement => settlement.PaymentAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(settlement => settlement.CreditCarriedForward).HasPrecision(18, 2).IsRequired();
        builder.Property(settlement => settlement.Reference).HasMaxLength(VatSettlement.ReferenceMaxLength);
        builder.Property(settlement => settlement.Status).IsRequired();
        builder.Property(settlement => settlement.CancellationReason).HasMaxLength(VatSettlement.ReasonMaxLength);
        builder.Property(settlement => settlement.CreatedAtUtc).IsRequired();
        builder.Property(settlement => settlement.UpdatedAtUtc).IsRequired();
        builder.Property(settlement => settlement.RowVersion).IsRowVersion();

        builder.HasIndex(settlement => settlement.Code)
            .IsUnique()
            .HasDatabaseName("UX_LiquidacionesIva_Code");
        builder.HasIndex(settlement => new { settlement.PeriodYear, settlement.PeriodMonth })
            .IsUnique()
            .HasFilter("[Status] = 1")
            .HasDatabaseName("UX_LiquidacionesIva_Period_Vigente");
        builder.HasIndex(settlement => settlement.Status)
            .HasDatabaseName("IX_LiquidacionesIva_Status");

        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(settlement => settlement.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LiquidacionesIva_CuentasContables");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(settlement => settlement.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LiquidacionesIva_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(settlement => settlement.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LiquidacionesIva_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(settlement => settlement.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_LiquidacionesIva_Usuarios_CancelledBy");
    }
}
