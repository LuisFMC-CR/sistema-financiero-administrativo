using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class AccountingPeriodOverrideConfiguration : IEntityTypeConfiguration<AccountingPeriodOverride>
{
    public void Configure(EntityTypeBuilder<AccountingPeriodOverride> builder)
    {
        builder.ToTable(
            "PeriodosContablesReabiertos",
            "finanzas",
            table => table.HasCheckConstraint(
                "CK_PeriodosContablesReabiertos_Month",
                "[Month] BETWEEN 1 AND 12 AND [Year] BETWEEN 1 AND 9998"));

        builder.HasKey(period => new { period.Year, period.Month });
        builder.Property(period => period.Year).IsRequired();
        builder.Property(period => period.Month).IsRequired();
        builder.Property(period => period.IsOpen).IsRequired();
        builder.Property(period => period.Reason)
            .HasMaxLength(SystemParameters.ClosingDateChangeReasonMaxLength);
        builder.Property(period => period.CreatedAtUtc).IsRequired();
        builder.Property(period => period.UpdatedAtUtc).IsRequired();
        builder.Property(period => period.RowVersion).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(period => period.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PeriodosContablesReabiertos_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(period => period.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PeriodosContablesReabiertos_Usuarios_UpdatedBy");
    }
}
