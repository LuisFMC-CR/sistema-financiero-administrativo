using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Incomes;
using SistemaFinanciero.Domain.Invoices;
using SistemaFinanciero.Domain.Receivables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de los ingresos.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>InvoiceService.ConfirmAsync</c> crea el ingreso de una factura de contado,
/// <c>ReceivablePaymentService.RegisterAsync</c> el de un abono e
/// <c>IncomeService.CreateManualAsync</c> el manual. <c>CK_Ingresos_Origin</c>,
/// <c>CK_Ingresos_PaymentMethod</c> y <c>CK_Ingresos_Description</c> repiten en SQL las invariantes
/// que ya exige el constructor de <c>Income</c>.
/// </remarks>
internal sealed class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.ToTable(
            "Ingresos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Ingresos_Origin", "[Origin] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Ingresos_Status", "[Status] IN (1, 2)");
                table.HasCheckConstraint("CK_Ingresos_Amount", "[Amount] >= 0");
                table.HasCheckConstraint(
                    "CK_Ingresos_OriginReferences",
                    "([Origin] = 1 AND [InvoiceId] IS NOT NULL AND [ReceivablePaymentId] IS NULL)"
                    + " OR ([Origin] = 2 AND [ReceivablePaymentId] IS NOT NULL AND [InvoiceId] IS NULL)"
                    + " OR ([Origin] = 3 AND [InvoiceId] IS NULL AND [ReceivablePaymentId] IS NULL)");
                table.HasCheckConstraint(
                    "CK_Ingresos_PaymentMethod",
                    "([Origin] IN (1, 3) AND [PaymentMethod] IS NOT NULL)"
                    + " OR ([Origin] = 2 AND [PaymentMethod] IS NULL)");
                table.HasCheckConstraint(
                    "CK_Ingresos_Description",
                    "([Origin] = 3 AND [Description] IS NOT NULL) OR ([Origin] IN (1, 2))");
            });

        builder.HasKey(income => income.Id);
        builder.Property(income => income.Code).HasMaxLength(Income.CodeMaxLength).IsRequired();
        builder.Property(income => income.Origin).IsRequired();
        builder.Property(income => income.CategoryId).IsRequired();
        builder.Property(income => income.CashAccountId).IsRequired();
        builder.Property(income => income.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(income => income.IncomeDate).HasColumnType("date").IsRequired();
        builder.Property(income => income.Reference).HasMaxLength(Income.ReferenceMaxLength);
        builder.Property(income => income.Description).HasMaxLength(Income.DescriptionMaxLength);
        builder.Property(income => income.Status).IsRequired();
        builder.Property(income => income.CancellationReason).HasMaxLength(Income.DescriptionMaxLength);
        builder.Property(income => income.CreatedAtUtc).IsRequired();
        builder.Property(income => income.UpdatedAtUtc).IsRequired();
        builder.Property(income => income.RowVersion).IsRowVersion();

        builder.HasIndex(income => income.Code)
            .IsUnique()
            .HasDatabaseName("UX_Ingresos_Code");
        builder.HasIndex(income => income.InvoiceId)
            .IsUnique()
            .HasFilter("[InvoiceId] IS NOT NULL")
            .HasDatabaseName("UX_Ingresos_InvoiceId");
        builder.HasIndex(income => income.ReceivablePaymentId)
            .IsUnique()
            .HasFilter("[ReceivablePaymentId] IS NOT NULL")
            .HasDatabaseName("UX_Ingresos_ReceivablePaymentId");
        builder.HasIndex(income => new { income.Status, income.IncomeDate })
            .HasDatabaseName("IX_Ingresos_Status_IncomeDate");

        builder.HasOne<Invoice>().WithMany().HasForeignKey(income => income.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_Facturas");
        builder.HasOne<ReceivablePayment>().WithMany().HasForeignKey(income => income.ReceivablePaymentId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_Abonos");
        builder.HasOne<FinancialCategory>().WithMany().HasForeignKey(income => income.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_CategoriasFinancieras");
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(income => income.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_CuentasContables");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(income => income.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(income => income.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(income => income.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Ingresos_Usuarios_CancelledBy");
    }
}
