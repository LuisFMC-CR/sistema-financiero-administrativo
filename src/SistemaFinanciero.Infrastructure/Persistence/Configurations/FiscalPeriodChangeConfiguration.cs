using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class FiscalPeriodChangeConfiguration : IEntityTypeConfiguration<FiscalPeriodChange>
{
    public void Configure(EntityTypeBuilder<FiscalPeriodChange> builder)
    {
        builder.ToTable(
            "PeriodoFiscalHistorial",
            "finanzas",
            table => table.HasCheckConstraint(
                "CK_PeriodoFiscalHistorial_Range",
                "([PreviousStartDate] IS NULL AND [PreviousEndDate] IS NULL OR "
                + "[PreviousStartDate] IS NOT NULL AND DAY([PreviousStartDate]) = 1 AND [PreviousEndDate] = "
                + "DATEADD(day, -1, DATEADD(year, 1, [PreviousStartDate]))) AND "
                + "DAY([NewStartDate]) = 1 AND [NewEndDate] = DATEADD(day, -1, DATEADD(year, 1, [NewStartDate]))"));

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.PreviousStartDate).HasColumnType("date");
        builder.Property(change => change.PreviousEndDate).HasColumnType("date");
        builder.Property(change => change.NewStartDate).HasColumnType("date").IsRequired();
        builder.Property(change => change.NewEndDate).HasColumnType("date").IsRequired();
        builder.Property(change => change.Reason)
            .HasMaxLength(SystemParameters.ClosingDateChangeReasonMaxLength);
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => change.ChangedAtUtc)
            .HasDatabaseName("IX_PeriodoFiscalHistorial_ChangedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PeriodoFiscalHistorial_Usuarios_ChangedBy");
    }
}
