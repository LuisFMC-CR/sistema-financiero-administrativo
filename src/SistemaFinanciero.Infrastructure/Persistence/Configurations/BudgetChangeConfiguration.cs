using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Budgets;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Configura la bitácora, de solo inserción, de cambios en el presupuesto mensual.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Solo
/// <c>BudgetService.SetAmountAsync</c> le agrega filas; nadie las modifica, protegido por
/// <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>, igual criterio que
/// <c>SystemParameterChangeConfiguration</c>.
/// </remarks>
internal sealed class BudgetChangeConfiguration : IEntityTypeConfiguration<BudgetChange>
{
    public void Configure(EntityTypeBuilder<BudgetChange> builder)
    {
        builder.ToTable(
            "PresupuestoCambios",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint("CK_PresupuestoCambios_NewAmount", "[NewAmount] >= 0");
                table.HasCheckConstraint(
                    "CK_PresupuestoCambios_PreviousAmount",
                    "[PreviousAmount] IS NULL OR [PreviousAmount] >= 0");
            });

        builder.HasKey(change => change.Id);
        builder.Property(change => change.PreviousAmount).HasPrecision(18, 2);
        builder.Property(change => change.NewAmount).HasPrecision(18, 2).IsRequired();
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => change.BudgetId)
            .HasDatabaseName("IX_PresupuestoCambios_BudgetId");
        builder.HasIndex(change => change.ChangedAtUtc)
            .HasDatabaseName("IX_PresupuestoCambios_ChangedAtUtc");

        builder.HasOne<Budget>().WithMany().HasForeignKey(change => change.BudgetId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_PresupuestoCambios_Presupuestos");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PresupuestoCambios_Usuarios_ChangedBy");
    }
}
