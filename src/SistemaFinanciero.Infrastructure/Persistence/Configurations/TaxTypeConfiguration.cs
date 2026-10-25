using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

/// <summary>Mapea <see cref="TaxType"/> a la tabla <c>catalogos.TiposImpuesto</c>.</summary>
/// <remarks>
/// Conexiones: <c>FinancialDbContext.OnModelCreating</c> la aplica por convención. La usa
/// <c>TaxTypeService</c>; <c>InvoiceLineTaxConfiguration</c> tiene la clave foránea restrictiva que
/// impide borrar un tipo que una línea de factura ya usó.
/// </remarks>
internal sealed class TaxTypeConfiguration : IEntityTypeConfiguration<TaxType>
{
    public void Configure(EntityTypeBuilder<TaxType> builder)
    {
        builder.ToTable(
            "TiposImpuesto",
            "catalogos",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_TiposImpuesto_Codigo_NoVacio",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.HasCheckConstraint(
                    "CK_TiposImpuesto_Rate",
                    "[Rate] >= 0 AND [Rate] <= 100");
            });

        builder.HasKey(taxType => taxType.Id);
        builder.Property(taxType => taxType.Code)
            .HasMaxLength(TaxType.CodeMaxLength)
            .IsRequired();
        builder.Property(taxType => taxType.Name)
            .HasMaxLength(TaxType.NameMaxLength)
            .IsRequired();
        builder.Property(taxType => taxType.Rate)
            .HasPrecision(18, 4)
            .IsRequired();
        builder.Property(taxType => taxType.Description)
            .HasMaxLength(TaxType.DescriptionMaxLength);
        builder.Property(taxType => taxType.IsActive)
            .HasDefaultValue(true);
        builder.Property(taxType => taxType.CreatedAtUtc).IsRequired();
        builder.Property(taxType => taxType.UpdatedAtUtc).IsRequired();
        builder.Property(taxType => taxType.RowVersion).IsRowVersion();

        builder.HasIndex(taxType => taxType.Code)
            .IsUnique()
            .HasDatabaseName("UX_TiposImpuesto_Code");
        builder.HasIndex(taxType => new { taxType.IsActive, taxType.Name })
            .HasDatabaseName("IX_TiposImpuesto_IsActive_Name");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(taxType => taxType.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TiposImpuesto_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(taxType => taxType.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_TiposImpuesto_Usuarios_UpdatedBy");
    }
}
