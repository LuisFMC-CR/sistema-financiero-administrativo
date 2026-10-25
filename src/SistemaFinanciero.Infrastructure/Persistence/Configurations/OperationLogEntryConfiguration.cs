using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Audit;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="OperationLogEntry"/> a la tabla <c>seguridad.BitacoraOperaciones</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención; el carácter de solo
/// inserción de la tabla lo hace cumplir <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>,
/// no esta clase.
/// </remarks>
internal sealed class OperationLogEntryConfiguration : IEntityTypeConfiguration<OperationLogEntry>
{
    public void Configure(EntityTypeBuilder<OperationLogEntry> builder)
    {
        builder.ToTable(
            "BitacoraOperaciones",
            "seguridad",
            table =>
            {
                table.HasCheckConstraint("CK_BitacoraOperaciones_EntityType", "[EntityType] BETWEEN 1 AND 7");
                table.HasCheckConstraint("CK_BitacoraOperaciones_Action", "[Action] BETWEEN 1 AND 3");
            });

        builder.HasKey(entry => entry.Id);
        builder.Property(entry => entry.EntityType).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.Action).HasConversion<int>().IsRequired();
        builder.Property(entry => entry.Reason).HasMaxLength(OperationLogEntry.ReasonMaxLength);
        builder.Property(entry => entry.PerformedAtUtc).IsRequired();

        builder.HasIndex(entry => new { entry.EntityType, entry.EntityId })
            .HasDatabaseName("IX_BitacoraOperaciones_EntityType_EntityId");
        builder.HasIndex(entry => entry.PerformedAtUtc)
            .HasDatabaseName("IX_BitacoraOperaciones_PerformedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(entry => entry.PerformedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_BitacoraOperaciones_Usuarios_PerformedBy");
    }
}
