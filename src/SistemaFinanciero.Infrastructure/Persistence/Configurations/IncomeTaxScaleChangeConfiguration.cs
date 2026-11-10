using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="IncomeTaxScaleChange"/> a <c>finanzas.EscalaRentaHistorial</c> (RF-99).</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Solo
/// <c>IncomeTaxService.UpdateScaleAsync</c> le agrega filas; nadie las modifica, protegido por
/// <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>.
/// </remarks>
internal sealed class IncomeTaxScaleChangeConfiguration : IEntityTypeConfiguration<IncomeTaxScaleChange>
{
    public void Configure(EntityTypeBuilder<IncomeTaxScaleChange> builder)
    {
        builder.ToTable("EscalaRentaHistorial", "finanzas");

        builder.HasKey(change => change.Id);
        builder.Property(change => change.Id).ValueGeneratedNever();
        builder.Property(change => change.Bracket1UpperLimit).HasPrecision(18, 2).IsRequired();
        builder.Property(change => change.Bracket2UpperLimit).HasPrecision(18, 2).IsRequired();
        builder.Property(change => change.Bracket3UpperLimit).HasPrecision(18, 2).IsRequired();
        builder.Property(change => change.Bracket1Rate).HasPrecision(5, 2).IsRequired();
        builder.Property(change => change.Bracket2Rate).HasPrecision(5, 2).IsRequired();
        builder.Property(change => change.Bracket3Rate).HasPrecision(5, 2).IsRequired();
        builder.Property(change => change.Bracket4Rate).HasPrecision(5, 2).IsRequired();
        builder.Property(change => change.GrossIncomeThreshold).HasPrecision(18, 2).IsRequired();
        builder.Property(change => change.GeneralRate).HasPrecision(5, 2).IsRequired();
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => change.ChangedAtUtc)
            .HasDatabaseName("IX_EscalaRentaHistorial_ChangedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_EscalaRentaHistorial_Usuarios_ChangedBy");
    }
}
