using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Budgets;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la persistencia del presupuesto mensual por categoría de gasto.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Solo
/// <c>BudgetService.SetAmountAsync</c> la crea o modifica. El índice único por (CategoryId,
/// PeriodYear, PeriodMonth) impide dos filas para la misma categoría y el mismo mes (regla 6).
/// </remarks>
internal sealed class BudgetConfiguration : IEntityTypeConfiguration<Budget>
{
    public void Configure(EntityTypeBuilder<Budget> builder)
    {
        builder.ToTable(
            "Presupuestos",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_Presupuestos_Amount", "[Amount] >= 0");
                table.HasCheckConstraint(
                    "CK_Presupuestos_PeriodMonth",
                    $"[PeriodMonth] BETWEEN {Budget.MinMonth} AND {Budget.MaxMonth}");
            });

        builder.HasKey(budget => budget.Id);
        builder.Property(budget => budget.PeriodYear).IsRequired();
        builder.Property(budget => budget.PeriodMonth).IsRequired();
        builder.Property(budget => budget.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(budget => budget.CreatedAtUtc).IsRequired();
        builder.Property(budget => budget.UpdatedAtUtc).IsRequired();
        builder.Property(budget => budget.RowVersion).IsRowVersion();

        builder.HasIndex(budget => new { budget.CategoryId, budget.PeriodYear, budget.PeriodMonth })
            .IsUnique()
            .HasDatabaseName("UX_Presupuestos_CategoryId_Period");

        builder.HasOne<FinancialCategory>().WithMany().HasForeignKey(budget => budget.CategoryId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Presupuestos_CategoriasFinancieras");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(budget => budget.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Presupuestos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(budget => budget.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Presupuestos_Usuarios_UpdatedBy");
    }
}
