using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class FinancialCategoryConfiguration : IEntityTypeConfiguration<FinancialCategory>
{
    public void Configure(EntityTypeBuilder<FinancialCategory> builder)
    {
        builder.ToTable(
            "CategoriasFinancieras",
            "catalogos",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CategoriasFinancieras_Codigo_NoVacio",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.HasCheckConstraint(
                    "CK_CategoriasFinancieras_Kind",
                    "[Kind] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_CategoriasFinancieras_Parent",
                    "[ParentId] IS NULL OR [ParentId] <> [Id]");
            });

        builder.HasKey(category => category.Id);
        builder.Property(category => category.Code)
            .HasMaxLength(FinancialCategory.CodeMaxLength)
            .IsRequired();
        builder.Property(category => category.Name)
            .HasMaxLength(FinancialCategory.NameMaxLength)
            .IsRequired();
        builder.Property(category => category.Kind).IsRequired();
        builder.Property(category => category.Description)
            .HasMaxLength(FinancialCategory.DescriptionMaxLength);
        builder.Property(category => category.IsActive)
            .HasDefaultValue(true);
        builder.Property(category => category.CreatedAtUtc).IsRequired();
        builder.Property(category => category.UpdatedAtUtc).IsRequired();
        builder.Property(category => category.RowVersion).IsRowVersion();

        builder.HasIndex(category => category.Code)
            .IsUnique()
            .HasDatabaseName("UX_CategoriasFinancieras_Code");
        builder.HasIndex(category => new { category.IsActive, category.Kind, category.Name })
            .HasDatabaseName("IX_CategoriasFinancieras_IsActive_Kind_Name");

        builder.Property(category => category.LedgerAccountId).IsRequired();

        builder.HasOne<LedgerAccount>()
            .WithMany()
            .HasForeignKey(category => category.LedgerAccountId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CategoriasFinancieras_CuentasContables");
        builder.HasOne<FinancialCategory>()
            .WithMany()
            .HasForeignKey(category => category.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CategoriasFinancieras_Parent");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(category => category.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CategoriasFinancieras_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(category => category.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CategoriasFinancieras_Usuarios_UpdatedBy");
    }
}
