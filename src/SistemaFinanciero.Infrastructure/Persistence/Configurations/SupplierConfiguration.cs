using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable(
            "Proveedores",
            "catalogos",
            table => table.HasCheckConstraint(
                "CK_Proveedores_Codigo_NoVacio",
                "LEN(LTRIM(RTRIM([Code]))) > 0"));

        builder.HasKey(supplier => supplier.Id);
        builder.Property(supplier => supplier.Code)
            .HasMaxLength(Supplier.CodeMaxLength)
            .IsRequired();
        builder.Property(supplier => supplier.Name)
            .HasMaxLength(Supplier.NameMaxLength)
            .IsRequired();
        builder.Property(supplier => supplier.Identification)
            .HasMaxLength(Supplier.IdentificationMaxLength);
        builder.Property(supplier => supplier.Email)
            .HasMaxLength(Supplier.EmailMaxLength);
        builder.Property(supplier => supplier.Phone)
            .HasMaxLength(Supplier.PhoneMaxLength);
        builder.Property(supplier => supplier.Address)
            .HasMaxLength(Supplier.AddressMaxLength);
        builder.Property(supplier => supplier.IsActive)
            .HasDefaultValue(true);
        builder.Property(supplier => supplier.CreatedAtUtc).IsRequired();
        builder.Property(supplier => supplier.UpdatedAtUtc).IsRequired();
        builder.Property(supplier => supplier.RowVersion).IsRowVersion();

        builder.HasIndex(supplier => supplier.Code)
            .IsUnique()
            .HasDatabaseName("UX_Proveedores_Code");
        builder.HasIndex(supplier => supplier.Identification)
            .IsUnique()
            .HasFilter("[Identification] IS NOT NULL")
            .HasDatabaseName("UX_Proveedores_Identification");
        builder.HasIndex(supplier => new { supplier.IsActive, supplier.Name })
            .HasDatabaseName("IX_Proveedores_IsActive_Name");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(supplier => supplier.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Proveedores_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(supplier => supplier.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Proveedores_Usuarios_UpdatedBy");
    }
}
