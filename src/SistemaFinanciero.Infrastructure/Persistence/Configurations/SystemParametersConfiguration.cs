using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="SystemParameters"/> a la tabla <c>finanzas.ParametrosSistema</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. La usa
/// <c>SystemParametersService</c>. El identificador siempre vale
/// <see cref="SystemParameters.SingletonId"/>, así que la clave primaria por sí sola impide que exista
/// una segunda fila.
/// </remarks>
internal sealed class SystemParametersConfiguration : IEntityTypeConfiguration<SystemParameters>
{
    public void Configure(EntityTypeBuilder<SystemParameters> builder)
    {
        builder.ToTable(
            "ParametrosSistema",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_ParametrosSistema_AuthorizationLimit",
                    "[AuthorizationLimitCrc] >= 0");
                table.HasCheckConstraint(
                    "CK_ParametrosSistema_OverdueAlertDays",
                    "[OverdueAlertDays] > 0");
            });

        builder.HasKey(parameters => parameters.Id);
        builder.Property(parameters => parameters.Id).ValueGeneratedNever();
        builder.Property(parameters => parameters.AuthorizationLimitCrc)
            .HasPrecision(18, 2)
            .IsRequired();
        builder.Property(parameters => parameters.OverdueAlertDays).IsRequired();
        builder.Property(parameters => parameters.ClosingDate).HasColumnType("date").IsRequired();
        builder.Property(parameters => parameters.CreatedAtUtc).IsRequired();
        builder.Property(parameters => parameters.UpdatedAtUtc).IsRequired();
        builder.Property(parameters => parameters.RowVersion).IsRowVersion();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(parameters => parameters.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ParametrosSistema_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(parameters => parameters.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ParametrosSistema_Usuarios_UpdatedBy");
    }
}
