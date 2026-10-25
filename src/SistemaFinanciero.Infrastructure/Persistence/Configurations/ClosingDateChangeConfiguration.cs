using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura la bitácora, de solo inserción, de cambios en la fecha de cierre.
/// </summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Solo
/// <c>SystemParametersService.ChangeClosingDateAsync</c> le agrega filas; nadie las modifica,
/// protegido por <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>.
/// </remarks>
internal sealed class ClosingDateChangeConfiguration : IEntityTypeConfiguration<ClosingDateChange>
{
    public void Configure(EntityTypeBuilder<ClosingDateChange> builder)
    {
        builder.ToTable("ParametrosFechaCierreHistorial", "finanzas");

        builder.HasKey(change => change.Id);
        builder.Property(change => change.PreviousClosingDate).HasColumnType("date").IsRequired();
        builder.Property(change => change.NewClosingDate).HasColumnType("date").IsRequired();
        builder.Property(change => change.Reason).HasMaxLength(SystemParameters.ClosingDateChangeReasonMaxLength);
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => change.ChangedAtUtc)
            .HasDatabaseName("IX_ParametrosFechaCierreHistorial_ChangedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ParametrosFechaCierreHistorial_Usuarios_ChangedBy");
    }
}
