using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura la bitácora, de solo inserción, de cambios en los parámetros del sistema.
/// </summary>
internal sealed class SystemParameterChangeConfiguration : IEntityTypeConfiguration<SystemParameterChange>
{
    public void Configure(EntityTypeBuilder<SystemParameterChange> builder)
    {
        builder.ToTable(
            "ParametrosSistemaHistorial",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ParametrosSistemaHistorial_NewAuthorizationLimit",
                    "[NewAuthorizationLimitCrc] >= 0");
                table.HasCheckConstraint(
                    "CK_ParametrosSistemaHistorial_PreviousAuthorizationLimit",
                    "[PreviousAuthorizationLimitCrc] IS NULL OR [PreviousAuthorizationLimitCrc] >= 0");
                table.HasCheckConstraint(
                    "CK_ParametrosSistemaHistorial_NewOverdueAlertDays",
                    "[NewOverdueAlertDays] > 0");
                table.HasCheckConstraint(
                    "CK_ParametrosSistemaHistorial_PreviousOverdueAlertDays",
                    "[PreviousOverdueAlertDays] IS NULL OR [PreviousOverdueAlertDays] > 0");
            });

        builder.HasKey(change => change.Id);
        builder.Property(change => change.PreviousAuthorizationLimitCrc).HasPrecision(18, 2);
        builder.Property(change => change.NewAuthorizationLimitCrc).HasPrecision(18, 2).IsRequired();
        builder.Property(change => change.NewOverdueAlertDays).IsRequired();
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => change.ChangedAtUtc)
            .HasDatabaseName("IX_ParametrosSistemaHistorial_ChangedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ParametrosSistemaHistorial_Usuarios_ChangedBy");
    }
}
