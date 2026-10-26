using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Parameters;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configura la bitácora, de solo inserción, de cambios en los datos de la empresa.
/// </summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. Solo
/// <c>SystemParametersService.UpdateCompanyAsync</c> le agrega filas; nadie las modifica, protegido
/// por <c>FinancialDbContext.EnsureAppendOnlyLogsAreNotMutated</c>.
/// </remarks>
internal sealed class CompanyDataChangeConfiguration : IEntityTypeConfiguration<CompanyDataChange>
{
    public void Configure(EntityTypeBuilder<CompanyDataChange> builder)
    {
        builder.ToTable("ParametrosEmpresaHistorial", "finanzas");

        builder.HasKey(change => change.Id);
        builder.Property(change => change.CompanyName).HasMaxLength(SystemParameters.CompanyNameMaxLength).IsRequired();
        builder.Property(change => change.CompanyLegalId).HasMaxLength(SystemParameters.CompanyLegalIdMaxLength).IsRequired();
        builder.Property(change => change.CompanyAddress).HasMaxLength(SystemParameters.CompanyAddressMaxLength);
        builder.Property(change => change.CompanyPhone).HasMaxLength(SystemParameters.CompanyPhoneMaxLength);
        builder.Property(change => change.CompanyEmail).HasMaxLength(SystemParameters.CompanyEmailMaxLength);
        builder.Property(change => change.ChangedAtUtc).IsRequired();

        builder.HasIndex(change => change.ChangedAtUtc)
            .HasDatabaseName("IX_ParametrosEmpresaHistorial_ChangedAtUtc");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(change => change.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_ParametrosEmpresaHistorial_Usuarios_ChangedBy");
    }
}
