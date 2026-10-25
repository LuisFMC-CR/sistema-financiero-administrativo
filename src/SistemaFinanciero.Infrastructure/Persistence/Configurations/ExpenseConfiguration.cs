using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Domain.Expenses;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia histórica de los gastos.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención.
/// <c>ExpenseService.ConfirmAsync</c> crea la cuenta por pagar cuando el gasto es a crédito, o guarda
/// la cuenta de efectivo y la referencia en el propio gasto cuando es de contado (regla 7).
/// <c>CK_Gastos_DueDate</c> repite en SQL la misma fórmula que el dominio usa para calcular
/// <c>DueDate</c> (regla 14); <c>CK_Gastos_CashConfirmation</c> repite la invariante de
/// <c>Expense.Confirm</c>: cuenta de efectivo y referencia solo existen en un gasto de contado que
/// llegó a confirmarse (vigente o ya anulado, que conserva ese historial igual que una factura
/// anulada conserva su número); un borrador o un gasto a crédito nunca las tiene.
/// </remarks>
internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable(
            "Gastos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Gastos_Status", "[Status] IN (1, 2, 3)");
                table.HasCheckConstraint("CK_Gastos_PaymentTerm", "[PaymentTerm] IN (1, 2, 3, 4)");
                table.HasCheckConstraint("CK_Gastos_PaymentMethod", "[PaymentMethod] IN (1, 2, 3, 4, 5)");
                table.HasCheckConstraint(
                    "CK_Gastos_DueDate",
                    "([PaymentTerm] = 1 AND [DueDate] IS NULL)"
                    + " OR ([PaymentTerm] = 2 AND [DueDate] = DATEADD(DAY, 30, [IssueDate]))"
                    + " OR ([PaymentTerm] = 3 AND [DueDate] = DATEADD(DAY, 60, [IssueDate]))"
                    + " OR ([PaymentTerm] = 4 AND [DueDate] = DATEADD(DAY, 90, [IssueDate]))");
                table.HasCheckConstraint(
                    "CK_Gastos_Amounts",
                    "[NetAmount] >= 0 AND [TaxRate] >= 0 AND [TaxAmount] >= 0 AND [TotalAmount] >= 0");
                table.HasCheckConstraint(
                    "CK_Gastos_CashConfirmation",
                    "(([Status] IN (2, 3) AND [PaymentTerm] = 1) AND [CashAccountId] IS NOT NULL AND [Reference] IS NOT NULL)"
                    + " OR (NOT ([Status] IN (2, 3) AND [PaymentTerm] = 1) AND [CashAccountId] IS NULL AND [Reference] IS NULL)");
            });

        builder.HasKey(expense => expense.Id);
        builder.Property(expense => expense.Code).HasMaxLength(Expense.CodeMaxLength).IsRequired();
        builder.Property(expense => expense.Description).HasMaxLength(Expense.DescriptionMaxLength).IsRequired();
        builder.Property(expense => expense.IssueDate).HasColumnType("date").IsRequired();
        builder.Property(expense => expense.PaymentTerm).IsRequired();
        builder.Property(expense => expense.DueDate).HasColumnType("date");
        builder.Property(expense => expense.NetAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(expense => expense.TaxCode).HasMaxLength(Expense.TaxCodeMaxLength).IsRequired();
        builder.Property(expense => expense.TaxName).HasMaxLength(Expense.TaxNameMaxLength).IsRequired();
        builder.Property(expense => expense.TaxRate).HasPrecision(18, 4).IsRequired();
        builder.Property(expense => expense.TaxAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(expense => expense.TotalAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(expense => expense.PaymentMethod).IsRequired();
        builder.Property(expense => expense.Reference).HasMaxLength(Expense.ReferenceMaxLength);
        builder.Property(expense => expense.Status).IsRequired();
        builder.Property(expense => expense.CancellationReason).HasMaxLength(Expense.ReasonMaxLength);
        builder.Property(expense => expense.CreatedAtUtc).IsRequired();
        builder.Property(expense => expense.UpdatedAtUtc).IsRequired();
        builder.Property(expense => expense.RowVersion).IsRowVersion();

        builder.HasIndex(expense => expense.Code)
            .IsUnique()
            .HasDatabaseName("UX_Gastos_Code");
        builder.HasIndex(expense => new { expense.Status, expense.IssueDate })
            .HasDatabaseName("IX_Gastos_Status_IssueDate");
        builder.HasIndex(expense => new { expense.SupplierId, expense.Status })
            .HasDatabaseName("IX_Gastos_SupplierId_Status");

        builder.HasOne<Supplier>().WithMany().HasForeignKey(expense => expense.SupplierId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_Proveedores");
        builder.HasOne<FinancialCategory>().WithMany().HasForeignKey(expense => expense.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_CategoriasFinancieras");
        builder.HasOne<TaxType>().WithMany().HasForeignKey(expense => expense.TaxTypeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_TiposImpuesto");
        builder.HasOne<LedgerAccount>().WithMany().HasForeignKey(expense => expense.CashAccountId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_CuentasContables");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(expense => expense.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(expense => expense.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_Usuarios_UpdatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(expense => expense.ConfirmedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_Usuarios_ConfirmedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(expense => expense.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Gastos_Usuarios_CancelledBy");
    }
}
