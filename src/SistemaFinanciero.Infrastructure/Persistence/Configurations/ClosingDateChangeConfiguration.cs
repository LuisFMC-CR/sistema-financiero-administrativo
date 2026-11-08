using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura la bitácora histórica, de solo inserción, de cambios en la fecha de cierre manual.
/// </summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Se conserva para
/// consultar auditoría anterior; los cambios mensuales actuales se guardan en otra tabla.
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
