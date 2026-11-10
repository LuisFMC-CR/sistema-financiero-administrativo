using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.IncomeTax;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de los pagos de impuesto sobre la renta (RF-99).</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>IncomeTaxService.RegisterPaymentAsync</c> crea cada fila. Espejo de
/// <c>CashTransferConfiguration</c>.
/// </remarks>
internal sealed class IncomeTaxPaymentConfiguration : IEntityTypeConfiguration<IncomeTaxPayment>
{
    public void Configure(EntityTypeBuilder<IncomeTaxPayment> builder)
    {
        builder.ToTable(
            "PagosImpuestoRenta",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_PagosImpuestoRenta_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_PagosImpuestoRenta_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_PagosImpuestoRenta_PaymentMethod", "[PaymentMethod] IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint(
                    "CK_PagosImpuestoRenta_FiscalPeriod",
                    "DAY([FiscalPeriodStartDate]) = 1 "
                    + "AND [FiscalPeriodEndDate] = DATEADD(day, -1, DATEADD(year, 1, [FiscalPeriodStartDate]))");
                table.HasCheckConstraint(
                    "CK_PagosImpuestoRenta_PaymentDate",
                    "[PaymentDate] >= [FiscalPeriodStartDate]");
            });

        builder.HasKey(payment => payment.Id);
        builder.Property(payment => payment.Code).HasMaxLength(IncomeTaxPayment.CodeMaxLength).IsRequired();
        builder.Property(payment => payment.FiscalPeriodStartDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.FiscalPeriodEndDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.PaymentMethod).IsRequired();
        builder.Property(payment => payment.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(payment => payment.Reference).HasMaxLength(IncomeTaxPayment.ReferenceMaxLength).IsRequired();
        builder.Property(payment => payment.Description).HasMaxLength(IncomeTaxPayment.DescriptionMaxLength);
        builder.Property(payment => payment.PaymentDate).HasColumnType("date").IsRequired();
        builder.Property(payment => payment.Status).IsRequired();
        builder.Property(payment => payment.CancellationReason).HasMaxLength(IncomeTaxPayment.ReasonMaxLength);
        builder.Property(payment => payment.CreatedAtUtc).IsRequired();
        builder.Property(payment => payment.UpdatedAtUtc).IsRequired();
        builder.Property(payment => payment.RowVersion).IsRowVersion();

        builder.HasIndex(payment => payment.Code)
            .IsUnique()
            .HasDatabaseName("UX_PagosImpuestoRenta_Code");
        builder.HasIndex(payment => new { payment.FiscalPeriodStartDate, payment.Status })
            .HasDatabaseName("IX_PagosImpuestoRenta_FiscalPeriodStartDate_Status");
        builder.HasIndex(payment => new { payment.CashAccountId, payment.Reference })
            .HasDatabaseName("IX_PagosImpuestoRenta_CashAccountId_Reference");

        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(payment => payment.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PagosImpuestoRenta_CuentasContables_CashAccount");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PagosImpuestoRenta_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PagosImpuestoRenta_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PagosImpuestoRenta_Usuarios_CancelledBy");
    }
}
