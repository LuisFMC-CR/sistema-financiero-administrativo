using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class AccountingPeriodChangeConfiguration : IEntityTypeConfiguration<AccountingPeriodChange>
{
    public void Configure(EntityTypeBuilder<AccountingPeriodChange> builder)
    {
        builder.ToTable(
            "PeriodosContablesHistorial",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_PeriodosContablesHistorial_Month",
                    "[Month] BETWEEN 1 AND 12");
                table.HasCheckConstraint(
                    "CK_PeriodosContablesHistorial_StateChanged",
                    "[WasOpen] <> [IsOpen]");
            });

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.Year).IsRequired();
        builder.Property(change => change.Month).IsRequired();
        builder.Property(change => change.WasOpen).IsRequired();
        builder.Property(change => change.IsOpen).IsRequired();
        builder.Property(change => change.Reason)
            .HasMaxLength(SystemParameters.ClosingDateChangeReasonMaxLength);
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => new { change.Year, change.Month, change.ChangedAtUtc })
            .HasDatabaseName("IX_PeriodosContablesHistorial_Periodo_ChangedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_PeriodosContablesHistorial_Usuarios_ChangedBy");
    }
}
