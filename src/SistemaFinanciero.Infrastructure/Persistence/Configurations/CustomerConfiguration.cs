using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable(
            "Clientes",
            "catalogos",
            table => table.HasCheckConstraint(
                "CK_Clientes_Codigo_NoVacio",
                "LEN(LTRIM(RTRIM([Code]))) > 0"));

        builder.HasKey(customer => customer.Id);
        builder.Property(customer => customer.Code)
            .HasMaxLength(Customer.CodeMaxLength)
            .IsRequired();
        builder.Property(customer => customer.Name)
            .HasMaxLength(Customer.NameMaxLength)
            .IsRequired();
        builder.Property(customer => customer.Identification)
            .HasMaxLength(Customer.IdentificationMaxLength);
        builder.Property(customer => customer.Email)
            .HasMaxLength(Customer.EmailMaxLength);
        builder.Property(customer => customer.Phone)
            .HasMaxLength(Customer.PhoneMaxLength);
        builder.Property(customer => customer.Address)
            .HasMaxLength(Customer.AddressMaxLength);
        builder.Property(customer => customer.IsActive)
            .HasDefaultValue(true);
        builder.Property(customer => customer.CreatedAtUtc).IsRequired();
        builder.Property(customer => customer.UpdatedAtUtc).IsRequired();
        builder.Property(customer => customer.RowVersion).IsRowVersion();

        builder.HasIndex(customer => customer.Code)
            .IsUnique()
            .HasDatabaseName("UX_Clientes_Code");
        builder.HasIndex(customer => customer.Identification)
            .IsUnique()
            .HasFilter("[Identification] IS NOT NULL")
            .HasDatabaseName("UX_Clientes_Identification");
        builder.HasIndex(customer => new { customer.IsActive, customer.Name })
            .HasDatabaseName("IX_Clientes_IsActive_Name");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(customer => customer.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Clientes_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(customer => customer.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Clientes_Usuarios_UpdatedBy");
    }
}
