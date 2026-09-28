using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class WithholdingTypeConfiguration : IEntityTypeConfiguration<WithholdingType>
{
    public void Configure(EntityTypeBuilder<WithholdingType> builder)
    {
        builder.ToTable(
            "TiposRetencion",
            "catalogos",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TiposRetencion_Codigo_NoVacio",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.HasCheckConstraint(
                    "CK_TiposRetencion_Rate",
                    "[Rate] >= 0 AND [Rate] <= 100");
            });

        builder.HasKey(withholding => withholding.Id);
        builder.Property(withholding => withholding.Code)
            .HasMaxLength(WithholdingType.CodeMaxLength)
            .IsRequired();
        builder.Property(withholding => withholding.Name)
            .HasMaxLength(WithholdingType.NameMaxLength)
            .IsRequired();
        builder.Property(withholding => withholding.Rate)
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(withholding => withholding.Description)
            .HasMaxLength(WithholdingType.DescriptionMaxLength);
        builder.Property(withholding => withholding.IsActive)
            .HasDefaultValue(true);
        builder.Property(withholding => withholding.CreatedAtUtc).IsRequired();
        builder.Property(withholding => withholding.UpdatedAtUtc).IsRequired();
        builder.Property(withholding => withholding.RowVersion).IsRowVersion();

        builder.HasIndex(withholding => withholding.Code)
            .IsUnique()
            .HasDatabaseName("UX_TiposRetencion_Code");
        builder.HasIndex(withholding => new { withholding.IsActive, withholding.Name })
            .HasDatabaseName("IX_TiposRetencion_IsActive_Name");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(withholding => withholding.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TiposRetencion_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(withholding => withholding.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TiposRetencion_Usuarios_UpdatedBy");
    }
}
