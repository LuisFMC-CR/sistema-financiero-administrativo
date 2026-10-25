using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Domain.Payables;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia de las cuentas por pagar.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>ExpenseService.ConfirmAsync</c> la crea cuando el gasto es a crédito;
/// <c>ExpenseService.CancelAsync</c> la anula en cascada. <c>CK_CuentasPorPagar_Balance</c> repite en
/// SQL la invariante 0 ≤ <c>Balance</c> ≤ <c>OriginalAmount</c> que ya exige el dominio. Espejo de
/// <c>ReceivableConfiguration</c>, del lado de ingresos.
/// </remarks>
internal sealed class PayableConfiguration : IEntityTypeConfiguration<Payable>
{
    public void Configure(EntityTypeBuilder<Payable> builder)
    {
        builder.ToTable(
            "CuentasPorPagar",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_CuentasPorPagar_Status", "[Status] IN (1, 2, 3, 4)");
                table.HasCheckConstraint(
                    "CK_CuentasPorPagar_Balance",
                    "[Balance] >= 0 AND [Balance] <= [OriginalAmount]");
            });

        builder.HasKey(payable => payable.Id);
        builder.Property(payable => payable.OriginalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(payable => payable.Balance).HasPrecision(18, 2).IsRequired();
        builder.Property(payable => payable.DueDate).HasColumnType("date").IsRequired();
        builder.Property(payable => payable.Status).IsRequired();
        builder.Property(payable => payable.CreatedAtUtc).IsRequired();
        builder.Property(payable => payable.UpdatedAtUtc).IsRequired();
        builder.Property(payable => payable.RowVersion).IsRowVersion();

        builder.HasIndex(payable => payable.ExpenseId)
            .IsUnique()
            .HasDatabaseName("UX_CuentasPorPagar_ExpenseId");
        builder.HasIndex(payable => new { payable.SupplierId, payable.Status })
            .HasDatabaseName("IX_CuentasPorPagar_SupplierId_Status");
        builder.HasIndex(payable => new { payable.Status, payable.DueDate })
            .HasDatabaseName("IX_CuentasPorPagar_Status_DueDate");

        builder.HasOne<Expense>().WithMany().HasForeignKey(payable => payable.ExpenseId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorPagar_Gastos");
        builder.HasOne<Supplier>().WithMany().HasForeignKey(payable => payable.SupplierId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorPagar_Proveedores");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payable => payable.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorPagar_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payable => payable.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorPagar_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(payable => payable.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_CuentasPorPagar_Usuarios_CancelledBy");
    }
}
