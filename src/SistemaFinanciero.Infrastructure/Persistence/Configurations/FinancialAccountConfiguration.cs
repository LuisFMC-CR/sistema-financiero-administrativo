using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SistemaFinanciero.Domain.Catalogs;
using SistemaFinanciero.Infrastructure.Identity;

namespace SistemaFinanciero.Infrastructure.Persistence.Configurations;

internal sealed class FinancialAccountConfiguration : IEntityTypeConfiguration<FinancialAccount>
{
    public void Configure(EntityTypeBuilder<FinancialAccount> builder)
    {
        builder.ToTable(
            "CuentasFinancieras",
            "finanzas",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_CuentasFinancieras_Codigo_NoVacio",
                    "LEN(LTRIM(RTRIM([Code]))) > 0");
                table.HasCheckConstraint(
                    "CK_CuentasFinancieras_Type",
                    "[Type] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_CuentasFinancieras_Currency",
                    "[Currency] IN ('CRC', 'USD')");
            });

        builder.HasKey(account => account.Id);
        builder.Property(account => account.Code)
            .HasMaxLength(FinancialAccount.CodeMaxLength)
            .IsRequired();
        builder.Property(account => account.Name)
            .HasMaxLength(FinancialAccount.NameMaxLength)
            .IsRequired();
        builder.Property(account => account.Type).IsRequired();
        builder.Property(account => account.Currency)
            .HasConversion<string>()
            .HasColumnType("char(3)")
            .IsUnicode(false)
            .IsRequired();
        builder.Property(account => account.Reference)
            .HasMaxLength(FinancialAccount.ReferenceMaxLength);
        builder.Property(account => account.IsActive)
            .HasDefaultValue(true);
        builder.Property(account => account.CreatedAtUtc).IsRequired();
        builder.Property(account => account.UpdatedAtUtc).IsRequired();
        builder.Property(account => account.RowVersion).IsRowVersion();

        builder.HasIndex(account => account.Code)
            .IsUnique()
            .HasDatabaseName("UX_CuentasFinancieras_Code");
        builder.HasIndex(account => new { account.IsActive, account.Currency, account.Name })
            .HasDatabaseName("IX_CuentasFinancieras_IsActive_Currency_Name");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(account => account.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CuentasFinancieras_Usuarios_CreatedBy");
        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(account => account.UpdatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_CuentasFinancieras_Usuarios_UpdatedBy");
    }
}
